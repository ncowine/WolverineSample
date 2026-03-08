using System.Text.Json;
using TradingAssistant.Application.Indicators;
using TradingAssistant.Application.Intelligence;
using TradingAssistant.Contracts.Backtesting;

namespace TradingAssistant.Application.Backtesting;

/// <summary>
/// Unified backtest engine: handles both single-symbol (universe of 1)
/// and multi-symbol portfolio backtests. Day-by-day simulation with
/// regime-adaptive strategy switching, circuit breaker, Kelly sizing,
/// vol targeting, correlation filtering, geographic budgets, and cost profiles.
/// </summary>
public class PortfolioBacktestEngine
{
    private StrategyDefinition _strategy;
    private readonly Dictionary<string, StrategyDefinition>? _regimeStrategies;
    private readonly int _maxPositions;
    private readonly decimal _initialCapital;
    private readonly BacktestConfig _config;
    private const int RegimeCheckIntervalDays = 21; // ~monthly

    private decimal _cash;
    private decimal _peakEquity;
    private bool _circuitBreakerActive;
    private readonly List<PortfolioPosition> _openPositions = new();
    private readonly List<PendingPortfolioOrder> _pendingOrders = new();
    private readonly List<TradeRecord> _trades = new();
    private readonly List<EquityPoint> _equityCurve = new();
    private readonly List<string> _log = new();
    private readonly List<(DateTime Date, string Regime)> _regimeTimeline = new();
    private int _maxPositionsHeld;
    private long _totalPositionDays;
    private long _totalDays;
    private string _currentRegime = "";
    private decimal _currentRegimeConfidence;

    // Optional features (set via methods before Run)
    private IReadOnlyDictionary<string, decimal>? _correlationData;
    private IReadOnlyDictionary<string, string>? _symbolToMarket;
    private CostProfileData? _costProfile;

    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public PortfolioBacktestEngine(
        StrategyDefinition strategy,
        int maxPositions = 10,
        decimal initialCapital = 100_000m,
        BacktestConfig? config = null,
        Dictionary<string, StrategyDefinition>? regimeStrategies = null)
    {
        _strategy = strategy;
        _regimeStrategies = regimeStrategies;
        _maxPositions = maxPositions;
        _config = config ?? BacktestConfig.Default;
        _initialCapital = initialCapital;
        _cash = initialCapital;
        _peakEquity = initialCapital;
    }

    /// <summary>Provide pairwise correlation data for correlation-aware allocation.</summary>
    public void SetCorrelationData(IReadOnlyDictionary<string, decimal> correlations)
        => _correlationData = correlations;

    /// <summary>Provide symbol-to-market mapping for geographic risk budget.</summary>
    public void SetMarketMapping(IReadOnlyDictionary<string, string> symbolToMarket)
        => _symbolToMarket = symbolToMarket;

    /// <summary>Set market-specific cost profile.</summary>
    public void SetCostProfile(CostProfileData costProfile)
        => _costProfile = costProfile;

