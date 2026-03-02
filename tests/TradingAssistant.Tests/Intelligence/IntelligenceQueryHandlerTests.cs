using TradingAssistant.Application.Handlers.Intelligence;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.Infrastructure.Persistence;
using TradingAssistant.Tests.Helpers;

namespace TradingAssistant.Tests.Intelligence;

public class GetCurrentRegimeHandlerTests
{
    private IntelligenceDbContext CreateDb() => TestIntelligenceDbContextFactory.Create();

    [Fact]
    public async Task ReturnsLatestRegime()
    {
        await using var db = CreateDb();

        db.MarketRegimes.Add(new MarketRegime
        {
            MarketCode = "US_SP500", CurrentRegime = RegimeType.Sideways,
            ClassifiedAt = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            RegimeStartDate = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            RegimeDuration = 17, ConfidenceScore = 0.65m
        });
        db.MarketRegimes.Add(new MarketRegime
        {
            MarketCode = "US_SP500", CurrentRegime = RegimeType.Bull,
            ClassifiedAt = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            RegimeStartDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            RegimeDuration = 28, ConfidenceScore = 0.85m,
            SmaSlope50 = 0.003m, SmaSlope200 = 0.001m, VixLevel = 15m,
            BreadthScore = 72m, PctAbove200Sma = 0.68m, AdvanceDeclineRatio = 1.8m
        });
        await db.SaveChangesAsync();

        var dto = await GetCurrentRegimeHandler.HandleAsync(
            new GetCurrentRegimeQuery("US_SP500"), db);

        Assert.Equal("Bull", dto.CurrentRegime);
        Assert.Equal(0.85m, dto.ConfidenceScore);
        Assert.Equal(28, dto.RegimeDuration);
        Assert.Equal(0.003m, dto.SmaSlope50);
        Assert.Equal(15m, dto.VixLevel);
    }

