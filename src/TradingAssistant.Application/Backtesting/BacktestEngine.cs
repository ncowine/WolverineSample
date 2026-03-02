using TradingAssistant.Application.Indicators;
using TradingAssistant.Application.Intelligence;
using TradingAssistant.Contracts.Backtesting;

namespace TradingAssistant.Application.Backtesting;

/// <summary>
/// Bar-by-bar backtesting engine. Iterates over pre-computed daily bars,
/// evaluates strategy conditions, simulates order fills, and tracks equity.
/// No look-ahead bias: signal on bar N → fill at bar N+1 open.
/// </summary>
public class BacktestEngine
{
    private readonly StrategyDefinition _strategy;
    private readonly BacktestConfig _config;

    private decimal _cash;
    private decimal _peakEquity;
    private bool _circuitBreakerActive;
    private readonly List<Position> _openPositions = new();
    private readonly List<PendingOrder> _pendingOrders = new();
    private readonly List<TradeRecord> _trades = new();
    private readonly List<EquityPoint> _equityCurve = new();
    private readonly List<string> _log = new();
    private IReadOnlyDictionary<string, decimal>? _correlationData;
    private IReadOnlyDictionary<string, string>? _symbolToMarket;
    private CostProfileData? _costProfile;
    private string? _currentRegime;
    private decimal? _currentRegimeConfidence;

    public BacktestEngine(StrategyDefinition strategy, BacktestConfig? config = null)
    {
        _strategy = strategy;
        _config = config ?? BacktestConfig.Default;
        _cash = _config.InitialCapital;
        _peakEquity = _config.InitialCapital;
    }

    /// <summary>
    /// Provide pairwise correlation data for correlation-aware allocation.
    /// Keys use "SYMBOL_A|SYMBOL_B" format (alphabetically ordered).
    /// </summary>
    public void SetCorrelationData(IReadOnlyDictionary<string, decimal> correlations)
    {
        _correlationData = correlations;
    }

    /// <summary>
    /// Provide symbol-to-market mapping for geographic risk budget enforcement.
    /// Keys are stock symbols, values are market codes (e.g. "US_SP500", "IN_NIFTY50").
    /// </summary>
    public void SetMarketMapping(IReadOnlyDictionary<string, string> symbolToMarket)
    {
        _symbolToMarket = symbolToMarket;
    }

    /// <summary>
    /// Set market-specific cost profile for realistic transaction cost modeling.
    /// When set, replaces the flat CommissionPerTrade with per-share and percent-based fees.
    /// </summary>
    public void SetCostProfile(CostProfileData costProfile)
    {
        _costProfile = costProfile;
    }

    /// <summary>
    /// Set current market regime for circuit breaker resume gating.
    /// Call before Run() or update during multi-symbol backtesting.
    /// </summary>
    public void SetRegimeContext(string regime, decimal confidence)
    {
        _currentRegime = regime;
        _currentRegimeConfidence = confidence;
    }

    /// <summary>
    /// Run the backtest over aligned daily bars (from IndicatorOrchestrator).
    /// </summary>
    public BacktestEngineResult Run(CandleWithIndicators[] bars, string symbol)
    {
        if (bars.Length < 2)
            return EmptyResult(symbol, bars);

        for (var i = 1; i < bars.Length; i++)
        {
            var bar = bars[i];
            var prevBar = bars[i - 1];

            // 1. Fill pending orders at today's open
            FillPendingOrders(bar);

            // 2. Check intra-bar stop loss and take profit on open positions
            CheckStopsAndTargets(bar);

            // 3. Check exit conditions on remaining positions
            CheckExitConditions(bar, prevBar);

            // 4. Evaluate entry conditions (signal generated today, order fills tomorrow)
            if (ConditionEvaluator.Evaluate(_strategy.EntryConditions, bar, prevBar))
            {
                TryPlaceEntryOrder(bar, symbol);
            }

            // 5. Record equity (cash + market value of positions at close)
            RecordEquity(bar);
        }

        // Close any remaining open positions at last bar's close
        CloseAllPositions(bars[^1], "EndOfBacktest");

        return BuildResult(symbol, bars);
    }