    /// <summary>
    /// Run the backtest across all symbols. Single-symbol = dictionary with one entry.
    /// </summary>
    public BacktestEngineResult Run(Dictionary<string, CandleWithIndicators[]> symbolBars)
    {
        // 1. Build sorted date index
        var allDates = symbolBars.Values
            .SelectMany(bars => bars.Select(b => b.Timestamp))
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        if (allDates.Count < 2)
            return EmptyResult(allDates);

        // Build per-symbol lookup
        var symbolDateIndex = new Dictionary<string, Dictionary<DateTime, int>>();
        foreach (var (symbol, bars) in symbolBars)
        {
            var dateIdx = new Dictionary<DateTime, int>();
            for (var i = 0; i < bars.Length; i++)
                dateIdx[bars[i].Timestamp] = i;
            symbolDateIndex[symbol] = dateIdx;
        }

        _log.Add($"Backtest starting: {symbolBars.Count} symbols, {allDates.Count} trading days, capital={_initialCapital:C0}");

        // 2. Day-by-day simulation
        var lastRegimeCheck = 0;
        for (var dayIdx = 1; dayIdx < allDates.Count; dayIdx++)
        {
            var today = allDates[dayIdx];
            var yesterday = allDates[dayIdx - 1];

            // Regime check
            if (_regimeStrategies is { Count: > 0 } && dayIdx - lastRegimeCheck >= RegimeCheckIntervalDays)
            {
                lastRegimeCheck = dayIdx;
                var regime = DetectRegimeFromBars(symbolBars, symbolDateIndex, allDates, dayIdx);
                if (!string.IsNullOrEmpty(regime) && _regimeStrategies.TryGetValue(regime, out var newStrategy))
                {
                    newStrategy.PositionSizing.MaxPositions = _maxPositions;
                    _strategy = newStrategy;
                    if (_currentRegime != regime)
                    {
                        _log.Add($"{today:yyyy-MM-dd} REGIME SWITCH: {_currentRegime} → {regime}");
                        _currentRegime = regime;
                        _regimeTimeline.Add((today, regime));
                    }
                }
            }

            // 2a. Fill pending orders
            FillPendingOrders(today, symbolBars, symbolDateIndex);

            // 2b. Check stops and targets
            CheckStopsAndTargets(today, symbolBars, symbolDateIndex);

            // 2b2. Update trailing stops
            UpdateTrailingStops(today, symbolBars, symbolDateIndex);

            // 2b3. Apply time-decay tightening
            ApplyTimeDecay(today);

            // 2c. Check exit conditions
            CheckExitConditions(today, yesterday, symbolBars, symbolDateIndex);

            // 2d. Check stale positions (max holding days)
            CheckStalePositions(today, symbolBars, symbolDateIndex);

            // 2e. Scan for new entries
            if (!_circuitBreakerActive)
            {
                ScanForEntries(today, yesterday, symbolBars, symbolDateIndex, allDates, dayIdx);
            }

            // 2f. Record equity and evaluate circuit breaker
            RecordEquity(today, symbolBars, symbolDateIndex);
        }

        // 3. Close all at end
        var lastDate = allDates[^1];
        CloseAllPositions(lastDate, symbolBars, symbolDateIndex, "EndOfBacktest");

        return BuildResult(allDates, symbolBars);
    }

    private static string DetectRegimeFromBars(
        Dictionary<string, CandleWithIndicators[]> symbolBars,
        Dictionary<string, Dictionary<DateTime, int>> symbolDateIndex,
        List<DateTime> allDates, int currentDayIdx)
    {
        // Use SPY for regime detection (market-wide, not per-stock); fall back to first symbol only
        var candidates = new[] { "SPY" }.Concat(symbolBars.Keys.Take(1));
        foreach (var symbol in candidates)
        {
            if (!TryGetBar(symbol, allDates[currentDayIdx], symbolBars, symbolDateIndex, out var bar) || bar == null)
                continue;

            var ind = bar.Indicators;
            if (!ind.IsWarmedUp || ind.SmaLong <= 0) continue;

            // Check trend regimes first (most common), HighVol last (prevents false triggers)
            if (ind.SmaMedium > 0 && ind.SmaLong > 0)
            {
                if (ind.SmaMedium > ind.SmaLong && ind.Rsi > 50) return "Bull";
                if (ind.SmaMedium < ind.SmaLong && ind.Rsi < 50) return "Bear";
            }

            // Only flag HighVolatility at higher threshold (3.5% ATR/price)
            if (ind.Atr > 0 && bar.Close > 0)
            {
                var atrPct = ind.Atr / bar.Close * 100m;
                if (atrPct > 3.5m) return "HighVolatility";
            }

            return "Sideways";
        }
        return "";
    }

    private void FillPendingOrders(DateTime today, Dictionary<string, CandleWithIndicators[]> symbolBars,
        Dictionary<string, Dictionary<DateTime, int>> symbolDateIndex)
    {
        var filled = new List<PendingPortfolioOrder>();

        foreach (var order in _pendingOrders)
        {
            if (!TryGetBar(order.Symbol, today, symbolBars, symbolDateIndex, out var bar))
            {
                _log.Add($"{today:yyyy-MM-dd} CANCEL {order.Symbol}: no data");
                filled.Add(order);
                continue;
            }

            var fillPrice = bar!.Open * (1 + _config.SlippagePercent / 100m);
            var entryCost = GetTradeCost(fillPrice, order.Shares, isBuy: true);
            var cost = order.Shares * fillPrice + entryCost;

            if (cost > _cash)
            {
                _log.Add($"{today:yyyy-MM-dd} SKIP BUY {order.Symbol}: insufficient cash ({_cash:C0} < {cost:C0})");
                filled.Add(order);
                continue;
            }

            _cash -= cost;

            var stopLoss = CalculateStopLoss(fillPrice, bar);
            var takeProfit = CalculateTakeProfit(fillPrice, stopLoss);

            _openPositions.Add(new PortfolioPosition
            {
                Symbol = order.Symbol,
                EntryDate = today,
                EntryPrice = fillPrice,
                Shares = order.Shares,
                StopLoss = stopLoss,
                InitialStopLoss = stopLoss,
                HighestPrice = fillPrice,
                TakeProfit = takeProfit,
                CostBasis = cost,
                SignalScore = order.SignalScore,
                ReasoningJson = order.ReasoningJson,
                Regime = order.Regime,
            });

            _log.Add($"{today:yyyy-MM-dd} BUY {order.Shares} {order.Symbol} @ {fillPrice:F2} (stop={stopLoss:F2}, tp={takeProfit:F2}, score={order.SignalScore:F0})");
            filled.Add(order);
        }

        foreach (var f in filled)
            _pendingOrders.Remove(f);
    }

