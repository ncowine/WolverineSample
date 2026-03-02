using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application.Handlers.Intelligence;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.Domain.MarketData;
using TradingAssistant.Infrastructure.Persistence;
using TradingAssistant.Tests.Helpers;

namespace TradingAssistant.Tests.Intelligence;

public class ClassifyRegimeHandlerTests
{
    private static readonly DateTime ClassificationDate = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Seed daily candles for a stock with a given trend (rising/falling/flat).
    /// </summary>
    private static Stock SeedStock(
        MarketDataDbContext db,
        string symbol,
        int candleCount,
        decimal startPrice,
        decimal dailyChange)
    {
        var stock = new Stock { Symbol = symbol, Name = symbol, Exchange = "TEST", Sector = "Test" };
        db.Stocks.Add(stock);

        var date = ClassificationDate.AddDays(-candleCount);
        for (var i = 0; i < candleCount; i++)
        {
            while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                date = date.AddDays(1);

            var close = startPrice + dailyChange * i;
            db.PriceCandles.Add(new PriceCandle
            {
                StockId = stock.Id,
                Open = close - dailyChange / 2,
                High = close + 1m,
                Low = close - 1m,
                Close = close,
                Volume = 1_000_000,
                Timestamp = date,
                Interval = CandleInterval.Daily
            });

            date = date.AddDays(1);
        }

        return stock;
    }

    [Fact]
    public async Task ClassifiesRegime_WithBreadthAndPriceData()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var marketDb = TestMarketDataDbContextFactory.Create(dbName + "_market");
        await using var intelligenceDb = TestIntelligenceDbContextFactory.Create(dbName + "_intel");

        // Seed rising stocks → should produce bull-leaning SMA slopes
        SeedStock(marketDb, "AAPL", 250, 100m, 0.3m);
        SeedStock(marketDb, "MSFT", 250, 200m, 0.2m);
        SeedStock(marketDb, "GOOGL", 250, 150m, 0.25m);
        await marketDb.SaveChangesAsync();

        // Seed breadth snapshot with strong breadth
        intelligenceDb.BreadthSnapshots.Add(new BreadthSnapshot
        {
            MarketCode = "US_SP500",
            SnapshotDate = ClassificationDate,
            PctAbove200Sma = 0.75m,
            PctAbove50Sma = 0.80m,
            AdvanceDeclineRatio = 2.0m,
            TotalStocks = 3,
            Advancing = 3,
            Declining = 0,
            NewHighs = 3,
            NewLows = 0
        });

        // Seed market profile with US thresholds
        intelligenceDb.MarketProfiles.Add(new MarketProfile
        {
            MarketCode = "US_SP500",
            Exchange = "NYSE",
            ConfigJson = """{"regimeThresholds":{"highVol":30,"bullBreadth":0.60,"bearBreadth":0.40}}"""
        });

        await intelligenceDb.SaveChangesAsync();

        var command = new ClassifyRegimeCommand("US_SP500", ClassificationDate);
        var (regimeChanged, dto) = await ClassifyRegimeHandler.HandleAsync(command, marketDb, intelligenceDb);

        Assert.NotNull(dto);
        Assert.Equal("US_SP500", dto.MarketCode);
        Assert.True(dto.ConfidenceScore > 0);

