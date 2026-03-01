using Microsoft.Extensions.Logging.Abstractions;
using TradingAssistant.Application.Handlers.MarketData;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.MarketData;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Domain.MarketData;
using TradingAssistant.Tests.Helpers;

namespace TradingAssistant.Tests.Handlers.MarketData;

public class FetchMarketDataHandlerTests
{
    private readonly FakeMarketDataProvider _provider = new();
    private readonly NullLogger<FetchMarketDataHandler> _logger = new();

    [Fact]
    public async Task Creates_stock_if_not_exists_and_stores_candles()
    {
        using var db = TestMarketDataDbContextFactory.Create();
        _provider.SetCandles("AAPL", GenerateCandles(5));

        var command = new FetchMarketDataCommand("AAPL",
            DateTime.UtcNow.AddDays(-10), DateTime.UtcNow);

        var result = await FetchMarketDataHandler.HandleAsync(command, _provider, db, _logger);

        Assert.Equal("AAPL", result.Symbol);
        Assert.Equal(5, result.CandlesFetched);
        Assert.Equal(5, result.CandlesStored);
        Assert.Single(db.Stocks);
        Assert.Equal(5, db.PriceCandles.Count());
    }

    [Fact]
    public async Task Uppercases_symbol()
    {
        using var db = TestMarketDataDbContextFactory.Create();
        _provider.SetCandles("MSFT", GenerateCandles(1));

        var command = new FetchMarketDataCommand("msft");
        var result = await FetchMarketDataHandler.HandleAsync(command, _provider, db, _logger);

        Assert.Equal("MSFT", result.Symbol);
    }

    [Fact]
    public async Task Skips_duplicate_candles()
    {
        using var db = TestMarketDataDbContextFactory.Create();
        var stock = new Stock { Symbol = "AAPL", Name = "Apple", Exchange = "NASDAQ", Sector = "Tech" };
        db.Stocks.Add(stock);

        // Pre-insert a candle for today
        var today = DateTime.UtcNow.Date;
        db.PriceCandles.Add(new PriceCandle
        {
            StockId = stock.Id, Open = 100, High = 105, Low = 99, Close = 103,
            Volume = 1000, Timestamp = today, Interval = CandleInterval.Daily
        });
        await db.SaveChangesAsync();

        // Provider returns candles including today (duplicate)
        _provider.SetCandles("AAPL", new List<MarketCandle>
        {
            new(today, 100, 105, 99, 103, 103, 1000),
            new(today.AddDays(-1), 98, 102, 97, 100, 100, 900),
        });

        var command = new FetchMarketDataCommand("AAPL", today.AddDays(-5), today);
        var result = await FetchMarketDataHandler.HandleAsync(command, _provider, db, _logger);

        Assert.Equal(2, result.CandlesFetched);
        Assert.Equal(1, result.CandlesStored); // Only the non-duplicate
        Assert.Equal(2, db.PriceCandles.Count()); // 1 existing + 1 new
    }

    [Fact]
    public async Task Returns_no_data_message_when_provider_returns_empty()
    {
        using var db = TestMarketDataDbContextFactory.Create();
        _provider.SetCandles("XYZ", new List<MarketCandle>());

        var command = new FetchMarketDataCommand("XYZ");
        var result = await FetchMarketDataHandler.HandleAsync(command, _provider, db, _logger);

        Assert.Equal(0, result.CandlesFetched);
        Assert.Equal(0, result.CandlesStored);
        Assert.Contains("No data", result.Message);
    }

    [Fact]
    public async Task Uses_adjusted_close_as_canonical_close()
    {
        using var db = TestMarketDataDbContextFactory.Create();
        _provider.SetCandles("AAPL", new List<MarketCandle>
        {
            new(DateTime.UtcNow.Date, Open: 100, High: 110, Low: 95,
                Close: 105, AdjustedClose: 52.50m, Volume: 5000)
        });

        var command = new FetchMarketDataCommand("AAPL");
        await FetchMarketDataHandler.HandleAsync(command, _provider, db, _logger);

        var stored = db.PriceCandles.Single();
        Assert.Equal(52.50m, stored.Close); // Adjusted close, not raw close
    }

    [Fact]
    public async Task Defaults_to_5_years_range()
    {
        using var db = TestMarketDataDbContextFactory.Create();
        _provider.SetCandles("SPY", GenerateCandles(1));

        var command = new FetchMarketDataCommand("SPY"); // No From/To
        await FetchMarketDataHandler.HandleAsync(command, _provider, db, _logger);

        var (from, to) = _provider.LastRequest;
        Assert.True((DateTime.UtcNow.Date - from).TotalDays > 1800); // ~5 years
        Assert.Equal(DateTime.UtcNow.Date, to);
    }

    [Fact]
    public async Task Reuses_existing_stock_entry()
    {
        using var db = TestMarketDataDbContextFactory.Create();
        var stock = new Stock { Symbol = "TSLA", Name = "Tesla Inc.", Exchange = "NASDAQ", Sector = "Auto" };
        db.Stocks.Add(stock);
        await db.SaveChangesAsync();

        _provider.SetCandles("TSLA", GenerateCandles(2));

        var command = new FetchMarketDataCommand("TSLA");
        await FetchMarketDataHandler.HandleAsync(command, _provider, db, _logger);

        Assert.Single(db.Stocks); // Didn't create a duplicate
        Assert.Equal("Tesla Inc.", db.Stocks.Single().Name); // Preserved existing name
    }

    private static List<MarketCandle> GenerateCandles(int count)
    {
        var candles = new List<MarketCandle>();
        for (var i = 0; i < count; i++)
        {
            var date = DateTime.UtcNow.Date.AddDays(-i);
            candles.Add(new MarketCandle(date, 100 + i, 105 + i, 95 + i, 103 + i, 103 + i, 1000 + i));
        }
        return candles;
    }
}

/// <summary>
/// Fake implementation of IMarketDataProvider for testing.
/// </summary>
internal class FakeMarketDataProvider : IMarketDataProvider
{
    private readonly Dictionary<string, IReadOnlyList<MarketCandle>> _data = new();
    public (DateTime from, DateTime to) LastRequest { get; private set; }

    public void SetCandles(string symbol, IReadOnlyList<MarketCandle> candles)
    {
        _data[symbol.ToUpperInvariant()] = candles;
    }

    public Task<IReadOnlyList<MarketCandle>> GetDailyCandlesAsync(
        string symbol, DateTime from, DateTime to, CancellationToken ct = default)
    {
        LastRequest = (from, to);
        var key = symbol.ToUpperInvariant();
        IReadOnlyList<MarketCandle> result = _data.TryGetValue(key, out var candles)
            ? candles
            : Array.Empty<MarketCandle>();
        return Task.FromResult(result);
    }
}