    private void CheckStopsAndTargets(DateTime today, Dictionary<string, CandleWithIndicators[]> symbolBars,
        Dictionary<string, Dictionary<DateTime, int>> symbolDateIndex)
    {
        var closed = new List<PortfolioPosition>();

        foreach (var pos in _openPositions)
        {
            if (!TryGetBar(pos.Symbol, today, symbolBars, symbolDateIndex, out var bar))
                continue;

            if (bar!.Low <= pos.StopLoss)
            {
                ClosePosition(pos, today, pos.StopLoss, "StopLoss");
                closed.Add(pos);
                continue;
            }

            if (pos.TakeProfit > 0 && bar.High >= pos.TakeProfit)
            {
                ClosePosition(pos, today, pos.TakeProfit, "TakeProfit");
                closed.Add(pos);
            }
        }

        foreach (var c in closed)
            _openPositions.Remove(c);
    }

    private void UpdateTrailingStops(DateTime today, Dictionary<string, CandleWithIndicators[]> symbolBars,
        Dictionary<string, Dictionary<DateTime, int>> symbolDateIndex)
    {
        if (!_strategy.StopLoss.UseTrailingStop) return;

        foreach (var pos in _openPositions)
        {
            if (!TryGetBar(pos.Symbol, today, symbolBars, symbolDateIndex, out var bar))
                continue;

            // Update high water mark
            pos.HighestPrice = Math.Max(pos.HighestPrice, bar!.High);

            // Check if trailing stop should activate
            var initialRisk = pos.EntryPrice - pos.InitialStopLoss;
            if (initialRisk <= 0) continue;

            var unrealizedR = (pos.HighestPrice - pos.EntryPrice) / initialRisk;
            if (unrealizedR < _strategy.StopLoss.TrailingActivationR) continue;

            // Compute trailing stop level
            if (bar.Indicators.Atr <= 0) continue;
            var newTrail = pos.HighestPrice - bar.Indicators.Atr * _strategy.StopLoss.TrailingAtrMultiplier;

            // Only ratchet up, never down
            if (newTrail > pos.StopLoss)
            {
                _log.Add($"{today:yyyy-MM-dd} TRAIL {pos.Symbol}: SL {pos.StopLoss:F2} → {newTrail:F2} (high={pos.HighestPrice:F2}, R={unrealizedR:F1})");
                pos.StopLoss = newTrail;
            }
        }
    }

    private void ApplyTimeDecay(DateTime today)
    {
        if (!_strategy.StopLoss.UseTimeDecay) return;

        foreach (var pos in _openPositions)
        {
            var daysHeld = (today - pos.EntryDate).Days;
            if (daysHeld < _strategy.StopLoss.TimeDecayStartDays) continue;

            var originalRisk = pos.EntryPrice - pos.InitialStopLoss;
            if (originalRisk <= 0) continue;

            var tightenedSl = pos.EntryPrice - originalRisk * (_strategy.StopLoss.TimeDecayTightenPercent / 100m);

            // Only ratchet up, never down
            if (tightenedSl > pos.StopLoss)
            {
                _log.Add($"{today:yyyy-MM-dd} TIME-DECAY {pos.Symbol}: SL {pos.StopLoss:F2} → {tightenedSl:F2} (day {daysHeld})");
                pos.StopLoss = tightenedSl;
            }
        }
    }

