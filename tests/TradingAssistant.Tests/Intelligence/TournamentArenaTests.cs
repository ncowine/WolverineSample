using Microsoft.Extensions.Logging.Abstractions;
using TradingAssistant.Application.Handlers.Intelligence;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Domain.Backtesting;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.Infrastructure.Persistence;
using TradingAssistant.Tests.Helpers;

namespace TradingAssistant.Tests.Intelligence;

public class TournamentArenaTests
{
    // ── CreateTournamentHandler ──────────────────────────────────────

    [Fact]
    public async Task CreateTournament_Success_ReturnsTournamentRun()
    {
        using var db = TestIntelligenceDbContextFactory.Create();
        var logger = NullLogger<CreateTournamentHandler>.Instance;

        var command = new CreateTournamentCommand("US_SP500", "Q1 2026 Tournament", 15);
        var result = await CreateTournamentHandler.HandleAsync(command, db, logger);

        Assert.Equal("US_SP500", result.MarketCode);
        Assert.Equal("Active", result.Status);
        Assert.Equal(15, result.MaxEntries);
        Assert.Equal(0, result.EntryCount);
        Assert.Equal("Q1 2026 Tournament", result.Description);

        var saved = await db.TournamentRuns.FindAsync(result.Id);
        Assert.NotNull(saved);
        Assert.Equal(TournamentRunStatus.Active, saved.Status);
    }

    [Fact]
    public async Task CreateTournament_MaxEntriesClamped_To20()
    {
        using var db = TestIntelligenceDbContextFactory.Create();
        var logger = NullLogger<CreateTournamentHandler>.Instance;

        var command = new CreateTournamentCommand("US_SP500", MaxEntries: 50);
        var result = await CreateTournamentHandler.HandleAsync(command, db, logger);

        Assert.Equal(CreateTournamentHandler.MaxEntriesLimit, result.MaxEntries);
    }

    [Fact]
    public async Task CreateTournament_MaxEntriesClamped_ToMin2()
    {
        using var db = TestIntelligenceDbContextFactory.Create();
        var logger = NullLogger<CreateTournamentHandler>.Instance;

        var command = new CreateTournamentCommand("US_SP500", MaxEntries: 0);
        var result = await CreateTournamentHandler.HandleAsync(command, db, logger);

        Assert.Equal(CreateTournamentHandler.MinEntries, result.MaxEntries);
    }

    // ── EnterTournamentHandler ──────────────────────────────────────

    [Fact]
    public async Task EnterTournament_Success_CreatesPaperAccountAndEntry()
    {
        var dbName = Guid.NewGuid().ToString();
        using var intelligenceDb = TestIntelligenceDbContextFactory.Create(dbName + "_intel");
        using var backtestDb = TestBacktestDbContextFactory.Create(dbName + "_bt");
        using var tradingDb = TestDbContextFactory.Create(dbName + "_trading");
        var logger = NullLogger<EnterTournamentHandler>.Instance;

        // Setup tournament and strategy
        var tournament = CreateActiveTournament(intelligenceDb, "US_SP500");
        var strategy = CreateStrategy(backtestDb, "RSI Mean Reversion");
        await intelligenceDb.SaveChangesAsync();
        await backtestDb.SaveChangesAsync();

        var command = new EnterTournamentCommand(tournament.Id, strategy.Id, 50_000m);
        var result = await EnterTournamentHandler.HandleAsync(
            command, backtestDb, tradingDb, intelligenceDb, logger);

        Assert.True(result.Success);
        Assert.NotNull(result.EntryId);
        Assert.NotNull(result.PaperAccountId);
        Assert.Equal("RSI Mean Reversion", result.StrategyName);

        // Verify paper account created in TradingDbContext
        var account = await tradingDb.Accounts.FindAsync(result.PaperAccountId);
        Assert.NotNull(account);
        Assert.Equal(50_000m, account.Balance);
        Assert.Equal(TradingAssistant.Domain.Enums.AccountType.Paper, account.AccountType);

        // Verify portfolio created
        var portfolio = tradingDb.Portfolios.FirstOrDefault(p => p.AccountId == account.Id);
        Assert.NotNull(portfolio);
        Assert.Equal(50_000m, portfolio.CashBalance);

        // Verify tournament entry in IntelligenceDbContext
        var entry = await intelligenceDb.TournamentEntries.FindAsync(result.EntryId);
        Assert.NotNull(entry);
        Assert.Equal(tournament.Id, entry.TournamentRunId);
        Assert.Equal(strategy.Id, entry.StrategyId);
        Assert.Equal(account.Id, entry.PaperAccountId);
        Assert.Equal(TournamentStatus.Active, entry.Status);
        Assert.Equal(25m, entry.AllocationPercent);
    }

