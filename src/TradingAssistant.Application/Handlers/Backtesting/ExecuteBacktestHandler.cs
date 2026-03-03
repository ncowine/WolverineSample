using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingAssistant.Application.Backtesting;
using TradingAssistant.Application.Indicators;
using TradingAssistant.Contracts.Backtesting;
using TradingAssistant.Contracts.Events;
using TradingAssistant.Domain.Backtesting;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Domain.MarketData;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Backtesting;

public class ExecuteBacktestHandler
{
    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task<BacktestExecuted> HandleAsync(
        HistoricalDataLoaded @event,
        BacktestDbContext db,
        MarketDataDbContext marketDb,
        ILogger<ExecuteBacktestHandler> logger)
    {
        logger.LogInformation("[BacktestDb] Executing backtest {RunId} for {Symbol}",
            @event.BacktestRunId, @event.Symbol);

        // Load strategy
        var strategy = await db.Strategies
            .Include(s => s.Rules)
            .FirstOrDefaultAsync(s => s.Id == @event.StrategyId);

        // Route to v2 engine if strategy uses RulesJson
        if (strategy?.UsesV2Engine == true)
        {
            return await ExecuteV2Async(@event, strategy, db, marketDb, logger);
        }

        return await ExecuteV1Async(@event, strategy, db, logger);
    }