    [Fact]
    public async Task NotFound_Throws()
    {
        await using var db = CreateDb();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => GetCurrentRegimeHandler.HandleAsync(
                new GetCurrentRegimeQuery("NONEXISTENT"), db));
    }

    [Fact]
    public async Task ReturnsCorrectMarketOnly()
    {
        await using var db = CreateDb();

        db.MarketRegimes.Add(new MarketRegime
        {
            MarketCode = "US_SP500", CurrentRegime = RegimeType.Bull,
            ClassifiedAt = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        db.MarketRegimes.Add(new MarketRegime
        {
            MarketCode = "IN_NIFTY50", CurrentRegime = RegimeType.Bear,
            ClassifiedAt = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        await db.SaveChangesAsync();

        var dto = await GetCurrentRegimeHandler.HandleAsync(
            new GetCurrentRegimeQuery("IN_NIFTY50"), db);

        Assert.Equal("Bear", dto.CurrentRegime);
        Assert.Equal("IN_NIFTY50", dto.MarketCode);
    }
}

public class GetRegimeHistoryHandlerTests
{
    private IntelligenceDbContext CreateDb() => TestIntelligenceDbContextFactory.Create();

    [Fact]
    public async Task ReturnsPaginatedHistory()
    {
        await using var db = CreateDb();

        for (int i = 0; i < 5; i++)
        {
            db.MarketRegimes.Add(new MarketRegime
            {
                MarketCode = "US_SP500", CurrentRegime = RegimeType.Bull,
                ClassifiedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i)
            });
        }
        await db.SaveChangesAsync();

        var result = await GetRegimeHistoryHandler.HandleAsync(
            new GetRegimeHistoryQuery("US_SP500", Page: 1, PageSize: 2), db);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.True(result.HasNextPage);
        Assert.False(result.HasPreviousPage);
    }

    [Fact]
    public async Task ReturnsOrderedByClassifiedAtDescending()
    {
        await using var db = CreateDb();

        db.MarketRegimes.Add(new MarketRegime
        {
            MarketCode = "US_SP500", CurrentRegime = RegimeType.Sideways,
            ClassifiedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        db.MarketRegimes.Add(new MarketRegime
        {
            MarketCode = "US_SP500", CurrentRegime = RegimeType.Bull,
            ClassifiedAt = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        await db.SaveChangesAsync();

        var result = await GetRegimeHistoryHandler.HandleAsync(
            new GetRegimeHistoryQuery("US_SP500"), db);

        Assert.Equal("Bull", result.Items[0].CurrentRegime);
        Assert.Equal("Sideways", result.Items[1].CurrentRegime);
    }

    [Fact]
    public async Task EmptyHistory_ReturnsEmptyPage()
    {
        await using var db = CreateDb();

        var result = await GetRegimeHistoryHandler.HandleAsync(
            new GetRegimeHistoryQuery("NONEXISTENT"), db);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task FiltersToCorrectMarket()
    {
        await using var db = CreateDb();

        db.MarketRegimes.Add(new MarketRegime
        {
            MarketCode = "US_SP500", CurrentRegime = RegimeType.Bull,
            ClassifiedAt = DateTime.UtcNow
        });
        db.MarketRegimes.Add(new MarketRegime
        {
            MarketCode = "IN_NIFTY50", CurrentRegime = RegimeType.Bear,
            ClassifiedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await GetRegimeHistoryHandler.HandleAsync(
            new GetRegimeHistoryQuery("US_SP500"), db);

        Assert.Single(result.Items);
        Assert.Equal("US_SP500", result.Items[0].MarketCode);
    }

    [Fact]
    public async Task SecondPage_ReturnsRemainingItems()
    {
        await using var db = CreateDb();

        for (int i = 0; i < 5; i++)
        {
            db.MarketRegimes.Add(new MarketRegime
            {
                MarketCode = "US_SP500", CurrentRegime = RegimeType.Bull,
                ClassifiedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i)
            });
        }
        await db.SaveChangesAsync();

        var result = await GetRegimeHistoryHandler.HandleAsync(
            new GetRegimeHistoryQuery("US_SP500", Page: 2, PageSize: 2), db);

        Assert.Equal(2, result.Items.Count);
        Assert.True(result.HasPreviousPage);
        Assert.True(result.HasNextPage);
    }
}

public class GetLatestBreadthHandlerTests
{
    private IntelligenceDbContext CreateDb() => TestIntelligenceDbContextFactory.Create();

    [Fact]
    public async Task ReturnsLatestSnapshot()
    {
        await using var db = CreateDb();

        db.BreadthSnapshots.Add(new BreadthSnapshot
        {
            MarketCode = "US_SP500",
            SnapshotDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            AdvanceDeclineRatio = 1.5m, PctAbove200Sma = 0.55m, PctAbove50Sma = 0.60m,
            NewHighs = 30, NewLows = 10, TotalStocks = 500, Advancing = 300, Declining = 200
        });
        db.BreadthSnapshots.Add(new BreadthSnapshot
        {
            MarketCode = "US_SP500",
            SnapshotDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            AdvanceDeclineRatio = 1.8m, PctAbove200Sma = 0.68m, PctAbove50Sma = 0.72m,
            NewHighs = 45, NewLows = 8, TotalStocks = 500, Advancing = 340, Declining = 160
        });
        await db.SaveChangesAsync();

        var dto = await GetLatestBreadthHandler.HandleAsync(
            new GetLatestBreadthQuery("US_SP500"), db);

        Assert.Equal("US_SP500", dto.MarketCode);
        Assert.Equal(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), dto.SnapshotDate);
        Assert.Equal(1.8m, dto.AdvanceDeclineRatio);
        Assert.Equal(0.68m, dto.PctAbove200Sma);
        Assert.Equal(0.72m, dto.PctAbove50Sma);
        Assert.Equal(45, dto.NewHighs);
        Assert.Equal(8, dto.NewLows);
        Assert.Equal(500, dto.TotalStocks);
        Assert.Equal(340, dto.Advancing);
        Assert.Equal(160, dto.Declining);
    }

    [Fact]
    public async Task NotFound_Throws()
    {
        await using var db = CreateDb();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => GetLatestBreadthHandler.HandleAsync(
                new GetLatestBreadthQuery("NONEXISTENT"), db));
    }

    [Fact]
    public async Task FiltersToCorrectMarket()
    {
        await using var db = CreateDb();

        db.BreadthSnapshots.Add(new BreadthSnapshot
        {
            MarketCode = "US_SP500",
            SnapshotDate = DateTime.UtcNow,
            AdvanceDeclineRatio = 1.8m, PctAbove200Sma = 0.68m
        });
        db.BreadthSnapshots.Add(new BreadthSnapshot
        {
            MarketCode = "IN_NIFTY50",
            SnapshotDate = DateTime.UtcNow,
            AdvanceDeclineRatio = 1.2m, PctAbove200Sma = 0.45m
        });
        await db.SaveChangesAsync();

        var dto = await GetLatestBreadthHandler.HandleAsync(
            new GetLatestBreadthQuery("IN_NIFTY50"), db);

        Assert.Equal("IN_NIFTY50", dto.MarketCode);
        Assert.Equal(1.2m, dto.AdvanceDeclineRatio);
    }
}

public class GetCorrelationMatrixHandlerTests
{
    private IntelligenceDbContext CreateDb() => TestIntelligenceDbContextFactory.Create();

    [Fact]
    public async Task ReturnsLatestMatrix()
    {
        await using var db = CreateDb();

        db.CorrelationSnapshots.Add(new CorrelationSnapshot
        {
            SnapshotDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            LookbackDays = 60,
            MatrixJson = """{"US_SP500|IN_NIFTY50":0.40}"""
        });
        db.CorrelationSnapshots.Add(new CorrelationSnapshot
        {
            SnapshotDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            LookbackDays = 60,
            MatrixJson = """{"US_SP500|IN_NIFTY50":0.45,"US_SP500|UK_FTSE100":0.78}"""
        });
        await db.SaveChangesAsync();

        var dto = await GetCorrelationMatrixHandler.HandleAsync(
            new GetCorrelationMatrixQuery(), db);

        Assert.Equal(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), dto.SnapshotDate);
        Assert.Equal(60, dto.LookbackDays);
        Assert.Contains("0.45", dto.MatrixJson);
        Assert.Contains("UK_FTSE100", dto.MatrixJson);
    }

    [Fact]
    public async Task NoData_Throws()
    {
        await using var db = CreateDb();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => GetCorrelationMatrixHandler.HandleAsync(
                new GetCorrelationMatrixQuery(), db));
    }
}
