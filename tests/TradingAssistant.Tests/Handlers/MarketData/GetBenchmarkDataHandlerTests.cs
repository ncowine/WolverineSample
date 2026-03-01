using TradingAssistant.Application.Handlers.MarketData;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Domain.MarketData;
using TradingAssistant.Tests.Helpers;

namespace TradingAssistant.Tests.Handlers.MarketData;

public class GetBenchmarkDataHandlerTests
{
    [Fact]
    public async Task Returns_spy_daily_candles_in_date_range()
    {
        using var db = TestMarketDataDbContextFactory.Create();
        var spy = new Stock { Symbol = "SPY", Name = "SPDR S&P 500 ETF", Exchange = "ARCA", Sector = "ETF" };
        db.Stocks.Add(spy);

        db.PriceCandles.Add(new PriceCandle
        {
            StockId = spy.Id, Open = 450, High = 455, Low = 448, Close = 453,
            Volume = 80_000_000, Timestamp = new DateTime(2025, 1, 6), Interval = CandleInterval.Daily
        });
        db.PriceCandles.Add(new PriceCandle
        {
            StockId = spy.Id, Open = 453, High = 460, Low = 451, Close = 458,
            Volume = 75_000_000, Timestamp = new DateTime(2025, 1, 7), Interval = CandleInterval.Daily
        });
        db.PriceCandles.Add(new PriceCandle
        {
            StockId = spy.Id, Open = 458, High = 462, Low = 455, Close = 460,
            Volume = 70_000_000, Timestamp = new DateTime(2025, 1, 8), Interval = CandleInterval.Daily
        });
        await db.SaveChangesAsync();

        var query = new GetBenchmarkDataQuery(new DateTime(2025, 1, 6), new DateTime(2025, 1, 7));
        var result = await GetBenchmarkDataHandler.HandleAsync(query, db);

        Assert.Equal(2, result.Count);
        Assert.Equal(450m, result[0].Open);
        Assert.Equal(458m, result[1].Close);
    }

    [Fact]
    public async Task Returns_candles_sorted_by_date()
    {
        using var db = TestMarketDataDbContextFactory.Create();
        var spy = new Stock { Symbol = "SPY", Name = "SPY ETF", Exchange = "ARCA", Sector = "ETF" };
        db.Stocks.Add(spy);

        // Insert out of order
        db.PriceCandles.Add(new PriceCandle
        {
            StockId = spy.Id, Open = 460, High = 465, Low = 458, Close = 462,
            Volume = 70_000_000, Timestamp = new DateTime(2025, 1, 8), Interval = CandleInterval.Daily
        });
        db.PriceCandles.Add(new PriceCandle
        {
            StockId = spy.Id, Open = 450, High = 455, Low = 448, Close = 453,
            Volume = 80_000_000, Timestamp = new DateTime(2025, 1, 6), Interval = CandleInterval.Daily
        });
        await db.SaveChangesAsync();

        var query = new GetBenchmarkDataQuery(new DateTime(2025, 1, 1), new DateTime(2025, 1, 31));
        var result = await GetBenchmarkDataHandler.HandleAsync(query, db);

        Assert.Equal(new DateTime(2025, 1, 6), result[0].Timestamp);
        Assert.Equal(new DateTime(2025, 1, 8), result[1].Timestamp);
    }

    [Fact]
    public async Task Throws_when_spy_not_found()
    {
        using var db = TestMarketDataDbContextFactory.Create();

        var query = new GetBenchmarkDataQuery(new DateTime(2025, 1, 1), new DateTime(2025, 1, 31));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => GetBenchmarkDataHandler.HandleAsync(query, db));

        Assert.Contains("SPY", ex.Message);
    }

    [Fact]
    public async Task Excludes_weekly_candles()
    {
        using var db = TestMarketDataDbContextFactory.Create();
        var spy = new Stock { Symbol = "SPY", Name = "SPY ETF", Exchange = "ARCA", Sector = "ETF" };
        db.Stocks.Add(spy);

        db.PriceCandles.Add(new PriceCandle
        {
            StockId = spy.Id, Open = 450, High = 455, Low = 448, Close = 453,
            Volume = 80_000_000, Timestamp = new DateTime(2025, 1, 6), Interval = CandleInterval.Daily
        });
        db.PriceCandles.Add(new PriceCandle
        {
            StockId = spy.Id, Open = 450, High = 460, Low = 445, Close = 458,
            Volume = 400_000_000, Timestamp = new DateTime(2025, 1, 6), Interval = CandleInterval.Weekly
        });
        await db.SaveChangesAsync();

        var query = new GetBenchmarkDataQuery(new DateTime(2025, 1, 1), new DateTime(2025, 1, 31));
        var result = await GetBenchmarkDataHandler.HandleAsync(query, db);

        Assert.Single(result); // Only daily, not weekly
    }
}
