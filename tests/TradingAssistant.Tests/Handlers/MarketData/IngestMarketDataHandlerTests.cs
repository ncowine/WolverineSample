using Microsoft.Extensions.Logging.Abstractions;
using TradingAssistant.Application.Handlers.MarketData;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.MarketData;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Domain.MarketData;
using TradingAssistant.Tests.Helpers;

namespace TradingAssistant.Tests.Handlers.MarketData;

public class IngestMarketDataHandlerTests
{
    private readonly FakeMarketDataProvider _provider = new();
    private readonly NullLogger<IngestMarketDataHandler> _logger = new();

    [Fact]
    public async Task Ingests_daily_weekly_and_monthly_candles()
    {
        using var db = TestMarketDataDbContextFactory.Create();

        // 10 trading days across 2 weeks in January 2025
        var candles = new List<MarketCandle>();
        // Week 1: Jan 6-10
        candles.Add(new MarketCandle(new DateTime(2025, 1, 6), 100, 110, 95, 105, 105, 1000));
        candles.Add(new MarketCandle(new DateTime(2025, 1, 7), 105, 112, 100, 108, 108, 1100));
        candles.Add(new MarketCandle(new DateTime(2025, 1, 8), 108, 115, 103, 110, 110, 900));
        candles.Add(new MarketCandle(new DateTime(2025, 1, 9), 110, 118, 107, 115, 115, 1500));
        candles.Add(new MarketCandle(new DateTime(2025, 1, 10), 115, 120, 112, 118, 118, 1100));
        // Week 2: Jan 13-17
        candles.Add(new MarketCandle(new DateTime(2025, 1, 13), 118, 125, 115, 122, 122, 1300));
        candles.Add(new MarketCandle(new DateTime(2025, 1, 14), 122, 128, 118, 125, 125, 1400));
        candles.Add(new MarketCandle(new DateTime(2025, 1, 15), 125, 130, 120, 127, 127, 1000));
        candles.Add(new MarketCandle(new DateTime(2025, 1, 16), 127, 135, 124, 132, 132, 1600));
        candles.Add(new MarketCandle(new DateTime(2025, 1, 17), 132, 138, 128, 135, 135, 1200));

        _provider.SetCandles("AAPL", candles);

        var command = new IngestMarketDataCommand("AAPL", YearsBack: 5);
        var result = await IngestMarketDataHandler.HandleAsync(command, _provider, db, _logger);

        Assert.Equal("AAPL", result.Symbol);
        Assert.Equal(10, result.DailyCandlesStored);
        Assert.Equal(2, result.WeeklyCandlesStored);  // 2 ISO weeks
        Assert.Equal(1, result.MonthlyCandlesStored); // 1 calendar month

        // Verify all stored in DB
        var dailyCount = db.PriceCandles.Count(c => c.Interval == CandleInterval.Daily);
        var weeklyCount = db.PriceCandles.Count(c => c.Interval == CandleInterval.Weekly);
        var monthlyCount = db.PriceCandles.Count(c => c.Interval == CandleInterval.Monthly);

        Assert.Equal(10, dailyCount);
        Assert.Equal(2, weeklyCount);
        Assert.Equal(1, monthlyCount);
    }

    [Fact]
    public async Task Deduplicates_daily_candles()
    {
        using var db = TestMarketDataDbContextFactory.Create();
        var stock = new Stock { Symbol = "MSFT", Name = "Microsoft", Exchange = "NASDAQ", Sector = "Tech" };
        db.Stocks.Add(stock);

        // Use recent dates that fall within the handler's computed range (UtcNow - YearsBack)
        var recentDate = DateTime.UtcNow.Date.AddDays(-10);
        var nextDate = recentDate.AddDays(1);

        // Pre-insert a daily candle
        db.PriceCandles.Add(new PriceCandle
        {
            StockId = stock.Id, Open = 100, High = 110, Low = 95, Close = 105,
            Volume = 1000, Timestamp = recentDate, Interval = CandleInterval.Daily
        });
        await db.SaveChangesAsync();

        // Provider returns the same date + a new one
        _provider.SetCandles("MSFT", new List<MarketCandle>
        {
            new(recentDate, 100, 110, 95, 105, 105, 1000), // duplicate
            new(nextDate, 105, 112, 100, 108, 108, 1100),   // new
        });

        var command = new IngestMarketDataCommand("MSFT", YearsBack: 5);
        var result = await IngestMarketDataHandler.HandleAsync(command, _provider, db, _logger);

        Assert.Equal(1, result.DailyCandlesStored); // Only the new one
        Assert.Equal(2, db.PriceCandles.Count(c => c.Interval == CandleInterval.Daily));
    }

