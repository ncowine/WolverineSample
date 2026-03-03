using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using TradingAssistant.Application.Handlers.Intelligence;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.Domain.MarketData;
using TradingAssistant.Tests.Helpers;

namespace TradingAssistant.Tests.Intelligence;

public class EnsembleVotingTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // ── Majority Voting ──────────────────────────────────────────

    [Fact]
    public void MajorityVoting_ThreeAgree_ReturnsConsensus()
    {
        var votes = new List<EnsembleVotingHandler.SymbolVote>
        {
            Vote("AAPL", "S1", SignalType.Buy, 1.5m),
            Vote("AAPL", "S2", SignalType.Buy, 1.2m),
            Vote("AAPL", "S3", SignalType.Buy, 0.8m),
            Vote("AAPL", "S4", SignalType.Sell, 0.5m),
            Vote("AAPL", "S5", SignalType.Sell, 0.3m),
        };

        var result = EnsembleVotingHandler.ComputeMajorityConsensus(votes, 2);

        Assert.NotNull(result);
        Assert.Equal(SignalType.Buy, result!.Direction);
        Assert.Equal(3, result.AgreeingCount);
        Assert.Equal(0.6m, result.Confidence);
    }

    [Fact]
    public void MajorityVoting_MinAgreementNotMet_ReturnsNull()
    {
        var votes = new List<EnsembleVotingHandler.SymbolVote>
        {
            Vote("AAPL", "S1", SignalType.Buy, 1.5m),
            Vote("AAPL", "S2", SignalType.Sell, 1.2m),
            Vote("AAPL", "S3", SignalType.Hold, 0.8m),
        };

        var result = EnsembleVotingHandler.ComputeMajorityConsensus(votes, 2);

        Assert.Null(result);
    }

    [Fact]
    public void MajorityVoting_Tie_ReturnsNull()
    {
        var votes = new List<EnsembleVotingHandler.SymbolVote>
        {
            Vote("AAPL", "S1", SignalType.Buy, 1.5m),
            Vote("AAPL", "S2", SignalType.Buy, 1.2m),
            Vote("AAPL", "S3", SignalType.Sell, 1.8m),
            Vote("AAPL", "S4", SignalType.Sell, 0.9m),
        };

        var result = EnsembleVotingHandler.ComputeMajorityConsensus(votes, 2);

        Assert.Null(result);
    }

    [Fact]
    public void MajorityVoting_Default2Of3_Works()
    {
        var votes = new List<EnsembleVotingHandler.SymbolVote>
        {
            Vote("MSFT", "S1", SignalType.Sell, 1.5m),
            Vote("MSFT", "S2", SignalType.Sell, 1.0m),
            Vote("MSFT", "S3", SignalType.Buy, 0.8m),
        };

        var result = EnsembleVotingHandler.ComputeMajorityConsensus(votes, 2);

        Assert.NotNull(result);
        Assert.Equal(SignalType.Sell, result!.Direction);
        Assert.Equal(2, result.AgreeingCount);
    }

    [Fact]
    public void MajorityVoting_SingleVoter_MeetsMinAgreement1()
    {
        var votes = new List<EnsembleVotingHandler.SymbolVote>
        {
            Vote("TSLA", "S1", SignalType.Buy, 1.5m),
        };

        var result = EnsembleVotingHandler.ComputeMajorityConsensus(votes, 1);

        Assert.NotNull(result);
        Assert.Equal(SignalType.Buy, result!.Direction);
        Assert.Equal(1.0m, result.Confidence);
    }

    [Fact]
    public void MajorityVoting_EmptyVotes_ReturnsNull()
    {
        var result = EnsembleVotingHandler.ComputeConsensus(
            new List<EnsembleVotingHandler.SymbolVote>(), 2, false);

        Assert.Null(result);
    }

    // ── Sharpe-Weighted Voting ───────────────────────────────────

    [Fact]
    public void WeightedVoting_HighSharpeWins()
    {
        // 2 Buy voters with low Sharpe, 1 Sell with high Sharpe
        // But we need at least 1 for min agreement
        var votes = new List<EnsembleVotingHandler.SymbolVote>
        {
            Vote("AAPL", "S1", SignalType.Buy, 0.5m),  // weight 0.5
            Vote("AAPL", "S2", SignalType.Buy, 0.3m),  // weight 0.3
            Vote("AAPL", "S3", SignalType.Sell, 3.0m),  // weight 3.0
        };

        var result = EnsembleVotingHandler.ComputeWeightedConsensus(votes, 1);

        Assert.NotNull(result);
        Assert.Equal(SignalType.Sell, result!.Direction);
        // Sell weight = 3.0, total = 3.8, confidence = 3.0/3.8
        Assert.True(result.Confidence > 0.7m);
    }

    [Fact]
    public void WeightedVoting_NegativeSharpeIgnored()
    {
        var votes = new List<EnsembleVotingHandler.SymbolVote>
        {
            Vote("AAPL", "S1", SignalType.Buy, 1.5m),
            Vote("AAPL", "S2", SignalType.Sell, -0.5m), // negative → weight clamped to 0
        };

        var result = EnsembleVotingHandler.ComputeWeightedConsensus(votes, 1);

        Assert.NotNull(result);
        Assert.Equal(SignalType.Buy, result!.Direction);
        Assert.Equal(1.0m, result.Confidence); // 1.5 / 1.5
    }

    [Fact]
    public void WeightedVoting_TiedWeights_ReturnsNull()
    {
        var votes = new List<EnsembleVotingHandler.SymbolVote>
        {
            Vote("AAPL", "S1", SignalType.Buy, 1.0m),
            Vote("AAPL", "S2", SignalType.Sell, 1.0m),
        };

        var result = EnsembleVotingHandler.ComputeWeightedConsensus(votes, 1);

        Assert.Null(result);
    }

    [Fact]
    public void WeightedVoting_MinAgreementNotMet_ReturnsNull()
    {
        var votes = new List<EnsembleVotingHandler.SymbolVote>
        {
            Vote("AAPL", "S1", SignalType.Buy, 5.0m),
        };

        var result = EnsembleVotingHandler.ComputeWeightedConsensus(votes, 2);

        Assert.Null(result);
    }

    [Fact]
    public void WeightedVoting_AllZeroSharpe_ReturnsNull()
    {
        var votes = new List<EnsembleVotingHandler.SymbolVote>
        {
            Vote("AAPL", "S1", SignalType.Buy, 0m),
            Vote("AAPL", "S2", SignalType.Buy, 0m),
        };

        var result = EnsembleVotingHandler.ComputeWeightedConsensus(votes, 2);

        Assert.Null(result);
    }

    // ── MapDirection ─────────────────────────────────────────────

    [Theory]
    [InlineData("Long", SignalType.Buy)]
    [InlineData("long", SignalType.Buy)]
    [InlineData("Buy", SignalType.Buy)]
    [InlineData("Short", SignalType.Sell)]
    [InlineData("short", SignalType.Sell)]
    [InlineData("Sell", SignalType.Sell)]
    [InlineData("Hold", SignalType.Hold)]
    [InlineData("unknown", SignalType.Hold)]
    public void MapDirection_MapsCorrectly(string input, SignalType expected)
    {
        Assert.Equal(expected, EnsembleVotingHandler.MapDirection(input));
    }

    // ── ComputeConsensus dispatch ────────────────────────────────

    [Fact]
    public void ComputeConsensus_UsesWeightedWhenFlagged()
    {
        // Same votes but weighted vs unweighted should differ
        var votes = new List<EnsembleVotingHandler.SymbolVote>
        {
            Vote("AAPL", "S1", SignalType.Buy, 0.1m),
            Vote("AAPL", "S2", SignalType.Buy, 0.1m),
            Vote("AAPL", "S3", SignalType.Sell, 5.0m),
        };

        var majority = EnsembleVotingHandler.ComputeConsensus(votes, 1, false);
        var weighted = EnsembleVotingHandler.ComputeConsensus(votes, 1, true);

        Assert.NotNull(majority);
        Assert.NotNull(weighted);
        Assert.Equal(SignalType.Buy, majority!.Direction);  // 2 vs 1 by count
        Assert.Equal(SignalType.Sell, weighted!.Direction);  // 5.0 vs 0.2 by weight
    }

    // ── Full Handler Integration ─────────────────────────────────

    [Fact]
    public async Task ComputeHandler_NoPromotedStrategies_Fails()
    {
        var dbName = Guid.NewGuid().ToString();
        using var marketDb = TestMarketDataDbContextFactory.Create(dbName + "_market");
        using var intelDb = TestIntelligenceDbContextFactory.Create(dbName + "_intel");
        var logger = NullLogger<EnsembleVotingHandler>.Instance;

        var result = await EnsembleVotingHandler.HandleAsync(
            new ComputeEnsembleSignalsCommand("US_SP500", DateTime.UtcNow.Date),
            marketDb, intelDb, logger);

        Assert.False(result.Success);
        Assert.Contains("No promoted strategies", result.Error!);
    }

    [Fact]
    public async Task ComputeHandler_WithPromotedStrategies_ProducesSignals()
    {
        var dbName = Guid.NewGuid().ToString();
        using var marketDb = TestMarketDataDbContextFactory.Create(dbName + "_market");
        using var intelDb = TestIntelligenceDbContextFactory.Create(dbName + "_intel");
        var logger = NullLogger<EnsembleVotingHandler>.Instance;

        var today = DateTime.UtcNow.Date;
        var strategyId1 = Guid.NewGuid();
        var strategyId2 = Guid.NewGuid();
        var strategyId3 = Guid.NewGuid();

        // Create promoted entries
        intelDb.TournamentEntries.AddRange(
            CreatePromotedEntry(strategyId1, "Momentum", 1.5m),
            CreatePromotedEntry(strategyId2, "MeanReversion", 1.2m),
            CreatePromotedEntry(strategyId3, "Breakout", 0.8m));
        await intelDb.SaveChangesAsync();

        // Create screener runs with signals
        // AAPL: 2 Long + 1 Short = consensus Buy; MSFT: 1 Long + 1 Short + 1 Short = consensus Sell
        marketDb.ScreenerRuns.AddRange(
            CreateScreenerRun(strategyId1, today, new[] { ("AAPL", "Long"), ("MSFT", "Long") }),
            CreateScreenerRun(strategyId2, today, new[] { ("AAPL", "Long"), ("MSFT", "Short") }),
            CreateScreenerRun(strategyId3, today, new[] { ("AAPL", "Short"), ("MSFT", "Short") }));
        await marketDb.SaveChangesAsync();

        var result = await EnsembleVotingHandler.HandleAsync(
            new ComputeEnsembleSignalsCommand("US_SP500", today, MinAgreement: 2),
            marketDb, intelDb, logger);

        Assert.True(result.Success);
        Assert.Equal(2, result.TotalSymbolsEvaluated);  // AAPL + MSFT
        Assert.Equal(2, result.ConsensusSignals);  // Both reach consensus
        var aapl = result.Signals.First(s => s.Symbol == "AAPL");
        Assert.Equal("Buy", aapl.Direction);
        Assert.Equal(3, aapl.TotalVoters);
        Assert.Equal(2, aapl.AgreeingVoters);
        var msft = result.Signals.First(s => s.Symbol == "MSFT");
        Assert.Equal("Sell", msft.Direction);
        Assert.Equal(2, msft.AgreeingVoters);
    }

    [Fact]
    public async Task ComputeHandler_WeightedVoting_UsesSharpWeights()
    {
        var dbName = Guid.NewGuid().ToString();
        using var marketDb = TestMarketDataDbContextFactory.Create(dbName + "_market");
        using var intelDb = TestIntelligenceDbContextFactory.Create(dbName + "_intel");
        var logger = NullLogger<EnsembleVotingHandler>.Instance;

        var today = DateTime.UtcNow.Date;
        var strategyId1 = Guid.NewGuid();
        var strategyId2 = Guid.NewGuid();
        var strategyId3 = Guid.NewGuid();

        intelDb.TournamentEntries.AddRange(
            CreatePromotedEntry(strategyId1, "Low", 0.3m),
            CreatePromotedEntry(strategyId2, "Low2", 0.2m),
            CreatePromotedEntry(strategyId3, "High", 3.0m));
        await intelDb.SaveChangesAsync();

        // 2 buy (low sharpe) vs 1 sell (high sharpe)
        marketDb.ScreenerRuns.AddRange(
            CreateScreenerRun(strategyId1, today, new[] { ("GOOG", "Long") }),
            CreateScreenerRun(strategyId2, today, new[] { ("GOOG", "Long") }),
            CreateScreenerRun(strategyId3, today, new[] { ("GOOG", "Short") }));
        await marketDb.SaveChangesAsync();

        var result = await EnsembleVotingHandler.HandleAsync(
            new ComputeEnsembleSignalsCommand("US_SP500", today, MinAgreement: 1, UseWeightedVoting: true),
            marketDb, intelDb, logger);

        Assert.True(result.Success);
        Assert.Single(result.Signals);
        Assert.Equal("Sell", result.Signals[0].Direction);  // High Sharpe wins
        Assert.Equal("SharpeWeighted", result.Signals[0].VotingMode);
    }

    [Fact]
    public async Task ComputeHandler_PersistsEnsembleSignals()
    {
        var dbName = Guid.NewGuid().ToString();
        using var marketDb = TestMarketDataDbContextFactory.Create(dbName + "_market");
        using var intelDb = TestIntelligenceDbContextFactory.Create(dbName + "_intel");
        var logger = NullLogger<EnsembleVotingHandler>.Instance;

        var today = DateTime.UtcNow.Date;
        var strategyId1 = Guid.NewGuid();
        var strategyId2 = Guid.NewGuid();

        intelDb.TournamentEntries.AddRange(
            CreatePromotedEntry(strategyId1, "S1", 1.5m),
            CreatePromotedEntry(strategyId2, "S2", 1.2m));
        await intelDb.SaveChangesAsync();

        marketDb.ScreenerRuns.AddRange(
            CreateScreenerRun(strategyId1, today, new[] { ("NVDA", "Long") }),
            CreateScreenerRun(strategyId2, today, new[] { ("NVDA", "Long") }));
        await marketDb.SaveChangesAsync();

        await EnsembleVotingHandler.HandleAsync(
            new ComputeEnsembleSignalsCommand("US_SP500", today),
            marketDb, intelDb, logger);

        var persisted = intelDb.EnsembleSignals.Where(s => s.Symbol == "NVDA").ToList();
        Assert.Single(persisted);
        Assert.Equal(SignalType.Buy, persisted[0].Direction);
        Assert.Contains("S1", persisted[0].VotesJson);
        Assert.Contains("S2", persisted[0].VotesJson);
    }

    // ── GetEnsembleSignals Query ─────────────────────────────────

    [Fact]
    public async Task GetSignals_ReturnsStoredSignals()
    {
        using var db = TestIntelligenceDbContextFactory.Create();

        var today = DateTime.UtcNow.Date;
        db.EnsembleSignals.Add(new EnsembleSignal
        {
            MarketCode = "US_SP500",
            Symbol = "AAPL",
            SignalDate = today,
            Direction = SignalType.Buy,
            Confidence = 0.67m,
            VotingMode = "Majority",
            MinAgreement = 2,
            TotalVoters = 3,
            AgreeingVoters = 2,
            VotesJson = "[]"
        });
        await db.SaveChangesAsync();

        var result = await GetEnsembleSignalsHandler.HandleAsync(
            new GetEnsembleSignalsQuery("US_SP500", today), db);

        Assert.Single(result);
        Assert.Equal("AAPL", result[0].Symbol);
        Assert.Equal("Buy", result[0].Direction);
    }

    [Fact]
    public async Task GetSignals_NoSignals_ReturnsEmpty()
    {
        using var db = TestIntelligenceDbContextFactory.Create();

        var result = await GetEnsembleSignalsHandler.HandleAsync(
            new GetEnsembleSignalsQuery("US_SP500"), db);

        Assert.Empty(result);
    }

    // ── Helpers ──────────────────────────────────────────────────

    private static EnsembleVotingHandler.SymbolVote Vote(
        string symbol, string strategyName, SignalType direction, decimal sharpe)
    {
        return new EnsembleVotingHandler.SymbolVote(
            symbol, Guid.NewGuid(), strategyName, direction, sharpe);
    }

    private static TournamentEntry CreatePromotedEntry(
        Guid strategyId, string name, decimal sharpe)
    {
        return new TournamentEntry
        {
            TournamentRunId = Guid.NewGuid(),
            StrategyId = strategyId,
            StrategyName = name,
            PaperAccountId = Guid.NewGuid(),
            MarketCode = "US_SP500",
            StartDate = DateTime.UtcNow.AddDays(-60),
            DaysActive = 60,
            TotalTrades = 50,
            WinRate = 0.55m,
            SharpeRatio = sharpe,
            MaxDrawdown = 5m,
            TotalReturn = 10m,
            Status = TournamentStatus.Promoted,
            AllocationPercent = 25m
        };
    }

    private static ScreenerRun CreateScreenerRun(
        Guid strategyId, DateTime date, (string Symbol, string Direction)[] signals)
    {
        var results = signals.Select(s => new { symbol = s.Symbol, direction = s.Direction }).ToList();
        return new ScreenerRun
        {
            StrategyId = strategyId,
            StrategyName = "Strategy",
            ScanDate = date,
            SymbolsScanned = signals.Length,
            SignalsFound = signals.Length,
            ResultsJson = JsonSerializer.Serialize(results, JsonOpts),
            ElapsedTime = TimeSpan.FromSeconds(1)
        };
    }
}
