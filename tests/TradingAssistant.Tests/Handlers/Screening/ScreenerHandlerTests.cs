using System.Text.Json;
using TradingAssistant.Application.Handlers.Screening;
using TradingAssistant.Application.Screening;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Domain.MarketData;
using TradingAssistant.Tests.Helpers;

namespace TradingAssistant.Tests.Handlers.Screening;

public class ScreenerHandlerTests
{
    // ── Helpers ───────────────────────────────────────────────

    private static ScreenerRun SeedRun(
        DateTime scanDate,
        string strategyName = "TestStrategy",
        List<ScreenerSignalDto>? signals = null,
        List<string>? warnings = null)
    {
        signals ??= new List<ScreenerSignalDto>
        {
            new("AAPL", "A", 92m, "Long", 150m, 144m, 162m, 3m, 65m,
                scanDate, new List<ScreenerBreakdownEntryDto>
                {
                    new("TrendAlignment", 100, 0.25m, 25m, "Trend aligns"),
                    new("Confirmations", 83.33m, 0.25m, 20.83m, "5/6 confirmations"),
                    new("Volume", 100, 0.15m, 15m, "Volume above 1.2x"),
                    new("RiskReward", 100, 0.15m, 15m, "R:R = 3.0"),
                    new("History", 65, 0.10m, 6.5m, "Historical win rate: 65%"),
                    new("Volatility", 100, 0.10m, 10m, "ATR normal")
                }),
            new("MSFT", "B", 78m, "Long", 400m, 388m, 424m, 2m, 55m,
                scanDate, new List<ScreenerBreakdownEntryDto>())
        };

        return new ScreenerRun
        {
            Id = Guid.NewGuid(),
            ScanDate = scanDate,
            StrategyName = strategyName,
            SymbolsScanned = 50,
            SignalsFound = 2,
            ResultsJson = JsonSerializer.Serialize(signals),
            WarningsJson = JsonSerializer.Serialize(warnings ?? new List<string>()),
            ElapsedTime = TimeSpan.FromSeconds(3),
            CreatedAt = DateTime.UtcNow
        };
    }

    // ── GetScreenerResults ───────────────────────────────────

    [Fact]
    public async Task GetResults_returns_latest_run()
    {
        await using var db = TestMarketDataDbContextFactory.Create();

        var older = SeedRun(new DateTime(2025, 6, 14), "OldStrategy");
        var latest = SeedRun(new DateTime(2025, 6, 15), "NewStrategy");
        db.ScreenerRuns.AddRange(older, latest);
        await db.SaveChangesAsync();

        var result = await GetScreenerResultsHandler.HandleAsync(
            new GetScreenerResultsQuery(), db);

        Assert.Equal("NewStrategy", result.StrategyName);
        Assert.Equal(2, result.Signals.Count);
    }

    [Fact]
    public async Task GetResults_filter_by_date()
    {
        await using var db = TestMarketDataDbContextFactory.Create();

        var day1 = SeedRun(new DateTime(2025, 6, 14));
        var day2 = SeedRun(new DateTime(2025, 6, 15));
        db.ScreenerRuns.AddRange(day1, day2);
        await db.SaveChangesAsync();

        var result = await GetScreenerResultsHandler.HandleAsync(
            new GetScreenerResultsQuery(Date: new DateTime(2025, 6, 14)), db);

        Assert.Equal(new DateTime(2025, 6, 14), result.ScanDate.Date);
    }

    [Fact]
    public async Task GetResults_filter_by_grade()
    {
        await using var db = TestMarketDataDbContextFactory.Create();

        db.ScreenerRuns.Add(SeedRun(DateTime.Today));
        await db.SaveChangesAsync();

        var result = await GetScreenerResultsHandler.HandleAsync(
            new GetScreenerResultsQuery(MinGrade: "A"), db);

        // Only A-grade signals returned
        Assert.All(result.Signals, s => Assert.Equal("A", s.Grade));
    }

    [Fact]
    public async Task GetResults_no_data_returns_warning()
    {
        await using var db = TestMarketDataDbContextFactory.Create();

        var result = await GetScreenerResultsHandler.HandleAsync(
            new GetScreenerResultsQuery(), db);

        Assert.Empty(result.Signals);
        Assert.Contains(result.Warnings, w => w.Contains("No screener results"));
    }

    // ── GetScreenerSignal ────────────────────────────────────

    [Fact]
    public async Task GetSignal_returns_specific_symbol()
    {
        await using var db = TestMarketDataDbContextFactory.Create();

        db.ScreenerRuns.Add(SeedRun(DateTime.Today));
        await db.SaveChangesAsync();

        var signal = await GetScreenerSignalHandler.HandleAsync(
            new GetScreenerSignalQuery("AAPL"), db);

        Assert.Equal("AAPL", signal.Symbol);
        Assert.Equal("A", signal.Grade);
        Assert.Equal(92m, signal.Score);
        Assert.Equal(6, signal.Breakdown.Count);
    }

    [Fact]
    public async Task GetSignal_case_insensitive()
    {
        await using var db = TestMarketDataDbContextFactory.Create();

        db.ScreenerRuns.Add(SeedRun(DateTime.Today));
        await db.SaveChangesAsync();

        var signal = await GetScreenerSignalHandler.HandleAsync(
            new GetScreenerSignalQuery("aapl"), db);

        Assert.Equal("AAPL", signal.Symbol);
    }

