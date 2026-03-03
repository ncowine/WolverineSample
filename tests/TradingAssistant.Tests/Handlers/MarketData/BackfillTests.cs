using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TradingAssistant.Application.Handlers.MarketData;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Domain.MarketData;
using TradingAssistant.Tests.Helpers;

namespace TradingAssistant.Tests.Handlers.MarketData;

public class BackfillTests
{
    // ── InitiateBackfillHandler Tests ─────────────────────────────

    [Fact]
    public async Task Initiate_CreatesJobWithCorrectProperties()
    {
        using var db = TestMarketDataDbContextFactory.Create();
        var logger = NullLogger<InitiateBackfillHandler>.Instance;

        var universe = new StockUniverse
        {
            Name = "Test Universe",
            Description = "Test",
            Symbols = "AAPL,MSFT,GOOGL",
            IncludesBenchmark = true
        };
        db.StockUniverses.Add(universe);
        await db.SaveChangesAsync();

        var result = await InitiateBackfillHandler.HandleAsync(
            new BackfillCommand(universe.Id, 5, false), db, logger);

        Assert.Equal(universe.Id, result.UniverseId);
        Assert.Equal(5, result.YearsBack);
        Assert.False(result.IsIncremental);
        Assert.Equal("Pending", result.Status);
        Assert.Equal(4, result.TotalSymbols); // 3 stocks + SPY benchmark
        Assert.Equal(0, result.CompletedSymbols);
        Assert.Equal(0, result.FailedSymbols);
    }

    [Fact]
    public async Task Initiate_WithoutBenchmark_DoesNotAddSpy()
    {
        using var db = TestMarketDataDbContextFactory.Create();
        var logger = NullLogger<InitiateBackfillHandler>.Instance;

        var universe = new StockUniverse
        {
            Name = "No Benchmark",
            Description = "Test",
            Symbols = "AAPL,MSFT",
            IncludesBenchmark = false
        };
        db.StockUniverses.Add(universe);
        await db.SaveChangesAsync();

        var result = await InitiateBackfillHandler.HandleAsync(
            new BackfillCommand(universe.Id), db, logger);

        Assert.Equal(2, result.TotalSymbols); // No SPY added
    }

    [Fact]
    public async Task Initiate_AlreadyHasSpy_DoesNotDuplicate()
    {
        using var db = TestMarketDataDbContextFactory.Create();
        var logger = NullLogger<InitiateBackfillHandler>.Instance;

        var universe = new StockUniverse
        {
            Name = "Has SPY",
            Description = "Test",
            Symbols = "SPY,AAPL",
            IncludesBenchmark = true
        };
        db.StockUniverses.Add(universe);
        await db.SaveChangesAsync();

        var result = await InitiateBackfillHandler.HandleAsync(
            new BackfillCommand(universe.Id), db, logger);

        Assert.Equal(2, result.TotalSymbols); // SPY not duplicated
    }

    [Fact]
    public async Task Initiate_Incremental_SetsFlag()
    {
        using var db = TestMarketDataDbContextFactory.Create();
        var logger = NullLogger<InitiateBackfillHandler>.Instance;

        var universe = new StockUniverse
        {
            Name = "Daily Update",
            Description = "Test",
            Symbols = "AAPL",
            IncludesBenchmark = false
        };
        db.StockUniverses.Add(universe);
        await db.SaveChangesAsync();

        var result = await InitiateBackfillHandler.HandleAsync(
            new BackfillCommand(universe.Id, 1, Incremental: true), db, logger);

        Assert.True(result.IsIncremental);
    }

