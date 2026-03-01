using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingAssistant.Application.Services;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.MarketData;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Domain.MarketData;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.MarketData;

public class IngestMarketDataHandler
{
    private const int BatchSize = 500;

    public static async Task<IngestMarketDataResponse> HandleAsync(
        IngestMarketDataCommand command,
        IMarketDataProvider provider,
        MarketDataDbContext db,
        ILogger<IngestMarketDataHandler> logger)
    {
        var symbol = command.Symbol.Trim().ToUpperInvariant();
        var to = DateTime.UtcNow.Date;
        var from = to.AddYears(-command.YearsBack);

        logger.LogInformation("Ingesting {Symbol}: {YearsBack}y of data ({From:yyyy-MM-dd} to {To:yyyy-MM-dd})",
            symbol, command.YearsBack, from, to);

        // Ensure stock exists
        var stock = await db.Stocks.FirstOrDefaultAsync(s => s.Symbol == symbol);
        if (stock is null)
        {
            stock = new Stock { Symbol = symbol, Name = symbol, Exchange = "", Sector = "" };
            db.Stocks.Add(stock);
            await db.SaveChangesAsync();
        }

        // Fetch daily candles from provider
        var fetched = await provider.GetDailyCandlesAsync(symbol, from, to);
        if (fetched.Count == 0)
        {
            return new IngestMarketDataResponse(symbol, 0, 0, 0, null, null, "No data returned from provider.");
        }

        // --- Daily candles: deduplicate and store ---
        var existingDailyDates = await GetExistingDatesAsync(db, stock.Id, CandleInterval.Daily, from, to);

        var newDailyCandles = new List<PriceCandle>();
        foreach (var c in fetched)
        {
            if (existingDailyDates.Contains(c.Date.Date))
                continue;

            newDailyCandles.Add(new PriceCandle
            {
                StockId = stock.Id,
                Open = c.Open,
                High = c.High,
                Low = c.Low,
                Close = c.AdjustedClose,
                Volume = c.Volume,
                Timestamp = c.Date,
                Interval = CandleInterval.Daily
            });
        }

        await BulkInsertAsync(db, newDailyCandles, logger, symbol, "daily");

        // Load ALL daily candles for this stock to build accurate weekly/monthly
        var allDailyCandles = await db.PriceCandles
            .Where(c => c.StockId == stock.Id && c.Interval == CandleInterval.Daily)
            .OrderBy(c => c.Timestamp)
            .ToListAsync();

        // --- Weekly aggregation ---
        var weeklyCandles = CandleAggregator.AggregateDailyToWeekly(allDailyCandles, stock.Id);
        var existingWeeklyDates = await GetExistingDatesAsync(db, stock.Id, CandleInterval.Weekly, from, to);

        // Remove existing weekly candles in range and replace with fresh aggregation
        var existingWeekly = await db.PriceCandles
            .Where(c => c.StockId == stock.Id && c.Interval == CandleInterval.Weekly)
            .Where(c => c.Timestamp >= from && c.Timestamp <= to)
            .ToListAsync();
        db.PriceCandles.RemoveRange(existingWeekly);
        await db.SaveChangesAsync();

        await BulkInsertAsync(db, weeklyCandles, logger, symbol, "weekly");

        // --- Monthly aggregation ---
        var monthlyCandles = CandleAggregator.AggregateDailyToMonthly(allDailyCandles, stock.Id);

        var existingMonthly = await db.PriceCandles
            .Where(c => c.StockId == stock.Id && c.Interval == CandleInterval.Monthly)
            .Where(c => c.Timestamp >= from && c.Timestamp <= to)
            .ToListAsync();
        db.PriceCandles.RemoveRange(existingMonthly);
        await db.SaveChangesAsync();

        await BulkInsertAsync(db, monthlyCandles, logger, symbol, "monthly");

        logger.LogInformation(
            "Ingestion complete for {Symbol}: {Daily} daily, {Weekly} weekly, {Monthly} monthly candles",
            symbol, newDailyCandles.Count, weeklyCandles.Count, monthlyCandles.Count);

        return new IngestMarketDataResponse(
            Symbol: symbol,
            DailyCandlesStored: newDailyCandles.Count,
            WeeklyCandlesStored: weeklyCandles.Count,
            MonthlyCandlesStored: monthlyCandles.Count,
            EarliestDate: fetched.Min(c => c.Date),
            LatestDate: fetched.Max(c => c.Date),
            Message: $"Ingested {symbol}: {newDailyCandles.Count} daily, {weeklyCandles.Count} weekly, {monthlyCandles.Count} monthly candles stored.");
    }

    private static async Task<HashSet<DateTime>> GetExistingDatesAsync(
        MarketDataDbContext db, Guid stockId, CandleInterval interval, DateTime from, DateTime to)
    {
        var dates = await db.PriceCandles
            .Where(c => c.StockId == stockId && c.Interval == interval)
            .Where(c => c.Timestamp >= from && c.Timestamp <= to)
            .Select(c => c.Timestamp.Date)
            .ToListAsync();
        return new HashSet<DateTime>(dates);
    }

    private static async Task BulkInsertAsync(
        MarketDataDbContext db, List<PriceCandle> candles,
        ILogger logger, string symbol, string intervalName)
    {
        if (candles.Count == 0) return;

        for (var i = 0; i < candles.Count; i += BatchSize)
        {
            var batch = candles.Skip(i).Take(BatchSize);
            db.PriceCandles.AddRange(batch);
            await db.SaveChangesAsync();
        }

        logger.LogInformation("Stored {Count} {Interval} candles for {Symbol}",
            candles.Count, intervalName, symbol);
    }
}