    [Fact]
    public async Task GetSignal_not_found_throws()
    {
        await using var db = TestMarketDataDbContextFactory.Create();

        db.ScreenerRuns.Add(SeedRun(DateTime.Today));
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            GetScreenerSignalHandler.HandleAsync(new GetScreenerSignalQuery("GOOG"), db));
    }

    [Fact]
    public async Task GetSignal_no_runs_throws()
    {
        await using var db = TestMarketDataDbContextFactory.Create();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            GetScreenerSignalHandler.HandleAsync(new GetScreenerSignalQuery("AAPL"), db));
    }

    // ── GetScreenerHistory ───────────────────────────────────

    [Fact]
    public async Task GetHistory_returns_paged_runs()
    {
        await using var db = TestMarketDataDbContextFactory.Create();

        for (var i = 0; i < 5; i++)
            db.ScreenerRuns.Add(SeedRun(DateTime.Today.AddDays(-i), $"Strategy-{i}"));
        await db.SaveChangesAsync();

        var result = await GetScreenerHistoryHandler.HandleAsync(
            new GetScreenerHistoryQuery(1, 3), db);

        Assert.Equal(3, result.Items.Count);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(3, result.PageSize);
    }

    [Fact]
    public async Task GetHistory_ordered_by_scan_date_descending()
    {
        await using var db = TestMarketDataDbContextFactory.Create();

        db.ScreenerRuns.Add(SeedRun(new DateTime(2025, 6, 10)));
        db.ScreenerRuns.Add(SeedRun(new DateTime(2025, 6, 15)));
        db.ScreenerRuns.Add(SeedRun(new DateTime(2025, 6, 12)));
        await db.SaveChangesAsync();

        var result = await GetScreenerHistoryHandler.HandleAsync(
            new GetScreenerHistoryQuery(1, 10), db);

        Assert.Equal(new DateTime(2025, 6, 15), result.Items[0].ScanDate.Date);
        Assert.Equal(new DateTime(2025, 6, 12), result.Items[1].ScanDate.Date);
        Assert.Equal(new DateTime(2025, 6, 10), result.Items[2].ScanDate.Date);
    }

    [Fact]
    public async Task GetHistory_empty_returns_empty_page()
    {
        await using var db = TestMarketDataDbContextFactory.Create();

        var result = await GetScreenerHistoryHandler.HandleAsync(
            new GetScreenerHistoryQuery(), db);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    // ── RunScreenerHandler (Persist) ─────────────────────────

    [Fact]
    public async Task RunScreener_persists_results()
    {
        await using var db = TestMarketDataDbContextFactory.Create();

        var scanResult = new ScreenerRunResult
        {
            ScanDate = new DateTime(2025, 6, 15),
            StrategyName = "TestStrategy",
            SymbolsScanned = 50,
            SignalsFound = 3,
            SignalsPassingFilter = 2,
            Results = new List<ScreenerResult>
            {
                new()
                {
                    Symbol = "AAPL", Grade = SignalGrade.A, Score = 92m,
                    Direction = SignalDirection.Long,
                    EntryPrice = 150m, StopPrice = 144m, TargetPrice = 162m,
                    RiskRewardRatio = 3m, SignalDate = new DateTime(2025, 6, 15),
                    Breakdown = new List<GradeBreakdownEntry>
                    {
                        new() { Factor = "TrendAlignment", RawScore = 100, Weight = 0.25m, Reason = "ok" }
                    }
                }
            },
            Warnings = new List<string> { "Test warning" },
            ElapsedTime = TimeSpan.FromSeconds(2)
        };

        var dto = await RunScreenerHandler.HandleAsync(scanResult, db);

        Assert.Equal("TestStrategy", dto.StrategyName);
        Assert.Equal(1, dto.Signals.Count);
        Assert.Equal("AAPL", dto.Signals[0].Symbol);

        // Verify persisted in DB
        var saved = db.ScreenerRuns.Single();
        Assert.Equal(50, saved.SymbolsScanned);
        Assert.Contains("AAPL", saved.ResultsJson);
    }

    [Fact]
    public async Task RunScreener_purges_old_results()
    {
        await using var db = TestMarketDataDbContextFactory.Create();

        // Add an old run (40 days ago)
        var oldRun = SeedRun(DateTime.UtcNow.AddDays(-40));
        db.ScreenerRuns.Add(oldRun);
        await db.SaveChangesAsync();

        Assert.Equal(1, db.ScreenerRuns.Count());

        // Run a new scan — should purge the old one
        var newResult = new ScreenerRunResult
        {
            ScanDate = DateTime.UtcNow,
            StrategyName = "New",
            SymbolsScanned = 10,
            Results = new(),
            Warnings = new(),
            ElapsedTime = TimeSpan.FromSeconds(1)
        };

        await RunScreenerHandler.HandleAsync(newResult, db);

        // Old run should be gone, only new run remains
        Assert.Equal(1, db.ScreenerRuns.Count());
        Assert.Equal("New", db.ScreenerRuns.Single().StrategyName);
    }

    [Fact]
    public async Task RunScreener_keeps_recent_results()
    {
        await using var db = TestMarketDataDbContextFactory.Create();

        // Add a recent run (10 days ago)
        var recentRun = SeedRun(DateTime.UtcNow.AddDays(-10));
        db.ScreenerRuns.Add(recentRun);
        await db.SaveChangesAsync();

        var newResult = new ScreenerRunResult
        {
            ScanDate = DateTime.UtcNow,
            StrategyName = "New",
            SymbolsScanned = 10,
            Results = new(),
            Warnings = new(),
            ElapsedTime = TimeSpan.FromSeconds(1)
        };

        await RunScreenerHandler.HandleAsync(newResult, db);

        // Both should still exist
        Assert.Equal(2, db.ScreenerRuns.Count());
    }
}