    private void CheckExitConditions(DateTime today, DateTime yesterday,
        Dictionary<string, CandleWithIndicators[]> symbolBars,
        Dictionary<string, Dictionary<DateTime, int>> symbolDateIndex)
    {
        if (_strategy.ExitConditions.Count == 0 || _openPositions.Count == 0)
            return;

        var toClose = new List<PortfolioPosition>();

        foreach (var pos in _openPositions)
        {
            if (!TryGetBar(pos.Symbol, today, symbolBars, symbolDateIndex, out var bar))
                continue;
            TryGetBar(pos.Symbol, yesterday, symbolBars, symbolDateIndex, out var prevBar);

            if (ConditionEvaluator.Evaluate(_strategy.ExitConditions, bar!, prevBar))
            {
                ClosePosition(pos, today, bar!.Close, "ExitSignal");
                toClose.Add(pos);
            }
        }

        foreach (var c in toClose)
            _openPositions.Remove(c);
    }

    private void ScanForEntries(DateTime today, DateTime yesterday,
        Dictionary<string, CandleWithIndicators[]> symbolBars,
        Dictionary<string, Dictionary<DateTime, int>> symbolDateIndex,
        List<DateTime>? allDates = null, int dayIdx = 0)
    {
        var slotsAvailable = _maxPositions - _openPositions.Count - _pendingOrders.Count;
        if (slotsAvailable <= 0) return;

        var heldSymbols = new HashSet<string>(_openPositions.Select(p => p.Symbol));
        heldSymbols.UnionWith(_pendingOrders.Select(o => o.Symbol));

        // Portfolio heat check
        var equity = GetPortfolioEquity(today, symbolBars, symbolDateIndex);
        var totalRisk = _openPositions.Sum(p => p.RiskAmount);
        var heatPercent = equity > 0 ? totalRisk / equity * 100m : 0;
        if (heatPercent >= _strategy.PositionSizing.MaxPortfolioHeat)
        {
            _log.Add($"{today:yyyy-MM-dd} SKIP SCAN: portfolio heat {heatPercent:F1}% >= {_strategy.PositionSizing.MaxPortfolioHeat}%");
            return;
        }

        var candidates = new List<(string Symbol, decimal Score, CandleWithIndicators Bar, List<string>? Conditions)>();

        foreach (var (symbol, bars) in symbolBars)
        {
            if (heldSymbols.Contains(symbol)) continue;
            if (!TryGetBar(symbol, today, symbolBars, symbolDateIndex, out var bar)) continue;
            if (!bar!.Indicators.IsWarmedUp) continue; // Skip bars with zero indicators
            TryGetBar(symbol, yesterday, symbolBars, symbolDateIndex, out var prevBar);

            // Use EvaluateWithDetails for reasoning
            var (passed, matchedConditions) = ConditionEvaluator.EvaluateWithDetails(_strategy.EntryConditions, bar!, prevBar);
            if (!passed) continue;

            // Apply trade filters
            if (_strategy.Filters.MinVolume.HasValue && bar!.Volume < _strategy.Filters.MinVolume.Value) continue;
            if (_strategy.Filters.MinPrice.HasValue && bar!.Close < _strategy.Filters.MinPrice.Value) continue;
            if (_strategy.Filters.MaxPrice.HasValue && bar!.Close > _strategy.Filters.MaxPrice.Value) continue;

            // Weekly trend alignment filter (Step 3.2)
            if (_strategy.PositionSizing.RequireWeeklyTrendAlignment && allDates != null && dayIdx > 0)
            {
                if (!IsWeeklyTrendAligned(symbol, symbolBars, symbolDateIndex, allDates, dayIdx))
                {
                    _log.Add($"{today:yyyy-MM-dd} SKIP {symbol}: weekly trend not aligned");
                    continue;
                }
            }

            var score = ComputeSignalScore(bar!);
            candidates.Add((symbol, score, bar!, matchedConditions));
        }

        if (candidates.Count == 0) return;

        var topSignals = candidates.OrderByDescending(c => c.Score).Take(slotsAvailable);

        foreach (var (symbol, score, bar, matchedConditions) in topSignals)
        {
            var stopLoss = CalculateStopLoss(bar.Close, bar);
            var riskPerShare = bar.Close - stopLoss;
            if (riskPerShare <= 0) continue;

            // Risk-based sizing with signal score multiplier (Step 1.4)
            var riskPercent = CalculateRiskPercent(equity, heatPercent);
            var riskDollars = equity * riskPercent / 100m;
            var scoreMultiplier = 0.5m + (score / 100m) * 0.5m; // 50%-100% of base
            var adjustedRisk = riskDollars * scoreMultiplier;

            int shares;
            if (_strategy.PositionSizing.UseVolTargeting && bar.Indicators.Atr > 0)
            {
                shares = VolatilityTargeting.CalculateShares(
                    adjustedRisk, bar.Indicators.Atr, _strategy.PositionSizing.VolTargetAtrMultiplier);
            }
            else
            {
                shares = (int)(adjustedRisk / riskPerShare);
            }

            if (shares <= 0) continue;

            // Correlation filter
            if (_strategy.PositionSizing.UseCorrelationFilter && _correlationData != null)
            {
                var openSymbols = _openPositions.Select(p => p.Symbol).Distinct().ToList();
                var corrCheck = CorrelationFilter.Check(
                    symbol, openSymbols, _correlationData,
                    _strategy.PositionSizing.CorrelationBlockThreshold,
                    _strategy.PositionSizing.CorrelationReduceThreshold);

                if (corrCheck.Action == CorrelationAction.Block)
                {
                    _log.Add($"{today:yyyy-MM-dd} SKIP {symbol}: correlation block (avg={corrCheck.AvgCorrelation:F4})");
                    continue;
                }
                if (corrCheck.Action == CorrelationAction.Reduce)
                {
                    shares = CorrelationFilter.AdjustShares(shares, corrCheck);
                    if (shares <= 0) continue;
                }
            }

            // Geographic risk budget
            if (_strategy.PositionSizing.UseGeographicRiskBudget && _symbolToMarket != null)
            {
                var posNotionals = _openPositions.Select(p => (p.Symbol, Notional: p.Shares * bar.Close)).ToList();
                var marketNotionals = GeographicRiskBudget.ComputeMarketNotionals(posNotionals, _symbolToMarket);
                var candidateMarket = _symbolToMarket.GetValueOrDefault(symbol, "UNKNOWN");
                var proposedNotional = shares * bar.Close;
                var geoCheck = GeographicRiskBudget.Check(
                    candidateMarket, proposedNotional, marketNotionals, equity,
                    _strategy.PositionSizing.MaxMarketAllocationPercent);

                if (!geoCheck.Allowed)
                {
                    _log.Add($"{today:yyyy-MM-dd} SKIP {symbol}: geographic budget exceeded for {candidateMarket}");
                    continue;
                }
            }

            // Cash check
            var estCommission = GetTradeCost(bar.Close, shares, isBuy: true);
            var estimatedCost = shares * bar.Close * (1 + _config.SlippagePercent / 100m) + estCommission;
            if (estimatedCost > _cash)
            {
                var costPerShare = bar.Close * (1 + _config.SlippagePercent / 100m);
                var availableForShares = _cash - GetTradeCost(bar.Close, 1, isBuy: true);
                shares = (int)(availableForShares / costPerShare);
                if (shares <= 0) continue;
            }

            // Build trade reasoning
            var reasoning = new TradeReasoning
            {
                ConditionsFired = matchedConditions ?? new List<string>(),
                CompositeScore = score,
                FactorContributions = BuildFactorContributions(bar),
                Regime = _currentRegime,
                RegimeConfidence = _currentRegimeConfidence,
            };
            var reasoningJson = JsonSerializer.Serialize(reasoning, CamelCase);

            _pendingOrders.Add(new PendingPortfolioOrder
            {
                Symbol = symbol,
                Shares = shares,
                SignalDate = today,
                SignalScore = score,
                ReasoningJson = reasoningJson,
                Regime = _currentRegime,
            });

            _log.Add($"{today:yyyy-MM-dd} SIGNAL BUY {shares} {symbol} (score={score:F0}, regime={_currentRegime})");
        }
    }