    private void FillPendingOrders(CandleWithIndicators bar)
    {
        var filled = new List<PendingOrder>();

        foreach (var order in _pendingOrders)
        {
            decimal fillPrice;

            if (order.Type == OrderType.Market)
            {
                // Market order fills at open with slippage
                fillPrice = order.Side == OrderSide.Buy
                    ? bar.Open * (1 + _config.SlippagePercent / 100m)
                    : bar.Open * (1 - _config.SlippagePercent / 100m);
            }
            else // Limit
            {
                // Limit buy: fill if low ≤ limit price
                if (order.Side == OrderSide.Buy && bar.Low <= order.LimitPrice!.Value)
                    fillPrice = Math.Min(bar.Open, order.LimitPrice.Value);
                // Limit sell: fill if high ≥ limit price
                else if (order.Side == OrderSide.Sell && bar.High >= order.LimitPrice!.Value)
                    fillPrice = Math.Max(bar.Open, order.LimitPrice.Value);
                else
                    continue; // Not filled
            }

            if (order.Side == OrderSide.Buy)
            {
                var entryCost = GetTradeCost(fillPrice, order.Shares);
                var cost = order.Shares * fillPrice + entryCost;
                if (cost > _cash)
                {
                    _log.Add($"{bar.Timestamp:yyyy-MM-dd} SKIP BUY {order.Symbol}: insufficient cash ({_cash:C} < {cost:C})");
                    filled.Add(order);
                    continue;
                }

                _cash -= cost;

                // Recalculate stop/TP based on actual fill price
                var stopLoss = CalculateStopLoss(fillPrice, bar);
                var takeProfit = CalculateTakeProfit(fillPrice, stopLoss);

                var position = new Position
                {
                    Symbol = order.Symbol,
                    EntryDate = bar.Timestamp,
                    EntryPrice = fillPrice,
                    Shares = order.Shares,
                    StopLoss = stopLoss,
                    TakeProfit = takeProfit,
                    CostBasis = cost
                };

                _openPositions.Add(position);
                _log.Add($"{bar.Timestamp:yyyy-MM-dd} BUY {order.Shares} {order.Symbol} @ {fillPrice:F2} (stop={stopLoss:F2}, tp={takeProfit:F2})");
            }

            filled.Add(order);
        }

        foreach (var f in filled)
            _pendingOrders.Remove(f);
    }

    private void CheckStopsAndTargets(CandleWithIndicators bar)
    {
        var closed = new List<Position>();

        foreach (var pos in _openPositions)
        {
            // Stop loss: if bar low touches stop, exit at stop price
            if (bar.Low <= pos.StopLoss)
            {
                ClosePosition(pos, bar.Timestamp, pos.StopLoss, "StopLoss");
                closed.Add(pos);
                continue;
            }

            // Take profit: if bar high touches target, exit at target price
            if (pos.TakeProfit > 0 && bar.High >= pos.TakeProfit)
            {
                ClosePosition(pos, bar.Timestamp, pos.TakeProfit, "TakeProfit");
                closed.Add(pos);
            }
        }

        foreach (var c in closed)
            _openPositions.Remove(c);
    }

    private void CheckExitConditions(CandleWithIndicators bar, CandleWithIndicators prevBar)
    {
        if (_strategy.ExitConditions.Count == 0 || _openPositions.Count == 0)
            return;

        if (!ConditionEvaluator.Evaluate(_strategy.ExitConditions, bar, prevBar))
            return;

        // Exit all positions at close
        var toClose = _openPositions.ToList();
        foreach (var pos in toClose)
        {
            ClosePosition(pos, bar.Timestamp, bar.Close, "ExitSignal");
            _openPositions.Remove(pos);
        }
    }

