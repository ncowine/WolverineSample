using Microsoft.Extensions.Logging.Abstractions;
using TradingAssistant.Application.Handlers.Intelligence;
using TradingAssistant.Application.Indicators;
using TradingAssistant.Contracts.Events;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.Domain.MarketData;
using TradingAssistant.Domain.Trading;
using TradingAssistant.Tests.Helpers;

namespace TradingAssistant.Tests.Intelligence;

public class FeatureSnapshotTests
{
    // ── FeatureExtractor ─────────────────────────────────────────

    [Fact]
    public void ExtractFeatures_Produces45Features()
    {
        var indicators = CreateSampleIndicators();
        var context = CreateSampleContext();

        var features = FeatureExtractor.ExtractFeatures(indicators, context);

        Assert.True(features.Count >= 40, $"Expected 40+ features, got {features.Count}");
    }

    [Fact]
    public void ExtractFeatures_ContainsAllIndicators()
    {
        var indicators = CreateSampleIndicators();
        var context = CreateSampleContext();

        var features = FeatureExtractor.ExtractFeatures(indicators, context);

        // Trend
        Assert.True(features.ContainsKey("smaShort"));
        Assert.True(features.ContainsKey("smaMedium"));
        Assert.True(features.ContainsKey("smaLong"));
        Assert.True(features.ContainsKey("emaShort"));
        Assert.True(features.ContainsKey("emaMedium"));
        Assert.True(features.ContainsKey("emaLong"));

        // Momentum
        Assert.True(features.ContainsKey("rsi"));
        Assert.True(features.ContainsKey("macdLine"));
        Assert.True(features.ContainsKey("macdSignal"));
        Assert.True(features.ContainsKey("macdHistogram"));
        Assert.True(features.ContainsKey("stochasticK"));
        Assert.True(features.ContainsKey("stochasticD"));

        // Volatility
        Assert.True(features.ContainsKey("atr"));
        Assert.True(features.ContainsKey("bollingerUpper"));
        Assert.True(features.ContainsKey("bollingerMiddle"));
        Assert.True(features.ContainsKey("bollingerLower"));
        Assert.True(features.ContainsKey("bollingerBandwidth"));
        Assert.True(features.ContainsKey("bollingerPercentB"));

        // Volume
        Assert.True(features.ContainsKey("obv"));
        Assert.True(features.ContainsKey("volumeMa"));
        Assert.True(features.ContainsKey("relativeVolume"));
    }

    [Fact]
    public void ExtractFeatures_ContainsRegimeContext()
    {
        var features = FeatureExtractor.ExtractFeatures(
            CreateSampleIndicators(), CreateSampleContext());

        Assert.Equal("Bull", features["regimeLabel"]);
        Assert.True(features.ContainsKey("regimeConfidence"));
        Assert.True(features.ContainsKey("daysSinceRegimeChange"));
        Assert.True(features.ContainsKey("vixLevel"));
        Assert.True(features.ContainsKey("breadthScore"));
    }

    [Fact]
    public void ExtractFeatures_ContainsTradeContext()
    {
        var features = FeatureExtractor.ExtractFeatures(
            CreateSampleIndicators(), CreateSampleContext());

        Assert.Equal("Buy", features["orderSide"]);
        Assert.True(features.ContainsKey("winStreak"));
        Assert.True(features.ContainsKey("lossStreak"));
        Assert.True(features.ContainsKey("portfolioHeat"));
        Assert.True(features.ContainsKey("openPositionCount"));
        Assert.True(features.ContainsKey("recentWinRate"));
    }

    [Fact]
    public void ExtractFeatures_ContainsDerivedPriceFeatures()
    {
        var features = FeatureExtractor.ExtractFeatures(
            CreateSampleIndicators(), CreateSampleContext());

        Assert.True(features.ContainsKey("priceToSmaShort"));
        Assert.True(features.ContainsKey("priceToSmaLong"));
        Assert.True(features.ContainsKey("priceToEmaShort"));
        Assert.True(features.ContainsKey("atrPercent"));
        Assert.True(features.ContainsKey("dailyRange"));
    }