    /// <summary>
    /// V2 engine: full indicator computation, ConditionEvaluator, BacktestEngine, PerformanceCalculator.
    /// </summary>
    private static async Task<BacktestExecuted> ExecuteV2Async(
        HistoricalDataLoaded @event,
        Strategy strategy,
        BacktestDbContext db,
        MarketDataDbContext marketDb,
        ILogger<ExecuteBacktestHandler> logger)
    {
        logger.LogInformation("[V2 Engine] Running full backtest for {Symbol} with strategy '{Name}'",
            @event.Symbol, strategy.Name);

        // Deserialize v2 strategy definition
        var definition = JsonSerializer.Deserialize<StrategyDefinition>(strategy.RulesJson!,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (definition is null)
        {
            logger.LogWarning("[V2 Engine] Failed to deserialize RulesJson for strategy {Id}", strategy.Id);
            return await SaveEmptyResult(@event, db, logger, "Failed to deserialize strategy definition.");
        }

        // Load the backtest run to get date range
        var run = await db.BacktestRuns.FindAsync(@event.BacktestRunId);
        if (run is null)
        {
            return new BacktestExecuted(@event.BacktestRunId, @event.StrategyId, 0, 0, 0, 0, 0);
        }

        // Load candles from MarketDataDbContext
        var stock = await marketDb.Stocks.FirstOrDefaultAsync(s => s.Symbol == @event.Symbol);
        if (stock is null)
        {
            logger.LogWarning("[V2 Engine] Stock '{Symbol}' not found in market data", @event.Symbol);
            return await SaveEmptyResult(@event, db, logger, $"Stock '{@event.Symbol}' not found.");
        }

        var dailyCandles = await marketDb.PriceCandles
            .Where(c => c.StockId == stock.Id
                && c.Timestamp >= run.StartDate
                && c.Timestamp <= run.EndDate
                && c.Interval == CandleInterval.Daily)
            .OrderBy(c => c.Timestamp)
            .ToListAsync();

        logger.LogInformation("[V2 Engine] Loaded {Count} daily candles for {Symbol}", dailyCandles.Count, @event.Symbol);

        if (dailyCandles.Count < 2)
        {
            logger.LogWarning("[V2 Engine] Insufficient data: {Count} candles", dailyCandles.Count);
            return await SaveEmptyResult(@event, db, logger, $"Only {dailyCandles.Count} candles available. Need at least 2.");
        }

        // Load weekly + monthly candles for multi-timeframe analysis
        var weeklyCandles = await marketDb.PriceCandles
            .Where(c => c.StockId == stock.Id
                && c.Timestamp >= run.StartDate
                && c.Timestamp <= run.EndDate
                && c.Interval == CandleInterval.Weekly)
            .OrderBy(c => c.Timestamp)
            .ToListAsync();

        var monthlyCandles = await marketDb.PriceCandles
            .Where(c => c.StockId == stock.Id
                && c.Timestamp >= run.StartDate
                && c.Timestamp <= run.EndDate
                && c.Interval == CandleInterval.Monthly)
            .OrderBy(c => c.Timestamp)
            .ToListAsync();

        // Compute indicators via IndicatorOrchestrator
        var candlesByTimeframe = new Dictionary<CandleInterval, List<PriceCandle>>
        {
            [CandleInterval.Daily] = dailyCandles
        };
        if (weeklyCandles.Count > 0)
            candlesByTimeframe[CandleInterval.Weekly] = weeklyCandles;
        if (monthlyCandles.Count > 0)
            candlesByTimeframe[CandleInterval.Monthly] = monthlyCandles;

        var multiTf = IndicatorOrchestrator.Compute(candlesByTimeframe);
        var bars = multiTf.AlignedDaily;

        if (bars.Length < 2)
        {
            logger.LogWarning("[V2 Engine] Insufficient aligned bars: {Count}", bars.Length);
            return await SaveEmptyResult(@event, db, logger, "Insufficient data after indicator computation.");
        }

        // Run the real backtest engine
        var engine = new BacktestEngine(definition);
        var engineResult = engine.Run(bars, @event.Symbol);

        // Compute full performance metrics
        var metrics = PerformanceCalculator.Calculate(engineResult);

        logger.LogInformation(
            "[V2 Engine] Backtest {RunId} completed: {Trades} trades, {WinRate:F1}% win rate, {Return:F2}% return, Sharpe={Sharpe:F2}",
            @event.BacktestRunId, metrics.TotalTrades, metrics.WinRate, metrics.TotalReturn, metrics.SharpeRatio);

        // Serialize detailed data (plain JSON with camelCase for frontend consumption)
        var equityCurveJson = JsonSerializer.Serialize(engineResult.EquityCurve, CamelCase);
        var tradeLogJson = JsonSerializer.Serialize(engineResult.Trades, CamelCase);
        var monthlyReturnsJson = JsonSerializer.Serialize(metrics.MonthlyReturns, CamelCase);

        // Save result with full metrics
        run.Status = BacktestRunStatus.Completed;

        var result = new BacktestResult
        {
            BacktestRunId = @event.BacktestRunId,
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
            ParametersJson = strategy.RulesJson ?? "{}",
            ResultData = JsonSerializer.Serialize(engineResult.Trades.Select(t => new
            {
                t.Symbol,
                t.EntryDate,
                t.EntryPrice,
                t.ExitDate,
                t.ExitPrice,
                t.PnL,
                t.PnLPercent,
                t.HoldingDays,
                t.ExitReason
            }), CamelCase)
        };

        db.BacktestResults.Add(result);
        await db.SaveChangesAsync();

        return new BacktestExecuted(
            @event.BacktestRunId, @event.StrategyId,
            metrics.TotalTrades, Math.Round(metrics.WinRate, 2),
            Math.Round(metrics.TotalReturn, 2),
            Math.Round(Math.Abs(metrics.MaxDrawdownPercent), 2),
            Math.Round(metrics.SharpeRatio, 2));
    }

    /// <summary>
    /// V1 engine: legacy simple price-change-based simulation (for backward compatibility).
    /// </summary>
    private static async Task<BacktestExecuted> ExecuteV1Async(
        HistoricalDataLoaded @event,
        Strategy? strategy,
        BacktestDbContext db,
        ILogger<ExecuteBacktestHandler> logger)
    {
        logger.LogInformation("[V1 Engine] Running legacy backtest for {Symbol}", @event.Symbol);

        // Deserialize price data
        var priceData = JsonSerializer.Deserialize<List<CandleData>>(@event.PriceDataJson)
            ?? new List<CandleData>();

        // Simple backtest simulation
        var trades = new List<SimulatedTrade>();
        decimal balance = 100_000m;
        decimal peakBalance = balance;
        decimal maxDrawdown = 0;
        bool inPosition = false;
        decimal entryPrice = 0;

        for (int i = 1; i < priceData.Count; i++)
        {
            var current = priceData[i];
            var previous = priceData[i - 1];

            if (strategy?.Rules.Any() == true)
            {
                foreach (var rule in strategy.Rules)
                {
                    var priceChange = (current.Close - previous.Close) / previous.Close * 100;

                    if (rule.SignalType == SignalType.Buy && !inPosition)
                    {
                        if (rule.Condition == "GreaterThan" && priceChange > rule.Threshold)
                        {
                            inPosition = true;
                            entryPrice = current.Close;
                        }
                    }
                    else if (rule.SignalType == SignalType.Sell && inPosition)
                    {
                        if (rule.Condition == "LessThan" && priceChange < -rule.Threshold)
                        {
                            inPosition = false;
                            var pnl = (current.Close - entryPrice) / entryPrice * balance;
                            balance += pnl;
                            trades.Add(new SimulatedTrade(entryPrice, current.Close, pnl > 0));

                            peakBalance = Math.Max(peakBalance, balance);
                            var drawdown = (peakBalance - balance) / peakBalance * 100;
                            maxDrawdown = Math.Max(maxDrawdown, drawdown);
                        }
                    }
                }
            }
            else
            {
                // Default strategy: simple mean reversion
                var priceChange = (current.Close - previous.Close) / previous.Close * 100;

                if (!inPosition && priceChange < -1.5m)
                {
                    inPosition = true;
                    entryPrice = current.Close;
                }
                else if (inPosition && priceChange > 1.0m)
                {
                    inPosition = false;
                    var pnl = (current.Close - entryPrice) / entryPrice * balance;
                    balance += pnl;
                    trades.Add(new SimulatedTrade(entryPrice, current.Close, pnl > 0));

                    peakBalance = Math.Max(peakBalance, balance);
                    var drawdown = (peakBalance - balance) / peakBalance * 100;
                    maxDrawdown = Math.Max(maxDrawdown, drawdown);
                }
            }
        }

        var totalTrades = trades.Count;
        var winRate = totalTrades > 0 ? (decimal)trades.Count(t => t.IsWin) / totalTrades * 100 : 0;
        var totalReturn = (balance - 100_000m) / 100_000m * 100;
        var sharpeRatio = totalTrades > 0
            ? Math.Round(totalReturn / (maxDrawdown == 0 ? 1 : maxDrawdown), 2)
            : 0;

        // Save result
        var run = await db.BacktestRuns.FindAsync(@event.BacktestRunId);
        if (run != null)
        {
            run.Status = BacktestRunStatus.Completed;
        }

        var result = new BacktestResult
        {
            BacktestRunId = @event.BacktestRunId,
            TotalTrades = totalTrades,
            WinRate = Math.Round(winRate, 2),
            TotalReturn = Math.Round(totalReturn, 2),
            MaxDrawdown = Math.Round(maxDrawdown, 2),
            SharpeRatio = sharpeRatio,
            ResultData = JsonSerializer.Serialize(trades)
        };

        db.BacktestResults.Add(result);
        await db.SaveChangesAsync();

        logger.LogInformation(
            "[BacktestDb] Backtest {RunId} completed: {Trades} trades, {WinRate}% win rate, {Return}% return",
            @event.BacktestRunId, totalTrades, winRate, totalReturn);

        return new BacktestExecuted(
            @event.BacktestRunId, @event.StrategyId,
            totalTrades, Math.Round(winRate, 2),
            Math.Round(totalReturn, 2), Math.Round(maxDrawdown, 2), sharpeRatio);
    }

    private static async Task<BacktestExecuted> SaveEmptyResult(
        HistoricalDataLoaded @event,
        BacktestDbContext db,
        ILogger<ExecuteBacktestHandler> logger,
        string reason)
    {
        logger.LogWarning("[V2 Engine] Empty result for {RunId}: {Reason}", @event.BacktestRunId, reason);

        var run = await db.BacktestRuns.FindAsync(@event.BacktestRunId);
        if (run != null)
        {
            run.Status = BacktestRunStatus.Completed;
        }

        var result = new BacktestResult
        {
            BacktestRunId = @event.BacktestRunId,
            TotalTrades = 0,
            WinRate = 0,
            TotalReturn = 0,
            MaxDrawdown = 0,
            SharpeRatio = 0,
            ResultData = JsonSerializer.Serialize(new { error = reason })
        };

        db.BacktestResults.Add(result);
        await db.SaveChangesAsync();

        return new BacktestExecuted(@event.BacktestRunId, @event.StrategyId, 0, 0, 0, 0, 0);
    }

    private record CandleData(decimal Open, decimal High, decimal Low, decimal Close, long Volume, DateTime Timestamp);
    private record SimulatedTrade(decimal EntryPrice, decimal ExitPrice, bool IsWin);
}