    private void TryPlaceEntryOrder(CandleWithIndicators bar, string symbol)
    {
        // Circuit breaker: drawdown exceeded threshold
        if (_circuitBreakerActive)
        {
            _log.Add($"{bar.Timestamp:yyyy-MM-dd} SKIP ENTRY: circuit breaker active (drawdown exceeded {_strategy.PositionSizing.MaxDrawdownPercent}%)");
            return;
        }

        // No averaging down: don't enter if already holding this symbol
        if (_openPositions.Any(p => p.Symbol == symbol))
        {
            _log.Add($"{bar.Timestamp:yyyy-MM-dd} SKIP ENTRY: already holding {symbol} (no averaging down)");
            return;
        }

        // Check max positions
        if (_openPositions.Count >= _strategy.PositionSizing.MaxPositions)
        {
            _log.Add($"{bar.Timestamp:yyyy-MM-dd} SKIP ENTRY: max positions ({_strategy.PositionSizing.MaxPositions}) reached");
            return;
        }

        // Check portfolio heat
        var totalRisk = _openPositions.Sum(p => p.RiskAmount);
        var equity = _cash + _openPositions.Sum(p => p.MarketValue(bar.Close));
        var heatPercent = equity > 0 ? totalRisk / equity * 100m : 0;
        if (heatPercent >= _strategy.PositionSizing.MaxPortfolioHeat)
        {
            _log.Add($"{bar.Timestamp:yyyy-MM-dd} SKIP ENTRY: portfolio heat {heatPercent:F1}% >= {_strategy.PositionSizing.MaxPortfolioHeat}%");
            return;
        }

        // Calculate position size
        var stopLoss = CalculateStopLoss(bar.Close, bar);
        var riskPerShare = bar.Close - stopLoss;
        if (riskPerShare <= 0)
        {
            _log.Add($"{bar.Timestamp:yyyy-MM-dd} SKIP ENTRY: invalid stop loss (stop >= price)");
            return;
        }

        var riskPercent = CalculateRiskPercent(equity, heatPercent);
        var riskAmount = equity * riskPercent / 100m;

        // Vol-targeting: use ATR × multiplier as risk-per-share instead of price - stop
        int shares;
        if (_strategy.PositionSizing.UseVolTargeting && bar.Indicators.Atr > 0)
        {
            shares = VolatilityTargeting.CalculateShares(
                riskAmount, bar.Indicators.Atr, _strategy.PositionSizing.VolTargetAtrMultiplier);
        }
        else
        {
            shares = (int)(riskAmount / riskPerShare);
        }

        if (shares <= 0)
        {
            _log.Add($"{bar.Timestamp:yyyy-MM-dd} SKIP ENTRY: calculated 0 shares");
            return;
        }

        // Correlation-aware allocation: block or reduce correlated positions
        if (_strategy.PositionSizing.UseCorrelationFilter && _correlationData != null)
        {
            var openSymbols = _openPositions.Select(p => p.Symbol).Distinct().ToList();
            var corrCheck = CorrelationFilter.Check(
                symbol, openSymbols, _correlationData,
                _strategy.PositionSizing.CorrelationBlockThreshold,
                _strategy.PositionSizing.CorrelationReduceThreshold);

            _log.Add($"{bar.Timestamp:yyyy-MM-dd} CORRELATION {symbol}: {corrCheck.Detail}");

            if (corrCheck.Action == CorrelationAction.Block)
            {
                _log.Add($"{bar.Timestamp:yyyy-MM-dd} SKIP ENTRY: correlation block (avg={corrCheck.AvgCorrelation:F4})");
                return;
            }

            if (corrCheck.Action == CorrelationAction.Reduce)
            {
                shares = CorrelationFilter.AdjustShares(shares, corrCheck);
                if (shares <= 0)
                {
                    _log.Add($"{bar.Timestamp:yyyy-MM-dd} SKIP ENTRY: correlation reduction to 0 shares");
                    return;
                }
                _log.Add($"{bar.Timestamp:yyyy-MM-dd} REDUCED to {shares} shares (corr multiplier={corrCheck.SizeMultiplier:F4})");
            }
        }

        // Geographic risk budget: block if market allocation would exceed limit
        if (_strategy.PositionSizing.UseGeographicRiskBudget && _symbolToMarket != null)
        {
            var posNotionals = _openPositions
                .Select(p => (p.Symbol, Notional: p.MarketValue(bar.Close)))
                .ToList();
            var marketNotionals = GeographicRiskBudget.ComputeMarketNotionals(posNotionals, _symbolToMarket);
            var candidateMarket = _symbolToMarket.GetValueOrDefault(symbol, "UNKNOWN");
            var proposedNotional = shares * bar.Close;
            var geoCheck = GeographicRiskBudget.Check(
                candidateMarket, proposedNotional, marketNotionals, equity,
                _strategy.PositionSizing.MaxMarketAllocationPercent);

            _log.Add($"{bar.Timestamp:yyyy-MM-dd} GEO BUDGET {symbol}: {geoCheck.Detail}");

            if (!geoCheck.Allowed)
            {
                _log.Add($"{bar.Timestamp:yyyy-MM-dd} SKIP ENTRY: geographic budget exceeded for {candidateMarket}");
                return;
            }
        }

        // Check if we have enough cash for the order (rough estimate)
        var estCommission = GetTradeCost(bar.Close, shares);
        var estimatedCost = shares * bar.Close * (1 + _config.SlippagePercent / 100m) + estCommission;
        if (estimatedCost > _cash)
        {
            // Reduce share count to fit available cash
            var costPerShare = bar.Close * (1 + _config.SlippagePercent / 100m);
            var availableForShares = _cash - GetTradeCost(bar.Close, 1); // rough reserve for commission
            shares = (int)(availableForShares / costPerShare);
            if (shares <= 0)
            {
                _log.Add($"{bar.Timestamp:yyyy-MM-dd} SKIP ENTRY: insufficient cash");
                return;
            }
        }

        // Apply trade filters
        if (!PassesFilters(bar))
        {
            _log.Add($"{bar.Timestamp:yyyy-MM-dd} SKIP ENTRY: failed trade filters");
            return;
        }

        var order = new PendingOrder
        {
            Symbol = symbol,
            Type = OrderType.Market,
            Side = OrderSide.Buy,
            Shares = shares,
            StopLoss = stopLoss,
            TakeProfit = CalculateTakeProfit(bar.Close, stopLoss),
            SignalDate = bar.Timestamp
        };

        _pendingOrders.Add(order);
        _log.Add($"{bar.Timestamp:yyyy-MM-dd} SIGNAL BUY {shares} {symbol} (pending fill tomorrow)");
    }