    // ── Compression & Hash ───────────────────────────────────────

    [Fact]
    public void CompressDecompress_RoundTrip_Succeeds()
    {
        var features = FeatureExtractor.ExtractFeatures(
            CreateSampleIndicators(), CreateSampleContext());

        var compressed = FeatureExtractor.CompressFeatures(features);
        Assert.False(string.IsNullOrWhiteSpace(compressed));

        var decompressed = FeatureExtractor.DecompressFeatures(compressed);
        Assert.NotNull(decompressed);
        Assert.Equal(features.Count, decompressed!.Count);
    }

    [Fact]
    public void CompressedJson_SmallerThanOriginal()
    {
        var features = FeatureExtractor.ExtractFeatures(
            CreateSampleIndicators(), CreateSampleContext());

        var compressed = FeatureExtractor.CompressFeatures(features);
        var json = System.Text.Json.JsonSerializer.Serialize(features);

        // Compressed base64 should be smaller than raw JSON for 40+ features
        Assert.True(compressed.Length < json.Length,
            $"Compressed {compressed.Length} should be < raw {json.Length}");
    }

    [Fact]
    public void ComputeHash_ReturnsSha256Hex()
    {
        var features = FeatureExtractor.ExtractFeatures(
            CreateSampleIndicators(), CreateSampleContext());

        var hash = FeatureExtractor.ComputeHash(features);

        Assert.NotNull(hash);
        Assert.Equal(64, hash.Length); // SHA256 = 64 hex chars
        Assert.Matches("^[0-9a-f]{64}$", hash);
    }

    [Fact]
    public void ComputeHash_SameInput_SameHash()
    {
        var indicators = CreateSampleIndicators();
        var context = CreateSampleContext();

        var features1 = FeatureExtractor.ExtractFeatures(indicators, context);
        var features2 = FeatureExtractor.ExtractFeatures(indicators, context);

        Assert.Equal(
            FeatureExtractor.ComputeHash(features1),
            FeatureExtractor.ComputeHash(features2));
    }

