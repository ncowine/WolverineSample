using TradingAssistant.Application.Backtesting;
using TradingAssistant.Application.Indicators;
using TradingAssistant.Contracts.Backtesting;

namespace TradingAssistant.Application.Screening;

/// <summary>
/// Scans a universe of symbols, evaluates entry conditions, grades signals,
/// and returns ranked trade opportunities.
/// </summary>
public static class ScreenerEngine
{
    /// <summary>
    /// Run a screener scan across all provided symbols.
    /// </summary>
    /// <param name="symbolData">
    /// Per-symbol data: symbol → array of recent daily bars with indicators (most recent last).
    /// Must have at least 50 bars for proper evaluation.
    /// </param>
    /// <param name="strategy">Strategy definition with entry conditions and risk config.</param>
    /// <param name="config">Screener configuration (filters, limits).</param>
    /// <param name="historyTracker">Optional grade history tracker for accuracy tracking.</param>
    /// <param name="winRateBySymbol">Optional per-symbol historical win rates.</param>
    public static ScreenerRunResult Scan(
        Dictionary<string, CandleWithIndicators[]> symbolData,
        StrategyDefinition strategy,
        ScreenerConfig? config = null,
        GradeHistoryTracker? historyTracker = null,
        Dictionary<string, decimal>? winRateBySymbol = null)
    {
        config ??= new ScreenerConfig();
        var startTime = DateTime.UtcNow;
        var warnings = new List<string>();
        var allSignals = new List<ScreenerResult>();

        foreach (var (symbol, bars) in symbolData)
        {
            if (bars.Length < 2)
            {
                warnings.Add($"{symbol}: insufficient data ({bars.Length} bars)");
                continue;
            }

            var lastBar = bars[^1];
            var prevBar = bars[^2];

            // Apply trade filters
            if (!PassesFilters(lastBar, strategy.Filters, config))
                continue;

            // Evaluate entry conditions
            if (!ConditionEvaluator.Evaluate(strategy.EntryConditions, lastBar, prevBar))
                continue;

            // Compute stop/target
            var entryPrice = lastBar.Close;
            var stopPrice = CalculateStopLoss(entryPrice, lastBar, strategy.StopLoss);
            var targetPrice = CalculateTakeProfit(entryPrice, stopPrice, strategy.TakeProfit);

            if (stopPrice >= entryPrice)
            {
                warnings.Add($"{symbol}: invalid stop (stop {stopPrice:F2} >= entry {entryPrice:F2})");
                continue;
            }

            // Grab recent bars for signal evaluation (up to 50)
            var lookback = Math.Min(bars.Length, 50);
            var recentBars = bars[^lookback..];

            // Evaluate confirmations
            var evaluation = SignalEvaluator.Evaluate(
                symbol, lastBar.Timestamp, SignalDirection.Long, lastBar, recentBars);

            // Grade the signal
            var winRate = winRateBySymbol?.GetValueOrDefault(symbol) ?? config.DefaultWinRate;
            var report = ConfidenceGrader.Grade(evaluation, entryPrice, stopPrice, targetPrice, winRate);

            allSignals.Add(new ScreenerResult
            {
                Symbol = symbol,
                Grade = report.Grade,
                Score = report.Score,
                Direction = report.Direction,
                EntryPrice = entryPrice,
                StopPrice = stopPrice,
                TargetPrice = targetPrice,
                RiskRewardRatio = report.RiskRewardRatio,
                Breakdown = report.Breakdown,
                HistoricalWinRate = winRate,
                SignalDate = lastBar.Timestamp
            });

            // Track in history if provided
            historyTracker?.Record(new GradeHistoryEntry
            {
                Symbol = symbol,
                SignalDate = lastBar.Timestamp,
                Direction = SignalDirection.Long,
                Grade = report.Grade,
                Score = report.Score,
                EntryPrice = entryPrice,
                StopPrice = stopPrice,
                TargetPrice = targetPrice
            });
        }

        // Filter by minimum grade and sort by score descending
        var filtered = allSignals
            .Where(r => r.Grade <= config.MinGrade) // A=0, B=1 ... F=4; lower enum = better grade
            .OrderByDescending(r => r.Score)
            .Take(config.MaxSignals)
            .ToList();

        return new ScreenerRunResult
        {
            ScanDate = symbolData.Values
                .Where(b => b.Length > 0 && b[^1] is not null)
                .Select(b => b[^1].Timestamp)
                .DefaultIfEmpty(DateTime.UtcNow)
                .Max(),
            StrategyName = "Screener",
            SymbolsScanned = symbolData.Count,
            SignalsFound = allSignals.Count,
            SignalsPassingFilter = filtered.Count,
            Results = filtered,
            Warnings = warnings,
            ElapsedTime = DateTime.UtcNow - startTime
        };
    }

    // ── Price Calculation (mirrors BacktestEngine logic) ─────

    internal static decimal CalculateStopLoss(decimal price, CandleWithIndicators bar, StopLossConfig config)
    {
        return config.Type switch
        {
            "Atr" when bar.Indicators.Atr > 0 => price - bar.Indicators.Atr * config.Multiplier,
            "FixedPercent" => price * (1 - config.Multiplier / 100m),
            _ => price * 0.95m // fallback 5%
        };
    }

    internal static decimal CalculateTakeProfit(decimal price, decimal stopLoss, TakeProfitConfig config)
    {
        var risk = price - stopLoss;
        return config.Type switch
        {
            "RMultiple" => price + risk * config.Multiplier,
            "FixedPercent" => price * (1 + config.Multiplier / 100m),
            _ => 0m
        };
    }

    private static bool PassesFilters(
        CandleWithIndicators bar, TradeFilterConfig filters, ScreenerConfig config)
    {
        // Strategy-level filters
        if (filters.MinVolume.HasValue && bar.Volume < filters.MinVolume.Value)
            return false;
        if (filters.MinPrice.HasValue && bar.Close < filters.MinPrice.Value)
            return false;
        if (filters.MaxPrice.HasValue && bar.Close > filters.MaxPrice.Value)
            return false;

        // Screener-level volume filter
        if (config.MinVolume.HasValue && bar.Volume < config.MinVolume.Value)
            return false;

        return true;
    }
}