    private void CheckStalePositions(DateTime today, Dictionary<string, CandleWithIndicators[]> symbolBars,
        Dictionary<string, Dictionary<DateTime, int>> symbolDateIndex)
    {
        var maxDays = _strategy.PositionSizing.MaxHoldingDays;
        if (maxDays <= 0) return;

        var stale = _openPositions
            .Where(p => (today - p.EntryDate).Days >= maxDays)
            .ToList();

        foreach (var pos in stale)
        {
            decimal exitPrice;
            if (TryGetBar(pos.Symbol, today, symbolBars, symbolDateIndex, out var bar))
                exitPrice = bar!.Close;
            else
                exitPrice = pos.EntryPrice;

            ClosePosition(pos, today, exitPrice, "MaxHoldingDays");
            _openPositions.Remove(pos);
            _log.Add($"{today:yyyy-MM-dd} STALE EXIT {pos.Symbol}: held {(today - pos.EntryDate).Days} days >= {maxDays}");
        }
    }

    /// <summary>
    /// Checks weekly trend alignment by computing SMA50 and SMA200 from daily bars.
    /// Weekly SMA50 ≈ last 250 daily closes averaged over 50-bar windows.
    /// For simplicity, we use daily SMA50 > SMA200 as a proxy for weekly trend.
    /// </summary>
    private static bool IsWeeklyTrendAligned(string symbol,
        Dictionary<string, CandleWithIndicators[]> symbolBars,
        Dictionary<string, Dictionary<DateTime, int>> symbolDateIndex,
        List<DateTime> allDates, int currentDayIdx)
    {
        // Use the daily SMA values as proxy: SMA50 > SMA200 = uptrend
        if (!TryGetBar(symbol, allDates[currentDayIdx], symbolBars, symbolDateIndex, out var bar) || bar == null)
            return true; // allow if no data

        var ind = bar.Indicators;
        if (ind.SmaLong <= 0) return true; // no long SMA data, skip filter

        // For weekly trend alignment, check that medium-term (SMA50/SmaMedium) is above long-term (SMA200/SmaLong)
        if (ind.SmaMedium > 0 && ind.SmaLong > 0)
            return ind.SmaMedium > ind.SmaLong;

        return true; // allow if insufficient indicator data
    }