    [Fact]
    public async Task Initiate_UniverseNotFound_Throws()
    {
        using var db = TestMarketDataDbContextFactory.Create();
        var logger = NullLogger<InitiateBackfillHandler>.Instance;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InitiateBackfillHandler.HandleAsync(
                new BackfillCommand(Guid.NewGuid()), db, logger));
    }

    [Fact]
    public async Task Initiate_PersistsJobToDb()
    {
        using var db = TestMarketDataDbContextFactory.Create();
        var logger = NullLogger<InitiateBackfillHandler>.Instance;

        var universe = new StockUniverse
        {
            Name = "Persist Test",
            Description = "Test",
            Symbols = "AAPL",
            IncludesBenchmark = false
        };
        db.StockUniverses.Add(universe);
        await db.SaveChangesAsync();

        var result = await InitiateBackfillHandler.HandleAsync(
            new BackfillCommand(universe.Id), db, logger);

        var persisted = await db.BackfillJobs.FindAsync(result.Id);
        Assert.NotNull(persisted);
        Assert.Equal(BackfillStatus.Pending, persisted!.Status);
    }

    // ── GetBackfillStatusHandler Tests ────────────────────────────

    [Fact]
    public async Task GetStatus_ReturnsJob()
    {
        using var db = TestMarketDataDbContextFactory.Create();
        var job = new BackfillJob
        {
            UniverseId = Guid.NewGuid(),
            YearsBack = 5,
            Status = BackfillStatus.Running,
            TotalSymbols = 10,
            CompletedSymbols = 4,
            FailedSymbols = 1,
            StartedAt = DateTime.UtcNow.AddMinutes(-2)
        };
        db.BackfillJobs.Add(job);
        await db.SaveChangesAsync();

        var result = await GetBackfillStatusHandler.HandleAsync(
            new GetBackfillStatusQuery(job.Id), db);

        Assert.NotNull(result);
        Assert.Equal("Running", result!.Status);
        Assert.Equal(10, result.TotalSymbols);
        Assert.Equal(4, result.CompletedSymbols);
        Assert.Equal(1, result.FailedSymbols);
    }

    [Fact]
    public async Task GetStatus_NotFound_ReturnsNull()
    {
        using var db = TestMarketDataDbContextFactory.Create();

        var result = await GetBackfillStatusHandler.HandleAsync(
            new GetBackfillStatusQuery(Guid.NewGuid()), db);

        Assert.Null(result);
    }

    // ── GetBackfillJobsHandler Tests ──────────────────────────────

    [Fact]
    public async Task GetJobs_ReturnsAllJobs()
    {
        using var db = TestMarketDataDbContextFactory.Create();
        var universeId = Guid.NewGuid();
        db.BackfillJobs.AddRange(
            new BackfillJob { UniverseId = universeId, Status = BackfillStatus.Completed, TotalSymbols = 5 },
            new BackfillJob { UniverseId = universeId, Status = BackfillStatus.Running, TotalSymbols = 3 },
            new BackfillJob { UniverseId = Guid.NewGuid(), Status = BackfillStatus.Pending, TotalSymbols = 2 }
        );
        await db.SaveChangesAsync();

        var result = await GetBackfillJobsHandler.HandleAsync(
            new GetBackfillJobsQuery(), db);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task GetJobs_FiltersByUniverse()
    {
        using var db = TestMarketDataDbContextFactory.Create();
        var targetId = Guid.NewGuid();
        db.BackfillJobs.AddRange(
            new BackfillJob { UniverseId = targetId, Status = BackfillStatus.Completed, TotalSymbols = 5 },
            new BackfillJob { UniverseId = Guid.NewGuid(), Status = BackfillStatus.Running, TotalSymbols = 3 }
        );
        await db.SaveChangesAsync();

        var result = await GetBackfillJobsHandler.HandleAsync(
            new GetBackfillJobsQuery(targetId), db);

        Assert.Single(result);
        Assert.Equal(targetId, result[0].UniverseId);
    }

    [Fact]
    public async Task GetJobs_OrderedByCreatedAtDesc()
    {
        using var db = TestMarketDataDbContextFactory.Create();
        var universeId = Guid.NewGuid();

        var older = new BackfillJob { UniverseId = universeId, TotalSymbols = 1 };
        older.CreatedAt = DateTime.UtcNow.AddHours(-1);

        var newer = new BackfillJob { UniverseId = universeId, TotalSymbols = 2 };
        newer.CreatedAt = DateTime.UtcNow;

        db.BackfillJobs.AddRange(older, newer);
        await db.SaveChangesAsync();

        var result = await GetBackfillJobsHandler.HandleAsync(
            new GetBackfillJobsQuery(), db);

        Assert.Equal(2, result[0].TotalSymbols); // newer first
        Assert.Equal(1, result[1].TotalSymbols);
    }

    // ── BackfillJob Entity Tests ──────────────────────────────────

    [Fact]
    public async Task BackfillJob_DefaultValues()
    {
        using var db = TestMarketDataDbContextFactory.Create();
        var job = new BackfillJob { UniverseId = Guid.NewGuid(), TotalSymbols = 5 };
        db.BackfillJobs.Add(job);
        await db.SaveChangesAsync();

        var loaded = await db.BackfillJobs.FindAsync(job.Id);
        Assert.NotNull(loaded);
        Assert.Equal(BackfillStatus.Pending, loaded!.Status);
        Assert.Equal(5, loaded.YearsBack);
        Assert.False(loaded.IsIncremental);
        Assert.Equal("[]", loaded.ErrorLog);
        Assert.Null(loaded.StartedAt);
        Assert.Null(loaded.CompletedAt);
    }

    [Fact]
    public async Task BackfillJob_StatusTransitions()
    {
        using var db = TestMarketDataDbContextFactory.Create();
        var job = new BackfillJob { UniverseId = Guid.NewGuid(), TotalSymbols = 3 };
        db.BackfillJobs.Add(job);
        await db.SaveChangesAsync();

        // Pending → Running
        job.Status = BackfillStatus.Running;
        job.StartedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var running = await db.BackfillJobs.FindAsync(job.Id);
        Assert.Equal(BackfillStatus.Running, running!.Status);
        Assert.NotNull(running.StartedAt);

        // Running → Completed
        job.Status = BackfillStatus.Completed;
        job.CompletedSymbols = 3;
        job.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var completed = await db.BackfillJobs.FindAsync(job.Id);
        Assert.Equal(BackfillStatus.Completed, completed!.Status);
        Assert.Equal(3, completed.CompletedSymbols);
        Assert.NotNull(completed.CompletedAt);
    }
}
