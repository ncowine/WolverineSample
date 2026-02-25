using Microsoft.EntityFrameworkCore;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.MarketData;

public class GetHistoricalPricesHandler
{
    public static async Task<List<CandleDto>> HandleAsync(
        GetHistoricalPricesQuery query,
        MarketDataDbContext db)
    {
        var stock = await db.Stocks
            .FirstOrDefaultAsync(s => s.Symbol == query.Symbol)
            ?? throw new InvalidOperationException($"Stock '{query.Symbol}' not found.");

        if (!Enum.TryParse<CandleInterval>(query.Interval, true, out var interval))
            interval = CandleInterval.Daily;

        var candles = await db.PriceCandles
            .Where(c => c.StockId == stock.Id
                && c.Timestamp >= query.StartDate
                && c.Timestamp <= query.EndDate
                && c.Interval == interval)
            .OrderBy(c => c.Timestamp)
            .Select(c => new CandleDto(
                c.Open, c.High, c.Low, c.Close,
                c.Volume, c.Timestamp, c.Interval.ToString()))
            .ToListAsync();

        return candles;
    }
}