    [Fact]
    public void ComputeHash_DifferentInput_DifferentHash()
    {
        var indicators = CreateSampleIndicators();
        var context1 = CreateSampleContext();
        var context2 = CreateSampleContext() with { ClosePrice = 999m };

        var hash1 = FeatureExtractor.ComputeHash(
            FeatureExtractor.ExtractFeatures(indicators, context1));
        var hash2 = FeatureExtractor.ComputeHash(
            FeatureExtractor.ExtractFeatures(indicators, context2));

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void DecompressFeatures_EmptyString_ReturnsNull()
    {
        Assert.Null(FeatureExtractor.DecompressFeatures(""));
        Assert.Null(FeatureExtractor.DecompressFeatures(null!));
    }

    // ── Feature Version ──────────────────────────────────────────

    [Fact]
    public void CurrentVersion_IsPositive()
    {
        Assert.True(FeatureExtractor.CurrentVersion > 0);
    }

    // ── Win/Loss Streaks ─────────────────────────────────────────

    [Fact]
    public void ComputeStreaks_WinStreak_Correct()
    {
        var positions = new List<Position>
        {
            CreateClosedPosition(110m, 100m),  // win
            CreateClosedPosition(105m, 100m),  // win
            CreateClosedPosition(103m, 100m),  // win
            CreateClosedPosition(95m, 100m),   // loss
        };

        var (winStreak, lossStreak) = CaptureFeatureSnapshotHandler.ComputeStreaks(positions);
        Assert.Equal(3, winStreak);
        Assert.Equal(0, lossStreak);
    }

    [Fact]
    public void ComputeStreaks_LossStreak_Correct()
    {
        var positions = new List<Position>
        {
            CreateClosedPosition(90m, 100m),   // loss
            CreateClosedPosition(95m, 100m),   // loss
            CreateClosedPosition(110m, 100m),  // win
        };

        var (winStreak, lossStreak) = CaptureFeatureSnapshotHandler.ComputeStreaks(positions);
        Assert.Equal(0, winStreak);
        Assert.Equal(2, lossStreak);
    }

    [Fact]
    public void ComputeStreaks_Empty_ZeroBoth()
    {
        var (winStreak, lossStreak) = CaptureFeatureSnapshotHandler.ComputeStreaks(new List<Position>());
        Assert.Equal(0, winStreak);
        Assert.Equal(0, lossStreak);
    }

    [Fact]
    public void ComputeRecentWinRate_Mixed_Correct()
    {
        var positions = new List<Position>
        {
            CreateClosedPosition(110m, 100m),  // win
            CreateClosedPosition(90m, 100m),   // loss
            CreateClosedPosition(105m, 100m),  // win
            CreateClosedPosition(95m, 100m),   // loss
        };

        var winRate = CaptureFeatureSnapshotHandler.ComputeRecentWinRate(positions);
        Assert.Equal(0.5m, winRate);
    }

    [Fact]
    public void ComputeRecentWinRate_Empty_ReturnsZero()
    {
        Assert.Equal(0m, CaptureFeatureSnapshotHandler.ComputeRecentWinRate(new List<Position>()));
    }

    // ── CaptureFeatureSnapshotHandler Integration ────────────────

    [Fact]
    public async Task CaptureHandler_StoresSnapshotOnOrderFilled()
    {
        var dbName = Guid.NewGuid().ToString();
        using var marketDb = TestMarketDataDbContextFactory.Create(dbName + "_market");
        using var tradingDb = TestDbContextFactory.Create(dbName + "_trading");
        using var intelDb = TestIntelligenceDbContextFactory.Create(dbName + "_intel");
        var logger = NullLogger<CaptureFeatureSnapshotHandler>.Instance;

        // Set up a paper account
        var account = new Account
        {
            Name = "Test Paper",
            AccountType = TradingAssistant.Domain.Enums.AccountType.Paper,
            Balance = 100_000m
        };
        tradingDb.Accounts.Add(account);
        await tradingDb.SaveChangesAsync();

        var orderFilled = new OrderFilled(
            Guid.NewGuid(), account.Id, "AAPL", 10, 150m, 0m);

        await CaptureFeatureSnapshotHandler.HandleAsync(
            orderFilled, marketDb, tradingDb, intelDb, logger);

        var snapshots = intelDb.FeatureSnapshots.ToList();
        Assert.Single(snapshots);
        Assert.Equal("AAPL", snapshots[0].Symbol);
        Assert.Equal(orderFilled.OrderId, snapshots[0].TradeId);
        Assert.Equal(TradeOutcome.Pending, snapshots[0].TradeOutcome);
        Assert.True(snapshots[0].FeatureCount >= 40);
        Assert.Equal(64, snapshots[0].FeaturesHash.Length);
        Assert.True(snapshots[0].FeaturesJson.Length > 0);
        Assert.Equal(FeatureExtractor.CurrentVersion, snapshots[0].FeatureVersion);
    }

    [Fact]
    public async Task CaptureHandler_CompressedFeaturesDecompressable()
    {
        var dbName = Guid.NewGuid().ToString();
        using var marketDb = TestMarketDataDbContextFactory.Create(dbName + "_market");
        using var tradingDb = TestDbContextFactory.Create(dbName + "_trading");
        using var intelDb = TestIntelligenceDbContextFactory.Create(dbName + "_intel");
        var logger = NullLogger<CaptureFeatureSnapshotHandler>.Instance;

        var account = new Account
        {
            Name = "Test", AccountType = TradingAssistant.Domain.Enums.AccountType.Paper,
            Balance = 100_000m
        };
        tradingDb.Accounts.Add(account);
        await tradingDb.SaveChangesAsync();

        await CaptureFeatureSnapshotHandler.HandleAsync(
            new OrderFilled(Guid.NewGuid(), account.Id, "MSFT", 5, 300m, 0m),
            marketDb, tradingDb, intelDb, logger);

        var snapshot = intelDb.FeatureSnapshots.First();
        var features = FeatureExtractor.DecompressFeatures(snapshot.FeaturesJson);

        Assert.NotNull(features);
        Assert.True(features!.Count >= 40);
        Assert.True(features.ContainsKey("rsi"));
        Assert.True(features.ContainsKey("regimeLabel"));
        Assert.True(features.ContainsKey("portfolioHeat"));
    }

    // ── UpdateFeatureOutcomeHandler Integration ──────────────────

    [Fact]
    public async Task OutcomeHandler_UpdatesWinOutcome()
    {
        var dbName = Guid.NewGuid().ToString();
        using var tradingDb = TestDbContextFactory.Create(dbName + "_trading");
        using var intelDb = TestIntelligenceDbContextFactory.Create(dbName + "_intel");
        var logger = NullLogger<UpdateFeatureOutcomeHandler>.Instance;

        var accountId = Guid.NewGuid();
        var positionId = Guid.NewGuid();

        // Create a position
        tradingDb.Positions.Add(new Position
        {
            Id = positionId,
            AccountId = accountId,
            Symbol = "AAPL",
            Quantity = 10,
            AverageEntryPrice = 100m,
            CurrentPrice = 110m,
            Status = PositionStatus.Closed,
            OpenedAt = DateTime.UtcNow.AddDays(-5),
            ClosedAt = DateTime.UtcNow
        });
        await tradingDb.SaveChangesAsync();

        // Create a pending snapshot
        intelDb.FeatureSnapshots.Add(new FeatureSnapshot
        {
            TradeId = Guid.NewGuid(),
            Symbol = "AAPL",
            MarketCode = "US_SP500",
            CapturedAt = DateTime.UtcNow.AddDays(-5),
            FeatureVersion = 1,
            FeatureCount = 45,
            FeaturesJson = "compressed_data",
            FeaturesHash = "abc123",
            TradeOutcome = TradeOutcome.Pending
        });
        await intelDb.SaveChangesAsync();

        await UpdateFeatureOutcomeHandler.HandleAsync(
            new PositionClosed(positionId, accountId, "AAPL", 10, 110m, 100m),
            tradingDb, intelDb, logger);

        var snapshot = intelDb.FeatureSnapshots.First();
        Assert.Equal(TradeOutcome.Win, snapshot.TradeOutcome);
        Assert.NotNull(snapshot.TradePnlPercent);
        Assert.Equal(10m, snapshot.TradePnlPercent!.Value); // (110-100)/100 * 100 = 10%
        Assert.NotNull(snapshot.OutcomeUpdatedAt);
    }

    [Fact]
    public async Task OutcomeHandler_UpdatesLossOutcome()
    {
        var dbName = Guid.NewGuid().ToString();
        using var tradingDb = TestDbContextFactory.Create(dbName + "_trading");
        using var intelDb = TestIntelligenceDbContextFactory.Create(dbName + "_intel");
        var logger = NullLogger<UpdateFeatureOutcomeHandler>.Instance;

        var positionId = Guid.NewGuid();

        tradingDb.Positions.Add(new Position
        {
            Id = positionId,
            AccountId = Guid.NewGuid(),
            Symbol = "GOOG",
            Quantity = 5,
            AverageEntryPrice = 200m,
            CurrentPrice = 180m,
            Status = PositionStatus.Closed,
            OpenedAt = DateTime.UtcNow.AddDays(-10),
            ClosedAt = DateTime.UtcNow
        });
        await tradingDb.SaveChangesAsync();

        intelDb.FeatureSnapshots.Add(new FeatureSnapshot
        {
            TradeId = Guid.NewGuid(),
            Symbol = "GOOG",
            MarketCode = "US_SP500",
            CapturedAt = DateTime.UtcNow.AddDays(-10),
            FeatureVersion = 1, FeatureCount = 45,
            FeaturesJson = "data", FeaturesHash = "hash",
            TradeOutcome = TradeOutcome.Pending
        });
        await intelDb.SaveChangesAsync();

        await UpdateFeatureOutcomeHandler.HandleAsync(
            new PositionClosed(positionId, Guid.NewGuid(), "GOOG", 5, 180m, -100m),
            tradingDb, intelDb, logger);

        var snapshot = intelDb.FeatureSnapshots.First();
        Assert.Equal(TradeOutcome.Loss, snapshot.TradeOutcome);
        Assert.Equal(-10m, snapshot.TradePnlPercent); // (180-200)/200 * 100 = -10%
    }

    [Fact]
    public async Task OutcomeHandler_NoPendingSnapshots_NoOp()
    {
        var dbName = Guid.NewGuid().ToString();
        using var tradingDb = TestDbContextFactory.Create(dbName + "_trading");
        using var intelDb = TestIntelligenceDbContextFactory.Create(dbName + "_intel");
        var logger = NullLogger<UpdateFeatureOutcomeHandler>.Instance;

        var positionId = Guid.NewGuid();
        tradingDb.Positions.Add(new Position
        {
            Id = positionId, AccountId = Guid.NewGuid(), Symbol = "NVDA",
            Quantity = 1, AverageEntryPrice = 500m, CurrentPrice = 550m,
            Status = PositionStatus.Closed, OpenedAt = DateTime.UtcNow.AddDays(-1)
        });
        await tradingDb.SaveChangesAsync();

        // Should not throw
        await UpdateFeatureOutcomeHandler.HandleAsync(
            new PositionClosed(positionId, Guid.NewGuid(), "NVDA", 1, 550m, 50m),
            tradingDb, intelDb, logger);

        Assert.Empty(intelDb.FeatureSnapshots.ToList());
    }

    // ── Helpers ──────────────────────────────────────────────────

    private static IndicatorValues CreateSampleIndicators()
    {
        return new IndicatorValues
        {
            SmaShort = 148m, SmaMedium = 145m, SmaLong = 140m,
            EmaShort = 149m, EmaMedium = 146m, EmaLong = 141m,
            Rsi = 55m,
            MacdLine = 2.5m, MacdSignal = 2.0m, MacdHistogram = 0.5m,
            StochasticK = 65m, StochasticD = 60m,
            Atr = 3.5m,
            BollingerUpper = 155m, BollingerMiddle = 148m, BollingerLower = 141m,
            BollingerBandwidth = 14m, BollingerPercentB = 0.65m,
            Obv = 1_500_000m, VolumeMa = 1_200_000m, RelativeVolume = 1.25m,
            IsWarmedUp = true
        };
    }

    private static FeatureExtractor.FeatureContext CreateSampleContext()
    {
        return new FeatureExtractor.FeatureContext(
            ClosePrice: 150m, HighPrice: 152m, LowPrice: 148m, OpenPrice: 149m,
            RegimeLabel: "Bull", RegimeConfidence: 0.85m,
            DaysSinceRegimeChange: 15, VixLevel: 18.5m, BreadthScore: 0.65m,
            MarketCode: "US_SP500", Symbol: "AAPL", DayOfWeek: 3,
            OrderSide: "Buy", WinStreak: 3, LossStreak: 0,
            RecentWinRate: 0.6m, PortfolioHeat: 0.35m,
            OpenPositionCount: 4, TradePrice: 150m);
    }

    private static Position CreateClosedPosition(decimal currentPrice, decimal entryPrice)
    {
        return new Position
        {
            AccountId = Guid.NewGuid(),
            Symbol = "TEST",
            Quantity = 10,
            AverageEntryPrice = entryPrice,
            CurrentPrice = currentPrice,
            Status = PositionStatus.Closed,
            OpenedAt = DateTime.UtcNow.AddDays(-10),
            ClosedAt = DateTime.UtcNow
        };
    }
}
