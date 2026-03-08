using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingAssistant.Application.Backtesting;
using TradingAssistant.Application.Indicators;
using TradingAssistant.Application.Intelligence;
using TradingAssistant.Contracts.Backtesting;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.MarketData;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Domain.MarketData;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Backtesting;

/// <summary>
/// Runs walk-forward optimization: grid-searches parameter space across rolling windows,
/// validates on out-of-sample data, persists "blessed" parameters.
/// </summary>
public class RunOptimizationHandler
{
    // Map from user-facing parameter names → IndicatorConfig property names
    private static readonly HashSet<string> IndicatorPeriodParams = new(StringComparer.OrdinalIgnoreCase)
    {
        "RsiPeriod", "EmaShortPeriod", "EmaMediumPeriod", "EmaLongPeriod",
        "SmaShortPeriod", "SmaMediumPeriod", "SmaLongPeriod",
        "MacdFastPeriod", "MacdSlowPeriod", "MacdSignalPeriod",
        "AtrPeriod", "BollingerPeriod", "StochasticKPeriod", "StochasticDPeriod",
        "VolumeMaPeriod",
    };

    public static async Task<OptimizationResultDto> HandleAsync(
        RunOptimizationCommand command,
        BacktestDbContext db,
        MarketDataDbContext marketDb,
        IMarketDataProvider marketDataProvider,
        ILoggerFactory loggerFactory,
        ILogger<RunOptimizationHandler> logger)
    {
        // 1. Load strategy
        var strategy = await db.Strategies
            .Include(s => s.Rules)
            .FirstOrDefaultAsync(s => s.Id == command.StrategyId)
            ?? throw new InvalidOperationException($"Strategy '{command.StrategyId}' not found.");

        if (!strategy.UsesV2Engine)
            throw new InvalidOperationException("Optimization requires a V2 strategy with RulesJson.");

        var baseDefinition = JsonSerializer.Deserialize<StrategyDefinition>(strategy.RulesJson!,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Failed to deserialize strategy definition.");

        baseDefinition.PositionSizing.MaxPositions = command.MaxPositions;

        // 2. Build parameter space from command
        if (command.ParameterRanges.Count == 0)
            throw new InvalidOperationException("At least one parameter range is required for optimization.");

        var parameterSpace = new ParameterSpace
        {
            Parameters = command.ParameterRanges.Select(r => new ParameterDefinition
            {
                Name = r.Name,
                Min = r.Min,
                Max = r.Max,
                Step = r.Step,
            }).ToList()
        };

        logger.LogInformation(
            "[Optimization] Starting for strategy '{Name}', {Symbol}, {Combos} combinations",
            strategy.Name, command.Symbol, parameterSpace.TotalCombinations);

        // 3. Auto-ingest missing data
        var symbol = command.Symbol.Trim().ToUpperInvariant();
        var ingestLogger = loggerFactory.CreateLogger<Handlers.MarketData.IngestMarketDataHandler>();
        var yearsBack = Math.Max(5, (int)Math.Ceiling((DateTime.UtcNow - command.StartDate).TotalDays / 365.25));

        var stock = await marketDb.Stocks.FirstOrDefaultAsync(s => s.Symbol == symbol);
        var needsData = stock is null;
        if (!needsData)
        {
            var dailyCount = await marketDb.PriceCandles
                .CountAsync(c => c.StockId == stock!.Id
                    && c.Timestamp >= command.StartDate
                    && c.Timestamp <= command.EndDate
                    && c.Interval == CandleInterval.Daily);
            needsData = dailyCount < 20;
        }

        if (needsData)
        {
            var pRunId = AutoOptimizeProgressStore.ActiveRunId;
            if (pRunId.HasValue)
                AutoOptimizeProgressStore.Update(pRunId.Value, "Fetching market data", 0, 0, 0, 0);

            await Handlers.MarketData.IngestMarketDataHandler.HandleAsync(
                new IngestMarketDataCommand(symbol, yearsBack),
                marketDataProvider, marketDb, ingestLogger);
        }

        // 4. Load raw candles (we'll recompute indicators per parameter set)
        stock = await marketDb.Stocks.FirstOrDefaultAsync(s => s.Symbol == symbol)
            ?? throw new InvalidOperationException($"No market data for {symbol}.");

        var dailyCandles = await marketDb.PriceCandles
            .Where(c => c.StockId == stock.Id && c.Timestamp >= command.StartDate
                && c.Timestamp <= command.EndDate && c.Interval == CandleInterval.Daily)
            .OrderBy(c => c.Timestamp).ToListAsync();

        if (dailyCandles.Count < 100)
            throw new InvalidOperationException($"Insufficient data for optimization: {dailyCandles.Count} bars (need >= 100).");

        var weeklyCandles = await marketDb.PriceCandles
            .Where(c => c.StockId == stock.Id && c.Timestamp >= command.StartDate
                && c.Timestamp <= command.EndDate && c.Interval == CandleInterval.Weekly)
            .OrderBy(c => c.Timestamp).ToListAsync();

        var monthlyCandles = await marketDb.PriceCandles
            .Where(c => c.StockId == stock.Id && c.Timestamp >= command.StartDate
                && c.Timestamp <= command.EndDate && c.Interval == CandleInterval.Monthly)
            .OrderBy(c => c.Timestamp).ToListAsync();

        // Determine which params are indicator periods vs strategy thresholds
        var hasIndicatorParams = command.ParameterRanges.Any(r => IndicatorPeriodParams.Contains(r.Name));
        var costProfile = CostProfileData.ForMarket(command.CostProfileMarket);

        // 5. Pre-compute baseline indicators (used when no indicator params need changing)
        CandleWithIndicators[]? baselineBars = null;
        if (!hasIndicatorParams)
        {
            var candlesByTimeframe = BuildCandlesByTimeframe(dailyCandles, weeklyCandles, monthlyCandles);
            var multiTf = IndicatorOrchestrator.Compute(candlesByTimeframe);
            baselineBars = multiTf.AlignedDaily;
        }

        // 6. Build backtest runner factory for WalkForwardAnalyzer
        CandleWithIndicators[] backtestRunnerFactory(ParameterSet paramSet, CandleWithIndicators[] barSlice)
        {
            // This is a two-level factory: the outer closure recomputes indicators when
            // indicator params change; the inner runner mutates strategy thresholds.
            return barSlice; // placeholder, actual logic below
        }

        // The actual factory that WalkForwardAnalyzer calls
        BacktestEngineResult RunBacktestWithParams(ParameterSet paramSet, CandleWithIndicators[] barSlice)
        {
            // Clone strategy definition so we can mutate thresholds
            var definitionJson = JsonSerializer.Serialize(baseDefinition,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var definition = JsonSerializer.Deserialize<StrategyDefinition>(definitionJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            definition.PositionSizing.MaxPositions = command.MaxPositions;

            // Apply strategy threshold params to conditions
            ApplyThresholdParams(definition, paramSet);

            // Run engine
            var symbolBars = new Dictionary<string, CandleWithIndicators[]> { [symbol] = barSlice };
            var engine = new PortfolioBacktestEngine(definition, command.MaxPositions, command.InitialCapital);
            engine.SetCostProfile(costProfile);
            return engine.Run(symbolBars);
        }

        // If we have indicator params, we need to recompute indicators per param set
        // Wrap the factory to handle this
        BacktestEngineResult FullFactory(ParameterSet paramSet, CandleWithIndicators[] barSlice)
        {
            if (hasIndicatorParams)
            {
                // Build custom IndicatorConfig from param set
                var config = BuildIndicatorConfig(paramSet);

                // Recompute indicators on the FULL bar set, then slice
                var candlesByTimeframe = BuildCandlesByTimeframe(dailyCandles, weeklyCandles, monthlyCandles);
                var multiTf = IndicatorOrchestrator.Compute(candlesByTimeframe, config);
                var fullBars = multiTf.AlignedDaily;

                // Map barSlice indices: find matching date range in fullBars
                if (barSlice.Length >= 2)
                {
                    var sliceStart = barSlice[0].Timestamp;
                    var sliceEnd = barSlice[^1].Timestamp;
                    var recomputedSlice = fullBars
                        .Where(b => b.Timestamp >= sliceStart && b.Timestamp <= sliceEnd)
                        .ToArray();

                    if (recomputedSlice.Length >= 2)
                        return RunBacktestWithParams(paramSet, recomputedSlice);
                }

                // Fallback: use the slice as-is
                return RunBacktestWithParams(paramSet, barSlice);
            }

            return RunBacktestWithParams(paramSet, barSlice);
        }

        // 7. Compute baseline bars for walk-forward window generation
        var barsForWindows = baselineBars;
        if (barsForWindows is null)
        {
            var candlesByTimeframe = BuildCandlesByTimeframe(dailyCandles, weeklyCandles, monthlyCandles);
            var multiTf = IndicatorOrchestrator.Compute(candlesByTimeframe);
            barsForWindows = multiTf.AlignedDaily;
        }

        if (barsForWindows.Length < 100)
            throw new InvalidOperationException($"Insufficient computed bars for optimization: {barsForWindows.Length}.");

        // 8. Run walk-forward analysis
        var expectedWindows = Math.Max(0, (barsForWindows.Length - 504) / 126);
        logger.LogInformation("[Optimization] Running walk-forward with {Bars} bars, {Windows} expected windows",
            barsForWindows.Length, expectedWindows);

        {
            var pRunId2 = AutoOptimizeProgressStore.ActiveRunId;
            if (pRunId2.HasValue)
                AutoOptimizeProgressStore.Update(pRunId2.Value, "Running walk-forward optimization",
                    0, (int)expectedWindows, 0, parameterSpace.TotalCombinations);
        }

        // Wire progress reporting if invoked from auto-optimize
        Action<int, int, long, long>? wfProgress = null;
        var progressRunId = AutoOptimizeProgressStore.ActiveRunId;
        if (progressRunId.HasValue)
        {
            var pId = progressRunId.Value;
            wfProgress = (windowIdx, totalWindows, completedCombos, totalCombos) =>
                AutoOptimizeProgressStore.Update(pId, "Running walk-forward optimization",
                    windowIdx, totalWindows, completedCombos, totalCombos);
        }

        var wfResult = WalkForwardAnalyzer.Analyze(barsForWindows, parameterSpace, FullFactory,
            onProgress: wfProgress);

        logger.LogInformation(
            "[Optimization] Complete: {Windows} windows, OOS Sharpe={Sharpe:F2}, Grade={Grade}, Elapsed={Elapsed}",
            wfResult.Windows.Count, wfResult.AverageOutOfSampleSharpe, wfResult.Grade, wfResult.ElapsedTime);

        // 9. Save optimized params
        Guid? optimizedParamsId = null;
        if (wfResult.Windows.Count > 0)
        {
            var saved = await SaveOptimizedParamsHandler.HandleAsync(command.StrategyId, wfResult, db);
            optimizedParamsId = saved.Id;
        }

        // 10. Return result
        return new OptimizationResultDto(
            BlessedParameters: wfResult.BlessedParameters.Values,
            AvgOutOfSampleSharpe: Math.Round(wfResult.AverageOutOfSampleSharpe, 4),
            AvgEfficiency: Math.Round(wfResult.AverageEfficiency, 4),
            AvgOverfittingScore: Math.Round(wfResult.AverageOverfittingScore, 4),
            OverfittingGrade: wfResult.Grade.ToString(),
            WindowCount: wfResult.Windows.Count,
            OptimizedParamsId: optimizedParamsId);
    }

    /// <summary>
    /// Apply threshold-type parameters (non-indicator-period) to strategy definition conditions.
    /// Maps param names like "EntryRsiThreshold", "StopMultiplier", etc. to the strategy.
    /// </summary>
    private static void ApplyThresholdParams(StrategyDefinition definition, ParameterSet paramSet)
    {
        foreach (var (name, value) in paramSet.Values)
        {
            if (IndicatorPeriodParams.Contains(name))
                continue; // handled by indicator recomputation

            // Match known threshold patterns
            switch (name)
            {
                case "StopMultiplier" when definition.StopLoss is not null:
                    definition.StopLoss.Multiplier = value;
                    break;
                case "TakeProfitMultiplier" when definition.TakeProfit is not null:
                    definition.TakeProfit.Multiplier = value;
                    break;
                case "RiskPercent" when definition.PositionSizing is not null:
                    definition.PositionSizing.RiskPercent = value;
                    break;
                case "MaxStopLossPercent" when definition.StopLoss is not null:
                    definition.StopLoss.MaxStopLossPercent = value;
                    break;
                case "MaxHoldingDays" when definition.PositionSizing is not null:
                    definition.PositionSizing.MaxHoldingDays = (int)value;
                    break;
                case "MaxPortfolioHeat" when definition.PositionSizing is not null:
                    definition.PositionSizing.MaxPortfolioHeat = value;
                    break;
                case "TrailingActivationR" when definition.StopLoss is not null:
                    definition.StopLoss.UseTrailingStop = true;
                    definition.StopLoss.TrailingActivationR = value;
                    break;
                case "TrailingAtrMultiplier" when definition.StopLoss is not null:
                    definition.StopLoss.UseTrailingStop = true;
                    definition.StopLoss.TrailingAtrMultiplier = value;
                    break;
                default:
                    // Try to match "EntryXxxThreshold" or "ExitXxxThreshold" to condition values
                    ApplyConditionThreshold(definition, name, value);
                    break;
            }
        }
    }

    /// <summary>
    /// Match parameter names to condition values by convention:
    ///   "EntryRsiValue" → first RSI condition in entry conditions
    ///   "ExitRsiValue" → first RSI condition in exit conditions
    ///   Generic: any param name matching an indicator name sets Value on first matching condition
    /// </summary>
    private static void ApplyConditionThreshold(StrategyDefinition definition, string paramName, decimal value)
    {
        var isEntry = paramName.StartsWith("Entry", StringComparison.OrdinalIgnoreCase);
        var isExit = paramName.StartsWith("Exit", StringComparison.OrdinalIgnoreCase);

        var groups = isEntry ? definition.EntryConditions
            : isExit ? definition.ExitConditions
            : null;

        if (groups is null) return;

        // Strip "Entry"/"Exit" prefix to get indicator hint
        var indicatorHint = isEntry ? paramName[5..] : isExit ? paramName[4..] : paramName;
        // Strip "Value" or "Threshold" suffix
        indicatorHint = indicatorHint
            .Replace("Value", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Threshold", "", StringComparison.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            foreach (var condition in group.Conditions)
            {
                if (condition.Indicator.Contains(indicatorHint, StringComparison.OrdinalIgnoreCase))
                {
                    condition.Value = value;
                    return; // apply to first match only
                }
            }
        }
    }

    /// <summary>
    /// Build IndicatorConfig from parameter set, using defaults for any missing values.
    /// </summary>
    private static IndicatorConfig BuildIndicatorConfig(ParameterSet paramSet)
    {
        return new IndicatorConfig
        {
            RsiPeriod = paramSet.TryGet("RsiPeriod", out var rsi) ? (int)rsi : 14,
            EmaShortPeriod = paramSet.TryGet("EmaShortPeriod", out var emaS) ? (int)emaS : 12,
            EmaMediumPeriod = paramSet.TryGet("EmaMediumPeriod", out var emaM) ? (int)emaM : 26,
            EmaLongPeriod = paramSet.TryGet("EmaLongPeriod", out var emaL) ? (int)emaL : 50,
            SmaShortPeriod = paramSet.TryGet("SmaShortPeriod", out var smaS) ? (int)smaS : 10,
            SmaMediumPeriod = paramSet.TryGet("SmaMediumPeriod", out var smaM) ? (int)smaM : 20,
            SmaLongPeriod = paramSet.TryGet("SmaLongPeriod", out var smaL) ? (int)smaL : 50,
            MacdFastPeriod = paramSet.TryGet("MacdFastPeriod", out var mF) ? (int)mF : 12,
            MacdSlowPeriod = paramSet.TryGet("MacdSlowPeriod", out var mS) ? (int)mS : 26,
            MacdSignalPeriod = paramSet.TryGet("MacdSignalPeriod", out var mSig) ? (int)mSig : 9,
            AtrPeriod = paramSet.TryGet("AtrPeriod", out var atr) ? (int)atr : 14,
            BollingerPeriod = paramSet.TryGet("BollingerPeriod", out var boll) ? (int)boll : 20,
            StochasticKPeriod = paramSet.TryGet("StochasticKPeriod", out var stK) ? (int)stK : 14,
            StochasticDPeriod = paramSet.TryGet("StochasticDPeriod", out var stD) ? (int)stD : 3,
            VolumeMaPeriod = paramSet.TryGet("VolumeMaPeriod", out var vol) ? (int)vol : 20,
        };
    }

    private static Dictionary<CandleInterval, List<PriceCandle>> BuildCandlesByTimeframe(
        List<PriceCandle> daily, List<PriceCandle> weekly, List<PriceCandle> monthly)
    {
        var result = new Dictionary<CandleInterval, List<PriceCandle>>
        {
            [CandleInterval.Daily] = daily
        };
        if (weekly.Count > 0) result[CandleInterval.Weekly] = weekly;
        if (monthly.Count > 0) result[CandleInterval.Monthly] = monthly;
        return result;
    }
}
