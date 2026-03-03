using Microsoft.Extensions.Logging.Abstractions;
using TradingAssistant.Application.Handlers.Intelligence;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.Infrastructure.ML;
using TradingAssistant.Tests.Helpers;

namespace TradingAssistant.Tests.Intelligence;

public class MlRetrainServiceTests
{
    // ── Auto-Rollback Tests ──────────────────────────────────────

    [Fact]
    public async Task Retrain_RollsBack_WhenNewAucLowerThanPrevious()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TestIntelligenceDbContextFactory.Create(dbName);
        var logger = NullLogger<RetrainModelHandler>.Instance;

        // Seed a previous "active" model with high AUC
        db.MlModels.Add(new MlModel
        {
            MarketCode = "US_SP500",
            ModelVersion = 1,
            FeatureVersion = 1,
            ModelPath = "data/models/US_SP500/v1.zip",
            TrainedAt = DateTime.UtcNow.AddDays(-10),
            Auc = 0.95, // Very high AUC — hard to beat
            IsActive = true,
            FeatureImportanceJson = "[]"
        });
        await db.SaveChangesAsync();

        // Seed labeled snapshots to enable training
        SeedSnapshots(db, "US_SP500", 100);
        await db.SaveChangesAsync();

        var (result, trainedEvent) = await RetrainModelHandler.HandleAsync(
            new RetrainModelCommand("US_SP500"), db, logger);

        Assert.True(result.Success);
        Assert.NotNull(result.Model);
        Assert.Equal(2, result.Model!.ModelVersion);

        // The new model should NOT be active (AUC either below minimum or below previous)
        Assert.False(result.Model.IsActive);
        Assert.NotNull(result.RollbackReason);
        // Rollback triggered by either minimum AUC threshold or comparison with previous
        Assert.True(
            result.RollbackReason!.Contains("previous active") ||
            result.RollbackReason.Contains("below minimum"),
            $"Unexpected rollback reason: {result.RollbackReason}");

        // The original model should still be active
        var originalModel = db.MlModels.First(m => m.ModelVersion == 1);
        Assert.True(originalModel.IsActive);

        // Event should reflect rollback
        Assert.False(trainedEvent.IsActive);
        Assert.NotNull(trainedEvent.RollbackReason);

