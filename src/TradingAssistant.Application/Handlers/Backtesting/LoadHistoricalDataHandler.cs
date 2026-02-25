using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingAssistant.Contracts.Events;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Backtesting;

public class LoadHistoricalDataHandler
{
    public static async Task<HistoricalDataLoaded> HandleAsync(
        LoadHistoricalData command,
        MarketDataDbContext db,
        BacktestDbContext backtestDb,
        ILogger<LoadHistoricalDataHandler> logger)
    {
        logger.LogInformation("[MarketDataDb] Loading historical data for {Symbol} from {Start} to {End}",
            command.Symbol, command.StartDate, command.EndDate);

        var stock = await db.Stocks.FirstOrDefaultAsync(s => s.Symbol == command.Symbol)
            ?? throw new InvalidOperationException($"Stock '{command.Symbol}' not found in market data.");

        var candles = await db.PriceCandles
            .Where(c => c.StockId == stock.Id
                && c.Timestamp >= command.StartDate
                && c.Timestamp <= command.EndDate
                && c.Interval == CandleInterval.Daily)
            .OrderBy(c => c.Timestamp)
            .Select(c => new { c.Open, c.High, c.Low, c.Close, c.Volume, c.Timestamp })
            .ToListAsync();

        // Update backtest run status
        var run = await backtestDb.BacktestRuns.FindAsync(command.BacktestRunId);
        if (run != null)
        {
            run.Status = BacktestRunStatus.Running;
            await backtestDb.SaveChangesAsync();
        }

        var priceDataJson = JsonSerializer.Serialize(candles);

        logger.LogInformation("[MarketDataDb] Loaded {Count} candles for {Symbol}", candles.Count, command.Symbol);

        // Load strategy rules for the backtest
        var strategy = await backtestDb.Strategies
            .Include(s => s.Rules)
            .FirstOrDefaultAsync(s => s.Id == command.StrategyId);

        var rulesJson = strategy != null
            ? JsonSerializer.Serialize(strategy.Rules.Select(r => new
            {
                IndicatorType = r.IndicatorType.ToString(),
                r.Condition,
                r.Threshold,
                SignalType = r.SignalType.ToString()
            }))
            : "[]";

        return new HistoricalDataLoaded(
            command.BacktestRunId, command.StrategyId,
            command.Symbol, priceDataJson);
    }
}