    [Fact]
    public async Task EnterTournament_IsolatedAccounts_EachEntryGetsOwnAccount()
    {
        var dbName = Guid.NewGuid().ToString();
        using var intelligenceDb = TestIntelligenceDbContextFactory.Create(dbName + "_intel");
        using var backtestDb = TestBacktestDbContextFactory.Create(dbName + "_bt");
        using var tradingDb = TestDbContextFactory.Create(dbName + "_trading");
        var logger = NullLogger<EnterTournamentHandler>.Instance;

        var tournament = CreateActiveTournament(intelligenceDb, "US_SP500", maxEntries: 10);
        var strategy1 = CreateStrategy(backtestDb, "Strategy A");
        var strategy2 = CreateStrategy(backtestDb, "Strategy B");
        await intelligenceDb.SaveChangesAsync();
        await backtestDb.SaveChangesAsync();

        var result1 = await EnterTournamentHandler.HandleAsync(
            new EnterTournamentCommand(tournament.Id, strategy1.Id),
            backtestDb, tradingDb, intelligenceDb, logger);

        var result2 = await EnterTournamentHandler.HandleAsync(
            new EnterTournamentCommand(tournament.Id, strategy2.Id),
            backtestDb, tradingDb, intelligenceDb, logger);

        Assert.True(result1.Success);
        Assert.True(result2.Success);

        // Different paper accounts for isolation
        Assert.NotEqual(result1.PaperAccountId, result2.PaperAccountId);

        // Entry count updated
        var updatedTournament = await intelligenceDb.TournamentRuns.FindAsync(tournament.Id);
        Assert.Equal(2, updatedTournament!.EntryCount);
    }

    [Fact]
    public async Task EnterTournament_TournamentNotFound_ReturnsError()
    {
        var dbName = Guid.NewGuid().ToString();
        using var intelligenceDb = TestIntelligenceDbContextFactory.Create(dbName + "_intel");
        using var backtestDb = TestBacktestDbContextFactory.Create(dbName + "_bt");
        using var tradingDb = TestDbContextFactory.Create(dbName + "_trading");
        var logger = NullLogger<EnterTournamentHandler>.Instance;

        var command = new EnterTournamentCommand(Guid.NewGuid(), Guid.NewGuid());
        var result = await EnterTournamentHandler.HandleAsync(
            command, backtestDb, tradingDb, intelligenceDb, logger);

        Assert.False(result.Success);
        Assert.Contains("not found", result.Error!);
    }

    [Fact]
    public async Task EnterTournament_TournamentNotActive_ReturnsError()
    {
        var dbName = Guid.NewGuid().ToString();
        using var intelligenceDb = TestIntelligenceDbContextFactory.Create(dbName + "_intel");
        using var backtestDb = TestBacktestDbContextFactory.Create(dbName + "_bt");
        using var tradingDb = TestDbContextFactory.Create(dbName + "_trading");
        var logger = NullLogger<EnterTournamentHandler>.Instance;

        var tournament = new TournamentRun
        {
            MarketCode = "US_SP500",
            StartDate = DateTime.UtcNow,
            Status = TournamentRunStatus.Completed,
            MaxEntries = 10
        };
        intelligenceDb.TournamentRuns.Add(tournament);
        await intelligenceDb.SaveChangesAsync();

        var command = new EnterTournamentCommand(tournament.Id, Guid.NewGuid());
        var result = await EnterTournamentHandler.HandleAsync(
            command, backtestDb, tradingDb, intelligenceDb, logger);

        Assert.False(result.Success);
        Assert.Contains("Completed", result.Error!);
    }