        // Should have persisted a MarketRegime record
        var saved = await intelligenceDb.MarketRegimes.FirstAsync();
        Assert.Equal("US_SP500", saved.MarketCode);
        Assert.Equal(ClassificationDate, saved.ClassifiedAt);
    }

    [Fact]
    public async Task DetectsRegimeTransition_PublishesEvent()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var marketDb = TestMarketDataDbContextFactory.Create(dbName + "_market");
        await using var intelligenceDb = TestIntelligenceDbContextFactory.Create(dbName + "_intel");

        // Seed falling stocks (bear-like SMA slopes)
        SeedStock(marketDb, "AAPL", 250, 500m, -0.3m);
        SeedStock(marketDb, "MSFT", 250, 600m, -0.2m);
        await marketDb.SaveChangesAsync();

        // Previous regime was Bull
        intelligenceDb.MarketRegimes.Add(new MarketRegime
        {
            MarketCode = "US_SP500",
            CurrentRegime = RegimeType.Bull,
            RegimeStartDate = ClassificationDate.AddDays(-60),
            ClassifiedAt = ClassificationDate.AddDays(-1),
            ConfidenceScore = 0.80m
        });

        // Current breadth is weak → should push toward Bear/Sideways
        intelligenceDb.BreadthSnapshots.Add(new BreadthSnapshot
        {
            MarketCode = "US_SP500",
            SnapshotDate = ClassificationDate,
            PctAbove200Sma = 0.25m,
            PctAbove50Sma = 0.20m,
            AdvanceDeclineRatio = 0.5m,
            TotalStocks = 2,
            Advancing = 0,
            Declining = 2
        });

        await intelligenceDb.SaveChangesAsync();

        var command = new ClassifyRegimeCommand("US_SP500", ClassificationDate);
        var (regimeChanged, dto) = await ClassifyRegimeHandler.HandleAsync(command, marketDb, intelligenceDb);

        // Should detect transition from Bull to Bear (or Sideways)
        Assert.NotNull(regimeChanged);
        Assert.Equal("Bull", regimeChanged.FromRegime);
        Assert.Equal("US_SP500", regimeChanged.MarketCode);
        Assert.Equal(ClassificationDate, regimeChanged.TransitionDate);

        // Transition should be persisted
        var transition = await intelligenceDb.RegimeTransitions.FirstAsync();
        Assert.Equal(RegimeType.Bull, transition.FromRegime);
        Assert.Equal("US_SP500", transition.MarketCode);
    }

    [Fact]
    public async Task NoTransition_WhenRegimeSame_ReturnsNullEvent()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var marketDb = TestMarketDataDbContextFactory.Create(dbName + "_market");
        await using var intelligenceDb = TestIntelligenceDbContextFactory.Create(dbName + "_intel");

        // No stocks → neutral classification
        // Previous regime was Sideways
        intelligenceDb.MarketRegimes.Add(new MarketRegime
        {
            MarketCode = "US_SP500",
            CurrentRegime = RegimeType.Sideways,
            RegimeStartDate = ClassificationDate.AddDays(-30),
            ClassifiedAt = ClassificationDate.AddDays(-1),
            ConfidenceScore = 0.60m
        });
        await intelligenceDb.SaveChangesAsync();

        var command = new ClassifyRegimeCommand("US_SP500", ClassificationDate);
        var (regimeChanged, dto) = await ClassifyRegimeHandler.HandleAsync(command, marketDb, intelligenceDb);

        // No data → defaults to Sideways → same as previous → no transition
        Assert.Null(regimeChanged);
        Assert.Equal("Sideways", dto.CurrentRegime);

        // Should NOT have any transition records
        Assert.False(await intelligenceDb.RegimeTransitions.AnyAsync());
    }

    [Fact]
    public async Task FirstClassification_NoTransitionEvent()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var marketDb = TestMarketDataDbContextFactory.Create(dbName + "_market");
        await using var intelligenceDb = TestIntelligenceDbContextFactory.Create(dbName + "_intel");

        // No previous regime exists
        var command = new ClassifyRegimeCommand("US_SP500", ClassificationDate);
        var (regimeChanged, dto) = await ClassifyRegimeHandler.HandleAsync(command, marketDb, intelligenceDb);

        Assert.Null(regimeChanged); // First classification → no transition
        Assert.NotNull(dto);
        Assert.Equal("US_SP500", dto.MarketCode);
    }

    [Fact]
    public async Task UsesMarketProfileThresholds()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var marketDb = TestMarketDataDbContextFactory.Create(dbName + "_market");
        await using var intelligenceDb = TestIntelligenceDbContextFactory.Create(dbName + "_intel");

        // India profile with lower thresholds
        intelligenceDb.MarketProfiles.Add(new MarketProfile
        {
            MarketCode = "IN_NIFTY50",
            Exchange = "NSE",
            VixSymbol = "^INDIAVIX",
            ConfigJson = """{"regimeThresholds":{"highVol":25,"bullBreadth":0.55,"bearBreadth":0.35}}"""
        });

        // VIX stock at 27 (above India's 25 but below US's 30)
        var vixStock = new Stock { Symbol = "^INDIAVIX", Name = "India VIX", Exchange = "NSE" };
        marketDb.Stocks.Add(vixStock);
        marketDb.PriceCandles.Add(new PriceCandle
        {
            StockId = vixStock.Id,
            Open = 26m,
            High = 28m,
            Low = 25m,
            Close = 27m,
            Volume = 0,
            Timestamp = ClassificationDate,
            Interval = CandleInterval.Daily
        });
        await marketDb.SaveChangesAsync();
        await intelligenceDb.SaveChangesAsync();

        var command = new ClassifyRegimeCommand("IN_NIFTY50", ClassificationDate);
        var (_, dto) = await ClassifyRegimeHandler.HandleAsync(command, marketDb, intelligenceDb);

        // VIX 27 > India threshold 25 → HighVolatility
        Assert.Equal("HighVolatility", dto.CurrentRegime);
        Assert.Equal(27m, dto.VixLevel);
    }

    [Fact]
    public async Task UsesLatestDateByDefault_WhenDateIsNull()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var marketDb = TestMarketDataDbContextFactory.Create(dbName + "_market");
        await using var intelligenceDb = TestIntelligenceDbContextFactory.Create(dbName + "_intel");

        var command = new ClassifyRegimeCommand("US_SP500"); // no date
        var (_, dto) = await ClassifyRegimeHandler.HandleAsync(command, marketDb, intelligenceDb);

        Assert.NotNull(dto);
        Assert.Equal(DateTime.UtcNow.Date, dto.ClassifiedAt);
    }

    [Fact]
    public async Task RegimeStartDate_CarriedForward_WhenNoTransition()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var marketDb = TestMarketDataDbContextFactory.Create(dbName + "_market");
        await using var intelligenceDb = TestIntelligenceDbContextFactory.Create(dbName + "_intel");

        var originalStart = ClassificationDate.AddDays(-45);

        intelligenceDb.MarketRegimes.Add(new MarketRegime
        {
            MarketCode = "US_SP500",
            CurrentRegime = RegimeType.Sideways,
            RegimeStartDate = originalStart,
            ClassifiedAt = ClassificationDate.AddDays(-1),
            ConfidenceScore = 0.60m
        });
        await intelligenceDb.SaveChangesAsync();

        var command = new ClassifyRegimeCommand("US_SP500", ClassificationDate);
        var (_, dto) = await ClassifyRegimeHandler.HandleAsync(command, marketDb, intelligenceDb);

        // Should carry forward the original start date since regime is still Sideways
        Assert.Equal("Sideways", dto.CurrentRegime);
        Assert.Equal(originalStart, dto.RegimeStartDate);
        Assert.Equal(45, dto.RegimeDuration);
    }
}