    private decimal CalculateRiskPercent(decimal equity, decimal currentHeatPercent)
    {
        var sizing = _strategy.PositionSizing;

        if (!string.Equals(sizing.SizingMethod, "Kelly", StringComparison.OrdinalIgnoreCase))
            return sizing.RiskPercent;

        var tradePnls = _trades.Select(t => t.PnL).ToList();

        var result = KellyCriterion.CalculatePositionRisk(
            tradePnls, equity, currentHeatPercent,
            sizing.MaxPortfolioHeat,
            fixedRiskPercent: sizing.RiskPercent,
            kellyMultiplier: sizing.KellyMultiplier,
            windowSize: sizing.KellyWindowSize);

        return result.RiskPercent;
    }

    private static decimal ComputeSignalScore(CandleWithIndicators bar)
    {
        var score = 0m;
        var rsi = bar.Indicators.Rsi;
        if (rsi > 0) score += Math.Abs(rsi - 50m) / 50m * 40m;
        if (bar.Indicators.RelativeVolume > 0)
            score += Math.Min(bar.Indicators.RelativeVolume * 10m, 30m);
        if (bar.Indicators.Atr > 0 && bar.Close > 0)
        {
            var atrPercent = bar.Indicators.Atr / bar.Close * 100m;
            score += Math.Min(atrPercent * 5m, 30m);
        }
        return score;
    }

    private static Dictionary<string, decimal> BuildFactorContributions(CandleWithIndicators bar)
    {
        var factors = new Dictionary<string, decimal>();
        var rsi = bar.Indicators.Rsi;
        if (rsi > 0) factors["RSI"] = Math.Abs(rsi - 50m) / 50m * 40m;
        if (bar.Indicators.RelativeVolume > 0)
            factors["Volume"] = Math.Min(bar.Indicators.RelativeVolume * 10m, 30m);
        if (bar.Indicators.Atr > 0 && bar.Close > 0)
            factors["ATR"] = Math.Min(bar.Indicators.Atr / bar.Close * 100m * 5m, 30m);
        return factors;
    }

    private void RecordEquity(DateTime date, Dictionary<string, CandleWithIndicators[]> symbolBars,
        Dictionary<string, Dictionary<DateTime, int>> symbolDateIndex)
    {
        var equity = GetPortfolioEquity(date, symbolBars, symbolDateIndex);
        _equityCurve.Add(new EquityPoint(date, equity));

        if (equity > _peakEquity)
            _peakEquity = equity;

        if (_openPositions.Count > _maxPositionsHeld)
            _maxPositionsHeld = _openPositions.Count;

        _totalPositionDays += _openPositions.Count;
        _totalDays++;

        // Circuit breaker evaluation
        var eval = CircuitBreaker.Evaluate(
            equity, _peakEquity, _circuitBreakerActive,
            _strategy.PositionSizing.MaxDrawdownPercent,
            _strategy.PositionSizing.DrawdownRecoveryPercent,
            _currentRegime, _currentRegimeConfidence);

        if (eval.ShouldActivate)
        {
            _circuitBreakerActive = true;
            var cancelled = CancelPendingOrders();
            _log.Add($"{date:yyyy-MM-dd} CIRCUIT BREAKER ACTIVATED: {eval.Detail}");
            if (cancelled > 0)
                _log.Add($"{date:yyyy-MM-dd} CANCELLED {cancelled} pending order(s)");
        }
        else if (eval.ShouldDeactivate)
        {
            _circuitBreakerActive = false;
            _log.Add($"{date:yyyy-MM-dd} CIRCUIT BREAKER DEACTIVATED: {eval.Detail}");
        }
    }

