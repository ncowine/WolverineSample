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
using TradingAssistant.Domain.Backtesting;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Domain.MarketData;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Backtesting;

/// <summary>
/// Unified backtest handler: runs both single-symbol and portfolio backtests synchronously.
/// Returns BacktestResultDto directly (no cascade, no polling needed).
/// </summary>
public class RunBacktestHandler
{
    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Built-in regime strategy templates for runtime regime-adaptive switching.
    /// </summary>
    internal static readonly Dictionary<string, StrategyDefinition> RegimeTemplates = new()
    {
        // Bull: trend-following — enter when trend is active and RSI shows fresh momentum
        ["Bull"] = new StrategyDefinition
        {
            EntryConditions = new List<ConditionGroup>
            {
                new() { Timeframe = "Daily", Conditions = new List<Condition>
                {
                    new() { Indicator = "EMAShort", Comparison = "IsAbove", Value = 0, ReferenceIndicator = "EMALong" },
                }},
                new() { Timeframe = "Daily", Conditions = new List<Condition>
                {
                    new() { Indicator = "RSI", Comparison = "CrossAbove", Value = 50 },
                }},
            },
            ExitConditions = new List<ConditionGroup>
            {
                new() { Timeframe = "Daily", Conditions = new List<Condition>
                {
                    new() { Indicator = "EMAShort", Comparison = "IsBelow", Value = 0, ReferenceIndicator = "EMALong" },
                    new() { Indicator = "RSI", Comparison = "LessThan", Value = 40 },
                }},
            },
            StopLoss = new StopLossConfig { Type = "Atr", Multiplier = 2m },
            TakeProfit = new TakeProfitConfig { Type = "RMultiple", Multiplier = 2.5m },
            PositionSizing = new PositionSizingConfig { RiskPercent = 1m, MaxPositions = 6, MaxPortfolioHeat = 12m },
        },
        // Sideways: mean reversion — buy oversold, sell on recovery
        ["Sideways"] = new StrategyDefinition
        {
            EntryConditions = new List<ConditionGroup>
            {
                new() { Timeframe = "Daily", Conditions = new List<Condition>
                {
                    new() { Indicator = "RSI", Comparison = "LessThan", Value = 35 },
                    new() { Indicator = "BollingerPercentB", Comparison = "LessThan", Value = 0.2m },
                }},
            },
            ExitConditions = new List<ConditionGroup>
            {
                new() { Timeframe = "Daily", Conditions = new List<Condition>
                {
                    new() { Indicator = "RSI", Comparison = "GreaterThan", Value = 55 },
                    new() { Indicator = "BollingerPercentB", Comparison = "GreaterThan", Value = 0.7m },
                }},
            },
            StopLoss = new StopLossConfig { Type = "Atr", Multiplier = 1.5m },
            TakeProfit = new TakeProfitConfig { Type = "RMultiple", Multiplier = 2m },
            PositionSizing = new PositionSizingConfig { RiskPercent = 0.75m, MaxPositions = 5, MaxPortfolioHeat = 12m },
        },
        // HighVolatility: volatility breakout — enter when price breaks above upper band with momentum
        ["HighVolatility"] = new StrategyDefinition
        {
            EntryConditions = new List<ConditionGroup>
            {
                new() { Timeframe = "Daily", Conditions = new List<Condition>
                {
                    new() { Indicator = "Price", Comparison = "IsAbove", Value = 0, ReferenceIndicator = "BollingerUpper" },
                }},
                new() { Timeframe = "Daily", Conditions = new List<Condition>
                {
                    new() { Indicator = "RSI", Comparison = "GreaterThan", Value = 55 },
                }},
            },
            ExitConditions = new List<ConditionGroup>
            {
                new() { Timeframe = "Daily", Conditions = new List<Condition>
                {
                    new() { Indicator = "Price", Comparison = "IsBelow", Value = 0, ReferenceIndicator = "EMAMedium" },
                    new() { Indicator = "RSI", Comparison = "LessThan", Value = 45 },
                }},
            },
            StopLoss = new StopLossConfig { Type = "Atr", Multiplier = 2.5m },
            TakeProfit = new TakeProfitConfig { Type = "RMultiple", Multiplier = 2m },
            PositionSizing = new PositionSizingConfig { RiskPercent = 0.75m, MaxPositions = 5, MaxPortfolioHeat = 10m },
        },
        // Bear: defensive mean reversion — only buy deep oversold bounces
        ["Bear"] = new StrategyDefinition
        {
            EntryConditions = new List<ConditionGroup>
            {
                new() { Timeframe = "Daily", Conditions = new List<Condition>
                {
                    new() { Indicator = "RSI", Comparison = "LessThan", Value = 25 },
                }},
                new() { Timeframe = "Daily", Conditions = new List<Condition>
                {
                    new() { Indicator = "BollingerPercentB", Comparison = "LessThan", Value = 0.05m },
                }},
            },
            ExitConditions = new List<ConditionGroup>
            {
                new() { Timeframe = "Daily", Conditions = new List<Condition>
                {
                    new() { Indicator = "RSI", Comparison = "GreaterThan", Value = 50 },
                    new() { Indicator = "BollingerPercentB", Comparison = "GreaterThan", Value = 0.5m },
                }},
            },
            StopLoss = new StopLossConfig { Type = "Atr", Multiplier = 2.5m },
            TakeProfit = new TakeProfitConfig { Type = "RMultiple", Multiplier = 2m },
            PositionSizing = new PositionSizingConfig { RiskPercent = 0.5m, MaxPositions = 3, MaxPortfolioHeat = 6m },
        },
    };