    private decimal CalculateRiskPercent(decimal equity, decimal currentHeatPercent)
    {
        var sizing = _strategy.PositionSizing;

        if (!string.Equals(sizing.SizingMethod, "Kelly", StringComparison.OrdinalIgnoreCase))
            return sizing.RiskPercent;

        var tradePnls = _trades.Select(t => t.PnL).ToList();

        var result = KellyCriterion.CalculatePositionRisk(
            tradePnls,
            equity,
            currentHeatPercent,
            sizing.MaxPortfolioHeat,
            fixedRiskPercent: sizing.RiskPercent,
            kellyMultiplier: sizing.KellyMultiplier,
            windowSize: sizing.KellyWindowSize);

        return result.RiskPercent;
    }

    private decimal CalculateStopLoss(decimal price, CandleWithIndicators bar)
    {
        return _strategy.StopLoss.Type switch
        {
            "Atr" when bar.Indicators.Atr > 0 =>
                price - bar.Indicators.Atr * _strategy.StopLoss.Multiplier,
            "FixedPercent" =>
                price * (1 - _strategy.StopLoss.Multiplier / 100m),
            _ => price * 0.95m // fallback 5%
        };
    }

    private decimal CalculateTakeProfit(decimal price, decimal stopLoss)
    {
        var risk = price - stopLoss;
        return _strategy.TakeProfit.Type switch
        {
            "RMultiple" => price + risk * _strategy.TakeProfit.Multiplier,
            "FixedPercent" => price * (1 + _strategy.TakeProfit.Multiplier / 100m),
            _ => 0m // no take profit
        };
    }

    private bool PassesFilters(CandleWithIndicators bar)
    {
        var filters = _strategy.Filters;
        if (filters.MinVolume.HasValue && bar.Volume < filters.MinVolume.Value)
            return false;
        if (filters.MinPrice.HasValue && bar.Close < filters.MinPrice.Value)
            return false;
        if (filters.MaxPrice.HasValue && bar.Close > filters.MaxPrice.Value)
            return false;
        return true;
    }

    /// <summary>
    /// Get the cost for a single trade leg using cost profile or flat commission fallback.
    /// </summary>
    private decimal GetTradeCost(decimal price, int shares)
    {
        if (_costProfile != null)
            return MarketCostCalculator.EstimateTradeCost(price, shares, _costProfile);
        return _config.CommissionPerTrade;
    }