    [Fact]
    public async Task EnterTournament_TournamentFull_ReturnsError()
    {
        var dbName = Guid.NewGuid().ToString();
        using var intelligenceDb = TestIntelligenceDbContextFactory.Create(dbName + "_intel");
        using var backtestDb = TestBacktestDbContextFactory.Create(dbName + "_bt");
        using var tradingDb = TestDbContextFactory.Create(dbName + "_trading");
        var logger = NullLogger<EnterTournamentHandler>.Instance;

        var tournament = CreateActiveTournament(intelligenceDb, "US_SP500", maxEntries: 2);

        // Fill the tournament
        var s1 = CreateStrategy(backtestDb, "S1");
        var s2 = CreateStrategy(backtestDb, "S2");
        var s3 = CreateStrategy(backtestDb, "S3");
        await intelligenceDb.SaveChangesAsync();
        await backtestDb.SaveChangesAsync();

        await EnterTournamentHandler.HandleAsync(
            new EnterTournamentCommand(tournament.Id, s1.Id),
            backtestDb, tradingDb, intelligenceDb, logger);

        await EnterTournamentHandler.HandleAsync(
            new EnterTournamentCommand(tournament.Id, s2.Id),
            backtestDb, tradingDb, intelligenceDb, logger);

        // Third entry should fail
        var result = await EnterTournamentHandler.HandleAsync(
            new EnterTournamentCommand(tournament.Id, s3.Id),
            backtestDb, tradingDb, intelligenceDb, logger);

        Assert.False(result.Success);
        Assert.Contains("full", result.Error!);
    }

    [Fact]
    public async Task EnterTournament_StrategyNotFound_ReturnsError()
    {
        var dbName = Guid.NewGuid().ToString();
        using var intelligenceDb = TestIntelligenceDbContextFactory.Create(dbName + "_intel");
        using var backtestDb = TestBacktestDbContextFactory.Create(dbName + "_bt");
        using var tradingDb = TestDbContextFactory.Create(dbName + "_trading");
        var logger = NullLogger<EnterTournamentHandler>.Instance;

        var tournament = CreateActiveTournament(intelligenceDb, "US_SP500");
        await intelligenceDb.SaveChangesAsync();

        var command = new EnterTournamentCommand(tournament.Id, Guid.NewGuid());
        var result = await EnterTournamentHandler.HandleAsync(
            command, backtestDb, tradingDb, intelligenceDb, logger);

        Assert.False(result.Success);
        Assert.Contains("Strategy", result.Error!);
    }

    [Fact]
    public async Task EnterTournament_DuplicateStrategy_ReturnsError()
    {
        var dbName = Guid.NewGuid().ToString();
        using var intelligenceDb = TestIntelligenceDbContextFactory.Create(dbName + "_intel");
        using var backtestDb = TestBacktestDbContextFactory.Create(dbName + "_bt");
        using var tradingDb = TestDbContextFactory.Create(dbName + "_trading");
        var logger = NullLogger<EnterTournamentHandler>.Instance;

        var tournament = CreateActiveTournament(intelligenceDb, "US_SP500");
        var strategy = CreateStrategy(backtestDb, "Duplicate Test");
        await intelligenceDb.SaveChangesAsync();
        await backtestDb.SaveChangesAsync();

        // Enter once — should succeed
        var result1 = await EnterTournamentHandler.HandleAsync(
            new EnterTournamentCommand(tournament.Id, strategy.Id),
            backtestDb, tradingDb, intelligenceDb, logger);
        Assert.True(result1.Success);

        // Enter again — should fail
        var result2 = await EnterTournamentHandler.HandleAsync(
            new EnterTournamentCommand(tournament.Id, strategy.Id),
            backtestDb, tradingDb, intelligenceDb, logger);

        Assert.False(result2.Success);
        Assert.Contains("already entered", result2.Error!);
    }

    [Fact]
    public async Task EnterTournament_Supports20ConcurrentEntries()
    {
        var dbName = Guid.NewGuid().ToString();
        using var intelligenceDb = TestIntelligenceDbContextFactory.Create(dbName + "_intel");
        using var backtestDb = TestBacktestDbContextFactory.Create(dbName + "_bt");
        using var tradingDb = TestDbContextFactory.Create(dbName + "_trading");
        var logger = NullLogger<EnterTournamentHandler>.Instance;

        var tournament = CreateActiveTournament(intelligenceDb, "US_SP500", maxEntries: 20);
        await intelligenceDb.SaveChangesAsync();

        // Create 20 strategies
        for (int i = 0; i < 20; i++)
        {
            CreateStrategy(backtestDb, $"Strategy {i + 1}");
        }
        await backtestDb.SaveChangesAsync();

        var strategies = backtestDb.Strategies.ToList();
        var successes = 0;

        foreach (var strategy in strategies)
        {
            var result = await EnterTournamentHandler.HandleAsync(
                new EnterTournamentCommand(tournament.Id, strategy.Id),
                backtestDb, tradingDb, intelligenceDb, logger);

            if (result.Success) successes++;
        }

        Assert.Equal(20, successes);

        // Verify all entries are isolated
        var entries = intelligenceDb.TournamentEntries
            .Where(e => e.TournamentRunId == tournament.Id).ToList();
        var accountIds = entries.Select(e => e.PaperAccountId).ToList();
        Assert.Equal(20, accountIds.Distinct().Count());
    }

