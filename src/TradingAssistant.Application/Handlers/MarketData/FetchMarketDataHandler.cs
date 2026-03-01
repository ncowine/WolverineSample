using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.MarketData;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Domain.MarketData;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.MarketData;

public class FetchMarketDataHandler
{
    public static async Task<FetchMarketDataResponse> HandleAsync(
        FetchMarketDataCommand command,
        IMarketDataProvider provider,
        MarketDataDbContext db,
        ILogger<FetchMarketDataHandler> logger)
    {
        var symbol = command.Symbol.Trim().ToUpperInvariant();

        // Ensure stock exists in DB
        var stock = await db.Stocks.FirstOrDefaultAsync(s => s.Symbol == symbol);
        if (stock is null)
        {
            stock = new Stock
            {
                Symbol = symbol,
                Name = symbol, // Will be updated later with proper metadata
                Exchange = "",
                Sector = ""
            };
            db.Stocks.Add(stock);
            await db.SaveChangesAsync();
            logger.LogInformation("Created new stock entry for {Symbol}", symbol);
        }

        // Default range: 5 years of history
        var to = command.To ?? DateTime.UtcNow.Date;
        var from = command.From ?? to.AddYears(-5);

        // Fetch from Yahoo Finance
        var candles = await provider.GetDailyCandlesAsync(symbol, from, to);

        if (candles.Count == 0)
        {
            return new FetchMarketDataResponse(symbol, 0, 0, null, null, "No data returned from provider.");
        }

        // Find existing candle dates to avoid duplicates
        var existingDatesList = await db.PriceCandles
            .Where(c => c.StockId == stock.Id && c.Interval == CandleInterval.Daily)
            .Where(c => c.Timestamp >= from && c.Timestamp <= to)
            .Select(c => c.Timestamp.Date)
            .ToListAsync();
        var existingDates = new HashSet<DateTime>(existingDatesList);

        var newCandles = new List<PriceCandle>();

        foreach (var candle in candles)
        {
            if (existingDates.Contains(candle.Date.Date))
                continue;

            newCandles.Add(new PriceCandle
            {
                StockId = stock.Id,
                Open = candle.Open,
                High = candle.High,
                Low = candle.Low,
                Close = candle.AdjustedClose, // Use adjusted close as the canonical close
                Volume = candle.Volume,
                Timestamp = candle.Date,
                Interval = CandleInterval.Daily
            });
        }

        if (newCandles.Count > 0)
        {
            // Batch insert in chunks of 500 for performance
            const int batchSize = 500;
            for (var i = 0; i < newCandles.Count; i += batchSize)
            {
                var batch = newCandles.Skip(i).Take(batchSize);
                db.PriceCandles.AddRange(batch);
                await db.SaveChangesAsync();
            }

            logger.LogInformation(
                "Stored {NewCount} new candles for {Symbol} ({Skipped} duplicates skipped)",
                newCandles.Count, symbol, candles.Count - newCandles.Count);
        }

        return new FetchMarketDataResponse(
            Symbol: symbol,
            CandlesFetched: candles.Count,
            CandlesStored: newCandles.Count,
            EarliestDate: candles.Min(c => c.Date),
            LatestDate: candles.Max(c => c.Date),
            Message: newCandles.Count > 0
                ? $"Fetched {candles.Count} candles, stored {newCandles.Count} new."
                : $"Fetched {candles.Count} candles, all already exist in database.");
    }
}