    private int CancelPendingOrders()
    {
        var count = _pendingOrders.Count;
        _pendingOrders.Clear();
        return count;
    }

    private decimal GetPortfolioEquity(DateTime date, Dictionary<string, CandleWithIndicators[]> symbolBars,
        Dictionary<string, Dictionary<DateTime, int>> symbolDateIndex)
    {
        var positionsValue = 0m;
        foreach (var pos in _openPositions)
        {
            if (TryGetBar(pos.Symbol, date, symbolBars, symbolDateIndex, out var bar))
                positionsValue += pos.Shares * bar!.Close;
            else
                positionsValue += pos.Shares * pos.EntryPrice;
        }
        return _cash + positionsValue;
    }

    private void ClosePosition(PortfolioPosition pos, DateTime exitDate, decimal exitPrice, string reason)
    {
        // Apply sell-side slippage
        var slippedExitPrice = exitPrice * (1 - _config.SlippagePercent / 100m);

        var exitCost = GetTradeCost(slippedExitPrice, pos.Shares, isBuy: false);
        var entryCost = GetTradeCost(pos.EntryPrice, pos.Shares, isBuy: true);
        var proceeds = pos.Shares * slippedExitPrice - exitCost;
        _cash += proceeds;

        var totalCost = entryCost + exitCost;
        var pnl = pos.Shares * (slippedExitPrice - pos.EntryPrice) - totalCost;
        var pnlPercent = pos.EntryPrice != 0 ? (slippedExitPrice - pos.EntryPrice) / pos.EntryPrice * 100m : 0;

        _trades.Add(new TradeRecord
        {
            Symbol = pos.Symbol,
            EntryDate = pos.EntryDate,
            EntryPrice = pos.EntryPrice,
            ExitDate = exitDate,
            ExitPrice = slippedExitPrice,
            Shares = pos.Shares,
            PnL = pnl,
            PnLPercent = pnlPercent,
            Commission = totalCost,
            HoldingDays = (exitDate - pos.EntryDate).Days,
            ExitReason = reason,
            StopLossPrice = pos.StopLoss,
            InitialStopLoss = pos.InitialStopLoss,
            TakeProfitPrice = pos.TakeProfit,
            SignalScore = pos.SignalScore,
            ReasoningJson = pos.ReasoningJson,
            Regime = pos.Regime,
        });

        _log.Add($"{exitDate:yyyy-MM-dd} SELL {pos.Shares} {pos.Symbol} @ {slippedExitPrice:F2} ({reason}) PnL={pnl:F2}");
    }

    private void CloseAllPositions(DateTime lastDate, Dictionary<string, CandleWithIndicators[]> symbolBars,
        Dictionary<string, Dictionary<DateTime, int>> symbolDateIndex, string reason)
    {
        foreach (var pos in _openPositions.ToList())
        {
            decimal exitPrice;
            if (TryGetBar(pos.Symbol, lastDate, symbolBars, symbolDateIndex, out var bar))
                exitPrice = bar!.Close;
            else
                exitPrice = pos.EntryPrice;

            ClosePosition(pos, lastDate, exitPrice, reason);
        }
        _openPositions.Clear();
    }

    private decimal GetTradeCost(decimal price, int shares, bool isBuy)
    {
        if (_costProfile != null)
            return MarketCostCalculator.EstimateTradeCost(price, shares, _costProfile, isBuy);
        return _config.CommissionPerTrade;
    }

    private decimal CalculateStopLoss(decimal price, CandleWithIndicators bar)
    {
        var rawSl = _strategy.StopLoss.Type switch
        {
            "Atr" when bar.Indicators.Atr > 0 =>
                price - bar.Indicators.Atr * _strategy.StopLoss.Multiplier,
            "FixedPercent" =>
                price * (1 - _strategy.StopLoss.Multiplier / 100m),
            _ => price * 0.95m
        };

        // Cap SL so we never risk more than MaxStopLossPercent per trade
        var maxSlPrice = price * (1 - _strategy.StopLoss.MaxStopLossPercent / 100m);
        return Math.Max(rawSl, maxSlPrice);
    }