    // ── GetTournamentHandler ────────────────────────────────────────

    [Fact]
    public async Task GetTournament_Exists_ReturnsDto()
    {
        using var db = TestIntelligenceDbContextFactory.Create();

        var run = new TournamentRun
        {
            MarketCode = "IN_NIFTY50",
            StartDate = DateTime.UtcNow,
            Status = TournamentRunStatus.Active,
            MaxEntries = 10,
            EntryCount = 3,
            Description = "Test"
        };
        db.TournamentRuns.Add(run);
        await db.SaveChangesAsync();

        var result = await GetTournamentHandler.HandleAsync(
            new GetTournamentQuery(run.Id), db);

        Assert.NotNull(result);
        Assert.Equal("IN_NIFTY50", result!.MarketCode);
        Assert.Equal("Active", result.Status);
        Assert.Equal(3, result.EntryCount);
    }

    [Fact]
    public async Task GetTournament_NotFound_ReturnsNull()
    {
        using var db = TestIntelligenceDbContextFactory.Create();

        var result = await GetTournamentHandler.HandleAsync(
            new GetTournamentQuery(Guid.NewGuid()), db);

        Assert.Null(result);
    }

    // ── GetTournamentEntriesHandler ─────────────────────────────────

    [Fact]
    public async Task GetTournamentEntries_ReturnsSortedByReturn()
    {
        using var db = TestIntelligenceDbContextFactory.Create();

        var tournamentId = Guid.NewGuid();
        db.TournamentEntries.AddRange(
            new TournamentEntry
            {
                TournamentRunId = tournamentId, StrategyId = Guid.NewGuid(),
                PaperAccountId = Guid.NewGuid(), MarketCode = "US_SP500",
                StartDate = DateTime.UtcNow, TotalReturn = 5.5m, Status = TournamentStatus.Active
            },
            new TournamentEntry
            {
                TournamentRunId = tournamentId, StrategyId = Guid.NewGuid(),
                PaperAccountId = Guid.NewGuid(), MarketCode = "US_SP500",
                StartDate = DateTime.UtcNow, TotalReturn = 12.3m, Status = TournamentStatus.Active
            },
            new TournamentEntry
            {
                TournamentRunId = tournamentId, StrategyId = Guid.NewGuid(),
                PaperAccountId = Guid.NewGuid(), MarketCode = "US_SP500",
                StartDate = DateTime.UtcNow, TotalReturn = -2.1m, Status = TournamentStatus.Active
            }
        );
        await db.SaveChangesAsync();

        var result = await GetTournamentEntriesHandler.HandleAsync(
            new GetTournamentEntriesQuery(tournamentId), db);

        Assert.Equal(3, result.Count);
        // Sorted descending by TotalReturn
        Assert.Equal(12.3m, result[0].TotalReturn);
        Assert.Equal(5.5m, result[1].TotalReturn);
        Assert.Equal(-2.1m, result[2].TotalReturn);
    }

    [Fact]
    public async Task GetTournamentEntries_EmptyTournament_ReturnsEmpty()
    {
        using var db = TestIntelligenceDbContextFactory.Create();

        var result = await GetTournamentEntriesHandler.HandleAsync(
            new GetTournamentEntriesQuery(Guid.NewGuid()), db);

        Assert.Empty(result);
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private static TournamentRun CreateActiveTournament(
        IntelligenceDbContext db, string market, int maxEntries = 20)
    {
        var run = new TournamentRun
        {
            MarketCode = market,
            StartDate = DateTime.UtcNow,
            Status = TournamentRunStatus.Active,
            MaxEntries = maxEntries,
            Description = "Test tournament"
        };
        db.TournamentRuns.Add(run);
        return run;
    }

    private static Strategy CreateStrategy(BacktestDbContext db, string name)
    {
        var strategy = new Strategy
        {
            Name = name,
            Description = $"Test strategy: {name}",
            IsActive = true
        };
        db.Strategies.Add(strategy);
        return strategy;
    }
}