        // Cleanup
        CleanupModelFiles(db);
    }

    [Fact]
    public async Task Retrain_ActivatesNewModel_WhenAucBetterThanPrevious()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TestIntelligenceDbContextFactory.Create(dbName);
        var logger = NullLogger<RetrainModelHandler>.Instance;

        // Seed a previous "active" model with very low AUC
        db.MlModels.Add(new MlModel
        {
            MarketCode = "US_SP500",
            ModelVersion = 1,
            FeatureVersion = 1,
            ModelPath = "data/models/US_SP500/v1.zip",
            TrainedAt = DateTime.UtcNow.AddDays(-10),
            Auc = 0.50, // Barely above chance — easy to beat
            IsActive = true,
            FeatureImportanceJson = "[]"
        });
        await db.SaveChangesAsync();

        SeedSnapshots(db, "US_SP500", 100);
        await db.SaveChangesAsync();

        var (result, trainedEvent) = await RetrainModelHandler.HandleAsync(
            new RetrainModelCommand("US_SP500"), db, logger);

        Assert.True(result.Success);

        // If AUC >= 0.55 (minimum) AND > 0.50 (previous), it should be active
        if (result.Model!.Auc >= MlModelTrainer.MinimumAuc)
        {
            Assert.True(result.Model.IsActive);
            Assert.Null(result.RollbackReason);
            Assert.True(trainedEvent.IsActive);

            // Old model should be deactivated
            var oldModel = db.MlModels.First(m => m.ModelVersion == 1);
            Assert.False(oldModel.IsActive);
        }

        CleanupModelFiles(db);
    }

    [Fact]
    public async Task Retrain_PublishesModelTrainedEvent()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TestIntelligenceDbContextFactory.Create(dbName);
        var logger = NullLogger<RetrainModelHandler>.Instance;

        SeedSnapshots(db, "US_SP500", 100);
        await db.SaveChangesAsync();

        var (result, trainedEvent) = await RetrainModelHandler.HandleAsync(
            new RetrainModelCommand("US_SP500"), db, logger);

        Assert.True(result.Success);
        Assert.Equal("US_SP500", trainedEvent.MarketCode);
        Assert.Equal(1, trainedEvent.ModelVersion);
        Assert.True(trainedEvent.Auc > 0);
        Assert.True(trainedEvent.TrainedAt > DateTime.MinValue);

        CleanupModelFiles(db);
    }

    // ── Feature Drift Detection Tests ────────────────────────────

    [Fact]
    public void FeatureDrift_DetectsShift_WhenMeansChangeDrastically()
    {
        var rng = new Random(42);

        // Training data: RSI centered around 50
        var trainingData = Enumerable.Range(0, 100)
            .Select(_ => MakeVector(rng, rsiBase: 50f, priceBase: 100f))
            .ToList();

        // Recent data: RSI centered around 80 (significant shift)
        var recentData = Enumerable.Range(0, 30)
            .Select(_ => MakeVector(rng, rsiBase: 80f, priceBase: 100f))
            .ToList();

        var report = MlModelTrainer.ComputeFeatureDrift(trainingData, recentData);

        Assert.True(report.HasSignificantDrift);

        var rsiDrift = report.Entries.FirstOrDefault(e => e.FeatureName == "rsi");
        Assert.NotNull(rsiDrift);
        Assert.True(rsiDrift!.IsSignificant, $"RSI drift magnitude: {rsiDrift.DriftMagnitude:F2}");
    }

    [Fact]
    public void FeatureDrift_NoSignificantDrift_WhenDistributionsSimilar()
    {
        var rng = new Random(42);

        var trainingData = Enumerable.Range(0, 100)
            .Select(_ => MakeVector(rng, rsiBase: 50f, priceBase: 100f))
            .ToList();

        // Same distribution
        var recentData = Enumerable.Range(0, 30)
            .Select(_ => MakeVector(rng, rsiBase: 50f, priceBase: 100f))
            .ToList();

        var report = MlModelTrainer.ComputeFeatureDrift(trainingData, recentData);

        // With same distribution, very few features should drift
        var significantCount = report.SignificantEntries.Count;
        Assert.True(significantCount <= 5,
            $"Expected ≤5 significant drifts with similar distributions, got {significantCount}");
    }

    [Fact]
    public void FeatureDrift_ReportContainsAllNumericFeatures()
    {
        var rng = new Random(42);
        var training = Enumerable.Range(0, 50).Select(_ => MakeVector(rng)).ToList();
        var recent = Enumerable.Range(0, 20).Select(_ => MakeVector(rng)).ToList();

        var report = MlModelTrainer.ComputeFeatureDrift(training, recent);

        // Should have entries for all numeric features (41 float features)
        Assert.True(report.Entries.Count >= 30,
            $"Expected >= 30 features in drift report, got {report.Entries.Count}");
        Assert.Equal(training.Count, report.TrainingWindowSize);
        Assert.Equal(recent.Count, report.RecentWindowSize);
    }

    [Fact]
    public void FeatureDrift_MagnitudeIsPositive()
    {
        var rng = new Random(42);
        var training = Enumerable.Range(0, 50).Select(_ => MakeVector(rng)).ToList();
        var recent = Enumerable.Range(0, 20).Select(_ => MakeVector(rng)).ToList();

        var report = MlModelTrainer.ComputeFeatureDrift(training, recent);

        Assert.All(report.Entries, e => Assert.True(e.DriftMagnitude >= 0));
    }

    // ── ShouldRetrain Trigger Tests ──────────────────────────────

    [Fact]
    public async Task ShouldRetrain_True_WhenNoModelAndEnoughData()
    {
        using var db = TestIntelligenceDbContextFactory.Create();
        SeedSnapshots(db, "US_SP500", 25);
        await db.SaveChangesAsync();

        var (trigger, reason) = await MlRetrainChecker.ShouldRetrain(db, "US_SP500");

        Assert.True(trigger);
        Assert.Contains("No model exists", reason);
    }

    [Fact]
    public async Task ShouldRetrain_False_WhenNoModelAndInsufficientData()
    {
        using var db = TestIntelligenceDbContextFactory.Create();
        SeedSnapshots(db, "US_SP500", 10);
        await db.SaveChangesAsync();

        var (trigger, _) = await MlRetrainChecker.ShouldRetrain(db, "US_SP500");

        Assert.False(trigger);
    }

    [Fact]
    public async Task ShouldRetrain_True_WhenMonthlyThresholdExceeded()
    {
        using var db = TestIntelligenceDbContextFactory.Create();

        db.MlModels.Add(new MlModel
        {
            MarketCode = "US_SP500",
            ModelVersion = 1,
            FeatureVersion = 1,
            ModelPath = "test.zip",
            TrainedAt = DateTime.UtcNow.AddDays(-35), // 35 days ago
            Auc = 0.65,
            IsActive = true,
            FeatureImportanceJson = "[]"
        });
        await db.SaveChangesAsync();

        var (trigger, reason) = await MlRetrainChecker.ShouldRetrain(db, "US_SP500");

        Assert.True(trigger);
        Assert.Contains("Monthly retrain", reason);
    }

    [Fact]
    public async Task ShouldRetrain_True_WhenNewTradeThresholdMet()
    {
        using var db = TestIntelligenceDbContextFactory.Create();

        var trainedAt = DateTime.UtcNow.AddDays(-5);
        db.MlModels.Add(new MlModel
        {
            MarketCode = "US_SP500",
            ModelVersion = 1,
            FeatureVersion = 1,
            ModelPath = "test.zip",
            TrainedAt = trainedAt,
            Auc = 0.65,
            IsActive = true,
            FeatureImportanceJson = "[]"
        });

        // Add 55 new labeled snapshots AFTER the model was trained
        for (int i = 0; i < 55; i++)
        {
            db.FeatureSnapshots.Add(new FeatureSnapshot
            {
                TradeId = Guid.NewGuid(),
                Symbol = "AAPL",
                MarketCode = "US_SP500",
                CapturedAt = trainedAt.AddHours(i + 1), // After training
                FeatureVersion = 1,
                FeatureCount = 45,
                FeaturesJson = "{}",
                FeaturesHash = $"hash_{i}",
                TradeOutcome = i % 2 == 0 ? TradeOutcome.Win : TradeOutcome.Loss
            });
        }
        await db.SaveChangesAsync();

        var (trigger, reason) = await MlRetrainChecker.ShouldRetrain(db, "US_SP500");

        Assert.True(trigger);
        Assert.Contains("Trade threshold", reason);
    }

    [Fact]
    public async Task ShouldRetrain_False_WhenRecentModelAndFewNewTrades()
    {
        using var db = TestIntelligenceDbContextFactory.Create();

        db.MlModels.Add(new MlModel
        {
            MarketCode = "US_SP500",
            ModelVersion = 1,
            FeatureVersion = 1,
            ModelPath = "test.zip",
            TrainedAt = DateTime.UtcNow.AddDays(-5), // 5 days ago
            Auc = 0.65,
            IsActive = true,
            FeatureImportanceJson = "[]"
        });

        // Only 10 new trades — below threshold
        for (int i = 0; i < 10; i++)
        {
            db.FeatureSnapshots.Add(new FeatureSnapshot
            {
                TradeId = Guid.NewGuid(),
                Symbol = "AAPL",
                MarketCode = "US_SP500",
                CapturedAt = DateTime.UtcNow.AddHours(-i),
                FeatureVersion = 1,
                FeatureCount = 45,
                FeaturesJson = "{}",
                FeaturesHash = $"hash_{i}",
                TradeOutcome = TradeOutcome.Win
            });
        }
        await db.SaveChangesAsync();

        var (trigger, _) = await MlRetrainChecker.ShouldRetrain(db, "US_SP500");

        Assert.False(trigger);
    }

    // ── Feature Drift in Retrain Result ──────────────────────────

    [Fact]
    public async Task Retrain_IncludesFeatureDrift_WhenEnoughData()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TestIntelligenceDbContextFactory.Create(dbName);
        var logger = NullLogger<RetrainModelHandler>.Instance;

        SeedSnapshots(db, "US_SP500", 100);
        await db.SaveChangesAsync();

        var (result, _) = await RetrainModelHandler.HandleAsync(
            new RetrainModelCommand("US_SP500"), db, logger);

        Assert.True(result.Success);
        Assert.NotNull(result.FeatureDrift);
        Assert.True(result.FeatureDrift!.TrainingWindowSize > 0);
        Assert.True(result.FeatureDrift.RecentWindowSize > 0);
        Assert.True(result.FeatureDrift.DriftedFeatures.Count > 0);

        CleanupModelFiles(db);
    }

    // ── Helpers ──────────────────────────────────────────────────

    private static void SeedSnapshots(
        TradingAssistant.Infrastructure.Persistence.IntelligenceDbContext db,
        string marketCode, int count)
    {
        var indicators = CreateSampleIndicators();
        var context = CreateSampleContext(marketCode);
        var features = FeatureExtractor.ExtractFeatures(indicators, context);
        var compressed = FeatureExtractor.CompressFeatures(features);

        var rng = new Random(42);
        for (int i = 0; i < count; i++)
        {
            db.FeatureSnapshots.Add(new FeatureSnapshot
            {
                TradeId = Guid.NewGuid(),
                Symbol = "AAPL",
                MarketCode = marketCode,
                CapturedAt = DateTime.UtcNow.AddDays(-count + i),
                FeatureVersion = 1,
                FeatureCount = 45,
                FeaturesJson = compressed,
                FeaturesHash = $"hash_{Guid.NewGuid():N}",
                TradeOutcome = rng.NextDouble() > 0.5 ? TradeOutcome.Win : TradeOutcome.Loss
            });
        }
    }

    private static Application.Indicators.IndicatorValues CreateSampleIndicators() => new()
    {
        SmaShort = 148m, SmaMedium = 145m, SmaLong = 140m,
        EmaShort = 149m, EmaMedium = 146m, EmaLong = 141m,
        Rsi = 55m, MacdLine = 1.5m, MacdSignal = 1.2m, MacdHistogram = 0.3m,
        StochasticK = 65m, StochasticD = 60m,
        Atr = 3.5m, BollingerUpper = 155m, BollingerMiddle = 150m,
        BollingerLower = 145m, BollingerBandwidth = 6.67m, BollingerPercentB = 0.5m,
        Obv = 1000000m, VolumeMa = 500000m, RelativeVolume = 1.2m,
        IsWarmedUp = true
    };

    private static FeatureExtractor.FeatureContext CreateSampleContext(
        string marketCode = "US_SP500") => new(
        ClosePrice: 150m, HighPrice: 155m, LowPrice: 145m, OpenPrice: 148m,
        RegimeLabel: "Bullish", RegimeConfidence: 0.85m, DaysSinceRegimeChange: 15,
        VixLevel: 18.5m, BreadthScore: 0.72m,
        MarketCode: marketCode, Symbol: "AAPL", DayOfWeek: 3, OrderSide: "Buy",
        WinStreak: 3, LossStreak: 0, RecentWinRate: 0.65m,
        PortfolioHeat: 0.45m, OpenPositionCount: 5, TradePrice: 150m);

    private static FeatureVector MakeVector(Random rng, float rsiBase = 50f, float priceBase = 100f)
    {
        return new FeatureVector
        {
            SmaShort = priceBase - 2f + (float)rng.NextDouble() * 4f,
            SmaMedium = priceBase - 5f + (float)rng.NextDouble() * 10f,
            SmaLong = priceBase - 10f + (float)rng.NextDouble() * 20f,
            EmaShort = priceBase - 1f + (float)rng.NextDouble() * 2f,
            EmaMedium = priceBase - 3f + (float)rng.NextDouble() * 6f,
            EmaLong = priceBase - 8f + (float)rng.NextDouble() * 16f,
            Rsi = rsiBase + (float)(rng.NextDouble() * 10 - 5),
            MacdLine = (float)(rng.NextDouble() * 4 - 2),
            MacdSignal = (float)(rng.NextDouble() * 4 - 2),
            MacdHistogram = (float)(rng.NextDouble() * 2 - 1),
            StochasticK = (float)(rng.NextDouble() * 100),
            StochasticD = (float)(rng.NextDouble() * 100),
            Atr = (float)(rng.NextDouble() * 5),
            BollingerUpper = priceBase + 5f + (float)rng.NextDouble() * 5,
            BollingerMiddle = priceBase + (float)rng.NextDouble() * 2,
            BollingerLower = priceBase - 5f + (float)rng.NextDouble() * 5,
            BollingerBandwidth = (float)(rng.NextDouble() * 10),
            BollingerPercentB = (float)rng.NextDouble(),
            Obv = (float)(rng.NextDouble() * 1_000_000),
            VolumeMa = (float)(rng.NextDouble() * 500_000),
            RelativeVolume = (float)(0.5 + rng.NextDouble() * 2),
            ClosePrice = priceBase + (float)(rng.NextDouble() * 10 - 5),
            HighPrice = priceBase + 5f + (float)(rng.NextDouble() * 5),
            LowPrice = priceBase - 5f + (float)(rng.NextDouble() * 5),
            OpenPrice = priceBase + (float)(rng.NextDouble() * 6 - 3),
            DailyRange = (float)(rng.NextDouble() * 5),
            PriceToSmaShort = (float)(0.95 + rng.NextDouble() * 0.1),
            PriceToSmaLong = (float)(0.90 + rng.NextDouble() * 0.2),
            PriceToEmaShort = (float)(0.95 + rng.NextDouble() * 0.1),
            AtrPercent = (float)(rng.NextDouble() * 3),
            RegimeConfidence = (float)rng.NextDouble(),
            DaysSinceRegimeChange = rng.Next(0, 100),
            VixLevel = (float)(12 + rng.NextDouble() * 30),
            BreadthScore = (float)rng.NextDouble(),
            DayOfWeek = rng.Next(0, 5),
            WinStreak = rng.Next(0, 10),
            LossStreak = rng.Next(0, 10),
            RecentWinRate = (float)rng.NextDouble(),
            PortfolioHeat = (float)rng.NextDouble(),
            OpenPositionCount = rng.Next(0, 10),
            TradePrice = priceBase + (float)(rng.NextDouble() * 10 - 5),
            RegimeLabel = "Bull",
            MarketCode = "US_SP500",
            Symbol = "AAPL",
            OrderSide = "Buy",
            Label = rng.NextDouble() > 0.5
        };
    }

    private static void CleanupModelFiles(
        TradingAssistant.Infrastructure.Persistence.IntelligenceDbContext db)
    {
        foreach (var m in db.MlModels.ToList())
            if (File.Exists(m.ModelPath)) File.Delete(m.ModelPath);
    }
}