    [Fact]
    public async Task Replaces_weekly_and_monthly_on_re_ingestion()
    {
        using var db = TestMarketDataDbContextFactory.Create();

        var candles = new List<MarketCandle>
        {
            new(new DateTime(2025, 1, 6), 100, 110, 95, 105, 105, 1000),
            new(new DateTime(2025, 1, 7), 105, 112, 100, 108, 108, 1100),
        };
        _provider.SetCandles("GOOG", candles);

        // Ingest once
        var command = new IngestMarketDataCommand("GOOG", YearsBack: 5);
        await IngestMarketDataHandler.HandleAsync(command, _provider, db, _logger);

        var weeklyBefore = db.PriceCandles.Count(c => c.Interval == CandleInterval.Weekly);
        var monthlyBefore = db.PriceCandles.Count(c => c.Interval == CandleInterval.Monthly);

        // Ingest again (daily deduped, weekly/monthly replaced)
        await IngestMarketDataHandler.HandleAsync(command, _provider, db, _logger);

        var weeklyAfter = db.PriceCandles.Count(c => c.Interval == CandleInterval.Weekly);
        var monthlyAfter = db.PriceCandles.Count(c => c.Interval == CandleInterval.Monthly);

        Assert.Equal(weeklyBefore, weeklyAfter);   // Same count, not doubled
        Assert.Equal(monthlyBefore, monthlyAfter);
    }

    [Fact]
    public async Task Creates_stock_if_not_exists()
    {
        using var db = TestMarketDataDbContextFactory.Create();
        _provider.SetCandles("NVDA", new List<MarketCandle>
        {
            new(new DateTime(2025, 1, 6), 100, 110, 95, 105, 105, 1000),
        });

        var command = new IngestMarketDataCommand("NVDA", YearsBack: 5);
        await IngestMarketDataHandler.HandleAsync(command, _provider, db, _logger);

        Assert.Single(db.Stocks);
        Assert.Equal("NVDA", db.Stocks.Single().Symbol);
    }

    [Fact]
    public async Task Returns_no_data_when_provider_empty()
    {
        using var db = TestMarketDataDbContextFactory.Create();
        _provider.SetCandles("XYZ", new List<MarketCandle>());

        var command = new IngestMarketDataCommand("XYZ");
        var result = await IngestMarketDataHandler.HandleAsync(command, _provider, db, _logger);

        Assert.Equal(0, result.DailyCandlesStored);
        Assert.Equal(0, result.WeeklyCandlesStored);
        Assert.Equal(0, result.MonthlyCandlesStored);
        Assert.Contains("No data", result.Message);
    }

    [Fact]
    public async Task Weekly_open_and_close_are_correct()
    {
        using var db = TestMarketDataDbContextFactory.Create();

        _provider.SetCandles("SPY", new List<MarketCandle>
        {
            new(new DateTime(2025, 1, 6), 100, 110, 95, 105, 105, 1000), // Mon
            new(new DateTime(2025, 1, 7), 105, 108, 102, 106, 106, 800), // Tue
            new(new DateTime(2025, 1, 10), 106, 112, 99, 109, 109, 1200), // Fri
        });

        var command = new IngestMarketDataCommand("SPY", YearsBack: 5);
        await IngestMarketDataHandler.HandleAsync(command, _provider, db, _logger);

        var weekly = db.PriceCandles.Single(c => c.Interval == CandleInterval.Weekly);
        Assert.Equal(100m, weekly.Open);  // Monday's open
        Assert.Equal(109m, weekly.Close); // Friday's close
        Assert.Equal(112m, weekly.High);  // Max across week
        Assert.Equal(95m, weekly.Low);    // Min across week
        Assert.Equal(3000, weekly.Volume); // Sum
    }

    [Fact]
    public async Task Monthly_timestamp_is_first_of_month()
    {
        using var db = TestMarketDataDbContextFactory.Create();

        _provider.SetCandles("AMZN", new List<MarketCandle>
        {
            new(new DateTime(2025, 3, 15), 100, 110, 95, 105, 105, 1000),
        });

        var command = new IngestMarketDataCommand("AMZN", YearsBack: 5);
        await IngestMarketDataHandler.HandleAsync(command, _provider, db, _logger);

        var monthly = db.PriceCandles.Single(c => c.Interval == CandleInterval.Monthly);
        Assert.Equal(new DateTime(2025, 3, 1), monthly.Timestamp);
    }
}