    private void ClosePosition(Position pos, DateTime exitDate, decimal exitPrice, string reason)
    {
        var exitCost = GetTradeCost(exitPrice, pos.Shares);
        var entryCost = GetTradeCost(pos.EntryPrice, pos.Shares);
        var proceeds = pos.Shares * exitPrice - exitCost;
        _cash += proceeds;

        var totalCost = entryCost + exitCost;
        var pnl = pos.Shares * (exitPrice - pos.EntryPrice) - totalCost;
        var pnlPercent = pos.EntryPrice != 0 ? (exitPrice - pos.EntryPrice) / pos.EntryPrice * 100m : 0;

        _trades.Add(new TradeRecord
        {
            Symbol = pos.Symbol,
            EntryDate = pos.EntryDate,
            EntryPrice = pos.EntryPrice,
            ExitDate = exitDate,
            ExitPrice = exitPrice,
            Shares = pos.Shares,
            PnL = pnl,
            PnLPercent = pnlPercent,
            Commission = totalCost,
            HoldingDays = (exitDate - pos.EntryDate).Days,
            ExitReason = reason
        });

        _log.Add($"{exitDate:yyyy-MM-dd} SELL {pos.Shares} {pos.Symbol} @ {exitPrice:F2} ({reason}) PnL={pnl:F2}");
    }

    private void CloseAllPositions(CandleWithIndicators bar, string reason)
    {
        foreach (var pos in _openPositions.ToList())
            ClosePosition(pos, bar.Timestamp, bar.Close, reason);
        _openPositions.Clear();
    }

    private void RecordEquity(CandleWithIndicators bar)
    {
        var positionsValue = _openPositions.Sum(p => p.MarketValue(bar.Close));
        var equity = _cash + positionsValue;
        _equityCurve.Add(new EquityPoint(bar.Timestamp, equity));

        // Update peak equity
        if (equity > _peakEquity)
            _peakEquity = equity;

        // Evaluate circuit breaker with regime context
        var eval = CircuitBreaker.Evaluate(
            equity, _peakEquity, _circuitBreakerActive,
            _strategy.PositionSizing.MaxDrawdownPercent,
            _strategy.PositionSizing.DrawdownRecoveryPercent,
            _currentRegime, _currentRegimeConfidence);

        if (eval.ShouldActivate)
        {
            _circuitBreakerActive = true;
            var cancelled = CircuitBreaker.CancelPendingOrders(_pendingOrders);
            _log.Add($"{bar.Timestamp:yyyy-MM-dd} CIRCUIT BREAKER {eval.Detail}");
            if (cancelled > 0)
                _log.Add($"{bar.Timestamp:yyyy-MM-dd} CANCELLED {cancelled} pending order(s)");
        }
        else if (eval.ShouldDeactivate)
        {
            _circuitBreakerActive = false;
            _log.Add($"{bar.Timestamp:yyyy-MM-dd} CIRCUIT BREAKER {eval.Detail}");
        }
    }

    private BacktestEngineResult BuildResult(string symbol, CandleWithIndicators[] bars)
    {
        var finalEquity = _cash + _openPositions.Sum(p => p.MarketValue(bars[^1].Close));

        return new BacktestEngineResult
        {
            Symbol = symbol,
            StartDate = bars[0].Timestamp,
            EndDate = bars[^1].Timestamp,
            InitialCapital = _config.InitialCapital,
            FinalEquity = finalEquity,
            Trades = _trades,
            EquityCurve = _equityCurve,
            Log = _log
        };
    }

    private BacktestEngineResult EmptyResult(string symbol, CandleWithIndicators[] bars)
    {
        return new BacktestEngineResult
        {
            Symbol = symbol,
            StartDate = bars.Length > 0 ? bars[0].Timestamp : DateTime.MinValue,
            EndDate = bars.Length > 0 ? bars[^1].Timestamp : DateTime.MinValue,
            InitialCapital = _config.InitialCapital,
            FinalEquity = _config.InitialCapital,
            Trades = new(),
            EquityCurve = new(),
            Log = new List<string> { "Insufficient data for backtest (need at least 2 bars)" }
        };
    }
}