    public static async Task<BacktestResultDto> HandleAsync(
        RunBacktestCommand command,
        BacktestDbContext db,
        MarketDataDbContext marketDb,
        IMarketDataProvider marketDataProvider,
        ILoggerFactory loggerFactory,
        ILogger<RunBacktestHandler> logger)
    {
        // Load strategy
        var strategy = await db.Strategies
            .Include(s => s.Rules)
            .FirstOrDefaultAsync(s => s.Id == command.StrategyId)
            ?? throw new InvalidOperationException($"Strategy '{command.StrategyId}' not found.");

        if (!strategy.UsesV2Engine)
            throw new InvalidOperationException("Backtests require a V2 strategy with RulesJson.");

        var definition = JsonSerializer.Deserialize<StrategyDefinition>(strategy.RulesJson!,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Failed to deserialize strategy definition.");

        definition.PositionSizing.MaxPositions = command.MaxPositions;

        var isPortfolio = command.UniverseId.HasValue;

        // Determine symbols
        List<string> symbols;
        string? universeName = null;
        if (isPortfolio)
        {
            var universe = await marketDb.StockUniverses.FirstOrDefaultAsync(u => u.Id == command.UniverseId)
                ?? throw new InvalidOperationException($"Universe {command.UniverseId} not found.");
            symbols = universe.GetSymbolList();
            universeName = universe.Name;
        }
        else
        {
            symbols = new List<string> { command.Symbol };
        }

        // Create run record
        var run = new BacktestRun
        {
            StrategyId = command.StrategyId,
            Symbol = isPortfolio ? "PORTFOLIO" : command.Symbol,
            StartDate = command.StartDate,
            EndDate = command.EndDate,
            Status = BacktestRunStatus.Running,
            UniverseId = command.UniverseId,
            UniverseName = universeName,
            InitialCapital = command.InitialCapital,
            MaxPositions = command.MaxPositions,
            TotalSymbols = symbols.Count,
        };

        db.BacktestRuns.Add(run);
        await db.SaveChangesAsync();

        logger.LogInformation("[Backtest] Starting run {RunId} for {Count} symbol(s), strategy '{Name}'",
            run.Id, symbols.Count, strategy.Name);

        try
        {
            // Auto-ingest missing data
            var ingestLogger = loggerFactory.CreateLogger<Handlers.MarketData.IngestMarketDataHandler>();
            var yearsBack = Math.Max(5, (int)Math.Ceiling((DateTime.UtcNow - command.StartDate).TotalDays / 365.25));

            foreach (var symbol in symbols)
            {
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
                    try
                    {
                        await Handlers.MarketData.IngestMarketDataHandler.HandleAsync(
                            new IngestMarketDataCommand(symbol, yearsBack),
                            marketDataProvider, marketDb, ingestLogger);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "[Backtest] Failed to ingest {Symbol}, skipping", symbol);
                    }
                    if (symbols.Count > 1) await Task.Delay(500); // rate limit
                }
            }

            // Ensure SPY data for benchmark
            await EnsureSpyData(marketDb, marketDataProvider, ingestLogger, command.StartDate, yearsBack, logger);

            // Load candles and compute indicators for all symbols
            var symbolBars = new Dictionary<string, CandleWithIndicators[]>();
            var symbolsWithData = 0;

            foreach (var symbol in symbols)
            {
                var bars = await LoadSymbolBars(marketDb, symbol, command.StartDate, command.EndDate);
                if (bars != null && bars.Length >= 2)
                {
                    symbolBars[symbol] = bars;
                    symbolsWithData++;
                }
            }

            run.SymbolsWithData = symbolsWithData;
            logger.LogInformation("[Backtest] {RunId}: {WithData}/{Total} symbols have data",
                run.Id, symbolsWithData, symbols.Count);

            if (symbolBars.Count == 0)
            {
                run.Status = BacktestRunStatus.Completed;
                await db.SaveChangesAsync();
                return MapToDto(run, null);
            }

            // Build regime strategies for portfolio backtests
            var regimeStrategies = isPortfolio ? BuildRegimeStrategies(command.MaxPositions) : null;

            // Determine cost profile
            var costProfile = CostProfileData.ForMarket(command.CostProfileMarket);

            // Run unified engine
            var engine = new PortfolioBacktestEngine(
                definition,
                command.MaxPositions,
                command.InitialCapital,
                regimeStrategies: regimeStrategies);
            engine.SetCostProfile(costProfile);

            var engineResult = engine.Run(symbolBars);

            // Build SPY benchmark
            var benchmarkEquityCurve = await BuildSpyBenchmarkCurve(
                marketDb, command.StartDate, command.EndDate, command.InitialCapital);

            // Compute metrics
            var metrics = PerformanceCalculator.Calculate(engineResult, 4.5m, benchmarkEquityCurve);

            // Serialize JSON
            var equityCurveJson = JsonSerializer.Serialize(engineResult.EquityCurve, CamelCase);
            var tradeLogJson = JsonSerializer.Serialize(engineResult.Trades, CamelCase);
            var monthlyReturnsJson = JsonSerializer.Serialize(metrics.MonthlyReturns, CamelCase);
            var symbolBreakdownJson = engineResult.SymbolBreakdowns.Count > 0
                ? JsonSerializer.Serialize(engineResult.SymbolBreakdowns.Values.OrderByDescending(s => s.TotalPnL), CamelCase)
                : null;
            var executionLogJson = engineResult.Log.Count > 0
                ? JsonSerializer.Serialize(engineResult.Log, CamelCase)
                : null;
            var regimeTimelineJson = engineResult.RegimeTimeline.Count > 0
                ? JsonSerializer.Serialize(engineResult.RegimeTimeline.Select(r => new { date = r.Date, regime = r.Regime }), CamelCase)
                : null;
            var spyComparisonJson = JsonSerializer.Serialize(new
            {
                strategyCagr = Math.Round(metrics.Cagr, 2),
                spyCagr = Math.Round(metrics.BenchmarkCagr, 2),
                alpha = Math.Round(metrics.Alpha, 2),
                beta = Math.Round(metrics.Beta, 2)
            }, CamelCase);

            // Save result
            run.Status = BacktestRunStatus.Completed;

            var result = new BacktestResult
            {
                BacktestRunId = run.Id,
                TotalTrades = metrics.TotalTrades,
                WinRate = Math.Round(metrics.WinRate, 2),
                TotalReturn = Math.Round(metrics.TotalReturn, 2),
                MaxDrawdown = Math.Round(Math.Abs(metrics.MaxDrawdownPercent), 2),
                SharpeRatio = Math.Round(metrics.SharpeRatio, 2),
                Cagr = Math.Round(metrics.Cagr, 2),
                SortinoRatio = Math.Round(metrics.SortinoRatio, 2),
                CalmarRatio = Math.Round(metrics.CalmarRatio, 2),
                ProfitFactor = Math.Round(metrics.ProfitFactor, 2),
                Expectancy = Math.Round(metrics.Expectancy, 2),
                EquityCurveJson = equityCurveJson,
                TradeLogJson = tradeLogJson,
                MonthlyReturnsJson = monthlyReturnsJson,
                SpyComparisonJson = spyComparisonJson,
                ParametersJson = strategy.RulesJson ?? "{}",
                // Portfolio-specific
                UniqueSymbolsTraded = engineResult.UniqueSymbolsTraded > 0 ? engineResult.UniqueSymbolsTraded : null,
                AveragePositionsHeld = engineResult.AveragePositionsHeld > 0 ? engineResult.AveragePositionsHeld : null,
                MaxPositionsHeld = engineResult.MaxPositionsHeld > 0 ? engineResult.MaxPositionsHeld : null,
                SymbolBreakdownJson = symbolBreakdownJson,
                ExecutionLogJson = executionLogJson,
                RegimeTimelineJson = regimeTimelineJson,
                ResultData = JsonSerializer.Serialize(engineResult.Trades.Select(t => new
                {
                    t.Symbol, t.EntryDate, t.EntryPrice, t.ExitDate, t.ExitPrice,
                    t.PnL, t.PnLPercent, t.HoldingDays, t.ExitReason,
                    t.SignalScore, t.Regime
                }), CamelCase),
            };

            db.BacktestResults.Add(result);
            await db.SaveChangesAsync();

            logger.LogInformation(
                "[Backtest] {RunId} completed: {Trades} trades, {Return:F2}% return, Sharpe={Sharpe:F2}",
                run.Id, metrics.TotalTrades, metrics.TotalReturn, metrics.SharpeRatio);

            return MapToDto(run, result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Backtest] {RunId} failed", run.Id);
            run.Status = BacktestRunStatus.Failed;
            await db.SaveChangesAsync();
            throw;
        }
    }

    private static Dictionary<string, StrategyDefinition> BuildRegimeStrategies(int maxPositions)
    {
        var result = new Dictionary<string, StrategyDefinition>();
        foreach (var (regime, tpl) in RegimeTemplates)
        {
            var clone = JsonSerializer.Deserialize<StrategyDefinition>(
                JsonSerializer.Serialize(tpl, CamelCase),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            clone.PositionSizing.MaxPositions = maxPositions;
            result[regime] = clone;
        }
        return result;
    }

    private static async Task<CandleWithIndicators[]?> LoadSymbolBars(
        MarketDataDbContext marketDb, string symbol, DateTime startDate, DateTime endDate)
    {
        var stock = await marketDb.Stocks.FirstOrDefaultAsync(s => s.Symbol == symbol);
        if (stock is null) return null;

        var dailyCandles = await marketDb.PriceCandles
            .Where(c => c.StockId == stock.Id && c.Timestamp >= startDate && c.Timestamp <= endDate && c.Interval == CandleInterval.Daily)
            .OrderBy(c => c.Timestamp).ToListAsync();

        if (dailyCandles.Count < 20) return null;

        var weeklyCandles = await marketDb.PriceCandles
            .Where(c => c.StockId == stock.Id && c.Timestamp >= startDate && c.Timestamp <= endDate && c.Interval == CandleInterval.Weekly)
            .OrderBy(c => c.Timestamp).ToListAsync();

        var monthlyCandles = await marketDb.PriceCandles
            .Where(c => c.StockId == stock.Id && c.Timestamp >= startDate && c.Timestamp <= endDate && c.Interval == CandleInterval.Monthly)
            .OrderBy(c => c.Timestamp).ToListAsync();

        var candlesByTimeframe = new Dictionary<CandleInterval, List<PriceCandle>>
        {
            [CandleInterval.Daily] = dailyCandles
        };
        if (weeklyCandles.Count > 0) candlesByTimeframe[CandleInterval.Weekly] = weeklyCandles;
        if (monthlyCandles.Count > 0) candlesByTimeframe[CandleInterval.Monthly] = monthlyCandles;

        var multiTf = IndicatorOrchestrator.Compute(candlesByTimeframe);
        return multiTf.AlignedDaily.Length >= 2 ? multiTf.AlignedDaily : null;
    }

    private static async Task EnsureSpyData(
        MarketDataDbContext marketDb, IMarketDataProvider marketDataProvider,
        ILogger ingestLogger, DateTime startDate, int yearsBack, ILogger logger)
    {
        var spyStock = await marketDb.Stocks.FirstOrDefaultAsync(s => s.Symbol == "SPY");
        var spyCount = spyStock is not null
            ? await marketDb.PriceCandles.CountAsync(c => c.StockId == spyStock.Id
                && c.Timestamp >= startDate && c.Interval == CandleInterval.Daily)
            : 0;

        if (spyCount < 20)
        {
            try
            {
                await Handlers.MarketData.IngestMarketDataHandler.HandleAsync(
                    new IngestMarketDataCommand("SPY", yearsBack),
                    marketDataProvider, marketDb,
                    (ILogger<Handlers.MarketData.IngestMarketDataHandler>)ingestLogger);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[Backtest] Failed to ingest SPY benchmark data");
            }
        }
    }

    private static async Task<List<EquityPoint>?> BuildSpyBenchmarkCurve(
        MarketDataDbContext marketDb, DateTime startDate, DateTime endDate, decimal initialCapital)
    {
        var spyStock = await marketDb.Stocks.FirstOrDefaultAsync(s => s.Symbol == "SPY");
        if (spyStock is null) return null;

        var spyCandles = await marketDb.PriceCandles
            .Where(c => c.StockId == spyStock.Id && c.Timestamp >= startDate && c.Timestamp <= endDate && c.Interval == CandleInterval.Daily)
            .OrderBy(c => c.Timestamp).ToListAsync();

        if (spyCandles.Count < 2) return null;

        var startPrice = spyCandles[0].Close;
        if (startPrice <= 0) return null;

        var shares = initialCapital / startPrice;
        return spyCandles.Select(c => new EquityPoint(c.Timestamp, shares * c.Close)).ToList();
    }

    internal static BacktestResultDto MapToDto(BacktestRun run, BacktestResult? result)
    {
        return new BacktestResultDto(
            Id: result?.Id ?? Guid.Empty,
            BacktestRunId: run.Id,
            StrategyId: run.StrategyId,
            Symbol: run.Symbol,
            Status: run.Status.ToString(),
            TotalTrades: result?.TotalTrades ?? 0,
            WinRate: result?.WinRate ?? 0,
            TotalReturn: result?.TotalReturn ?? 0,
            MaxDrawdown: result?.MaxDrawdown ?? 0,
            SharpeRatio: result?.SharpeRatio ?? 0,
            StartDate: run.StartDate,
            EndDate: run.EndDate,
            CreatedAt: run.CreatedAt,
            Cagr: result?.Cagr ?? 0,
            SortinoRatio: result?.SortinoRatio ?? 0,
            CalmarRatio: result?.CalmarRatio ?? 0,
            ProfitFactor: result?.ProfitFactor ?? 0,
            Expectancy: result?.Expectancy ?? 0,
            OverfittingScore: result?.OverfittingScore,
            EquityCurveJson: result?.EquityCurveJson,
            TradeLogJson: result?.TradeLogJson,
            MonthlyReturnsJson: result?.MonthlyReturnsJson,
            BenchmarkReturnJson: result?.BenchmarkReturnJson,
            ParametersJson: result?.ParametersJson,
            WalkForwardJson: result?.WalkForwardJson,
            SpyComparisonJson: result?.SpyComparisonJson,
            // Portfolio fields
            UniverseId: run.UniverseId,
            UniverseName: run.UniverseName,
            InitialCapital: run.InitialCapital,
            MaxPositions: run.MaxPositions,
            TotalSymbols: run.TotalSymbols,
            SymbolsWithData: run.SymbolsWithData,
            UniqueSymbolsTraded: result?.UniqueSymbolsTraded,
            AveragePositionsHeld: result?.AveragePositionsHeld,
            MaxPositionsHeld: result?.MaxPositionsHeld,
            SymbolBreakdownJson: result?.SymbolBreakdownJson,
            ExecutionLogJson: result?.ExecutionLogJson,
            RegimeTimelineJson: result?.RegimeTimelineJson);
    }
}