    private decimal CalculateTakeProfit(decimal price, decimal stopLoss)
    {
        var risk = price - stopLoss;
        return _strategy.TakeProfit.Type switch
        {
            "RMultiple" => price + risk * _strategy.TakeProfit.Multiplier,
            "FixedPercent" => price * (1 + _strategy.TakeProfit.Multiplier / 100m),
            _ => 0m
        };
    }

    private static bool TryGetBar(string symbol, DateTime date,
        Dictionary<string, CandleWithIndicators[]> symbolBars,
        Dictionary<string, Dictionary<DateTime, int>> symbolDateIndex,
        out CandleWithIndicators? bar)
    {
        bar = null;
        if (!symbolDateIndex.TryGetValue(symbol, out var dateIdx)) return false;
        if (!dateIdx.TryGetValue(date, out var idx)) return false;
        bar = symbolBars[symbol][idx];
        return true;
    }

    private BacktestEngineResult BuildResult(List<DateTime> allDates, Dictionary<string, CandleWithIndicators[]> symbolBars)
    {
        var uniqueSymbols = _trades.Select(t => t.Symbol).Distinct().Count();
        var avgPositions = _totalDays > 0 ? (decimal)_totalPositionDays / _totalDays : 0m;
        var finalEquity = _equityCurve.Count > 0 ? _equityCurve[^1].Value : _initialCapital;

        var breakdowns = _trades
            .GroupBy(t => t.Symbol)
            .ToDictionary(g => g.Key, g =>
            {
                var trades = g.ToList();
                var wins = trades.Count(t => t.PnL > 0);
                return new SymbolBreakdown
                {
                    Symbol = g.Key,
                    Trades = trades.Count,
                    Wins = wins,
                    WinRate = trades.Count > 0 ? (decimal)wins / trades.Count * 100m : 0m,
                    TotalPnL = trades.Sum(t => t.PnL),
                    AvgPnLPercent = trades.Count > 0 ? trades.Average(t => t.PnLPercent) : 0m,
                    AvgHoldingDays = trades.Count > 0 ? (decimal)trades.Average(t => t.HoldingDays) : 0m
                };
            });

        // Determine symbol name for single-symbol runs
        var symbol = symbolBars.Count == 1 ? symbolBars.Keys.First() : string.Join(",", symbolBars.Keys.Take(5));

        return new BacktestEngineResult
        {
            Symbol = symbol,
            StartDate = allDates.Count > 0 ? allDates[0] : DateTime.MinValue,
            EndDate = allDates.Count > 0 ? allDates[^1] : DateTime.MinValue,
            InitialCapital = _initialCapital,
            FinalEquity = finalEquity,
            Trades = _trades,
            EquityCurve = _equityCurve,
            Log = _log,
            UniqueSymbolsTraded = uniqueSymbols,
            AveragePositionsHeld = Math.Round(avgPositions, 2),
            MaxPositionsHeld = _maxPositionsHeld,
            SymbolBreakdowns = breakdowns,
            RegimeTimeline = _regimeTimeline,
        };
    }

    private BacktestEngineResult EmptyResult(List<DateTime> allDates)
    {
        return new BacktestEngineResult
        {
            InitialCapital = _initialCapital,
            FinalEquity = _initialCapital,
            StartDate = allDates.Count > 0 ? allDates[0] : DateTime.MinValue,
            EndDate = allDates.Count > 0 ? allDates[^1] : DateTime.MinValue,
            Log = new List<string> { "Insufficient data for backtest (need at least 2 bars)" }
        };
    }

    private class PortfolioPosition
    {
        public string Symbol { get; init; } = string.Empty;
        public DateTime EntryDate { get; init; }
        public decimal EntryPrice { get; init; }
        public int Shares { get; init; }
        public decimal StopLoss { get; set; }
        public decimal InitialStopLoss { get; init; }
        public decimal HighestPrice { get; set; }
        public decimal TakeProfit { get; init; }
        public decimal CostBasis { get; init; }
        public decimal SignalScore { get; init; }
        public string? ReasoningJson { get; init; }
        public string? Regime { get; init; }
        public decimal RiskAmount => Shares * (EntryPrice - StopLoss);
    }

    private class PendingPortfolioOrder
    {
        public string Symbol { get; init; } = string.Empty;
        public int Shares { get; init; }
        public DateTime SignalDate { get; init; }
        public decimal SignalScore { get; init; }
        public string? ReasoningJson { get; init; }
        public string? Regime { get; init; }
    }
}
