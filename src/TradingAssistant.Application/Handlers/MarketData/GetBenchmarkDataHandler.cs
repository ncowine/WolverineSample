using Microsoft.EntityFrameworkCore;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.MarketData;

public class GetBenchmarkDataHandler
{
    public const string BenchmarkSymbol = "SPY";

    public static async Task<List<CandleDto>> HandleAsync(
        GetBenchmarkDataQuery query,
        MarketDataDbContext db)
    {
        var stock = await db.Stocks.FirstOrDefaultAsync(s => s.Symbol == BenchmarkSymbol)
            ?? throw new InvalidOperationException(
                $"Benchmark symbol {BenchmarkSymbol} not found. Ingest SPY data first.");

        var candles = await db.PriceCandles
            .Where(c => c.StockId == stock.Id
                && c.Interval == CandleInterval.Daily
                && c.Timestamp >= query.StartDate
                && c.Timestamp <= query.EndDate)
            .OrderBy(c => c.Timestamp)
            .Select(c => new CandleDto(c.Open, c.High, c.Low, c.Close, c.Volume, c.Timestamp, "Daily"))
            .ToListAsync();

        return candles;
    }
}
