using Microsoft.Extensions.Logging.Abstractions;
using TradingAssistant.Application.Handlers.Intelligence;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.Infrastructure.ML;
using TradingAssistant.Tests.Helpers;

namespace TradingAssistant.Tests.Intelligence;

public class MlModelTrainerTests
{
    // ── Trainer.Train ─────────────────────────────────────────────

    [Fact]
    public void Train_WithSyntheticData_ProducesModel()
    {
        var data = GenerateSyntheticData(100);
        var trainer = new MlModelTrainer();

        var result = trainer.Train(data);

        Assert.True(result.Success);
        Assert.NotNull(result.Model);
        Assert.NotNull(result.MlContext);
        Assert.True(result.Auc > 0, "AUC should be positive");
        Assert.True(result.TrainingSamples > 0);
        Assert.True(result.ValidationSamples > 0);
    }

    [Fact]
    public void Train_OutputsProbabilityScore()
    {
        var data = GenerateSyntheticData(100);
        var trainer = new MlModelTrainer();

        var result = trainer.Train(data);
        Assert.True(result.Success);

        // Use model to predict
        var predEngine = result.MlContext!.Model
            .CreatePredictionEngine<FeatureVector, MlPrediction>(result.Model);

        var testSample = data[^1]; // last sample
        var prediction = predEngine.Predict(testSample);

        // Probability should be between 0 and 1
        Assert.InRange(prediction.Probability, 0f, 1f);
    }

    [Fact]
    public void Train_WalkForwardSplit_NoFutureLeakage()
    {
        var data = GenerateSyntheticData(100);
        var trainer = new MlModelTrainer();

        var result = trainer.Train(data, trainSplitRatio: 0.7);

        Assert.True(result.Success);
        Assert.Equal(70, result.TrainingSamples);
        Assert.Equal(30, result.ValidationSamples);
    }

    [Fact]
    public void Train_InsufficientData_ReturnsFalse()
    {
        var data = GenerateSyntheticData(10);
        var trainer = new MlModelTrainer();

        var result = trainer.Train(data);

        Assert.False(result.Success);
        Assert.Contains("Insufficient", result.FailureReason);
    }

    [Fact]
    public void Train_InsufficientValidation_ReturnsFalse()
    {
        var data = GenerateSyntheticData(25);
        var trainer = new MlModelTrainer();

        // 99% train split leaves ~0 validation
        var result = trainer.Train(data, trainSplitRatio: 0.99);

        Assert.False(result.Success);
    }

    [Fact]
    public void Train_ReturnsMetrics()
    {
        var data = GenerateSyntheticData(200);
        var trainer = new MlModelTrainer();

        var result = trainer.Train(data);

        Assert.True(result.Success);
        Assert.InRange(result.Auc, 0, 1);
        Assert.InRange(result.Precision, 0, 1);
        Assert.InRange(result.Recall, 0, 1);
        Assert.InRange(result.F1Score, 0, 1);
        Assert.InRange(result.Accuracy, 0, 1);
    }

    [Fact]
    public void Train_ReportsWinLossCounts()
    {
        var data = GenerateSyntheticData(100);
        var trainer = new MlModelTrainer();

        var result = trainer.Train(data);

        Assert.True(result.Success);
        Assert.Equal(data.Count(v => v.Label), result.WinSamples);
        Assert.Equal(data.Count(v => !v.Label), result.LossSamples);
        Assert.Equal(100, result.WinSamples + result.LossSamples);
    }

    // ── AUC Gate ──────────────────────────────────────────────────

    [Fact]
    public void MinimumAuc_IsPointFiveFive()
    {
        Assert.Equal(0.55, MlModelTrainer.MinimumAuc);
    }

    // ── Save / Load Model ─────────────────────────────────────────

    [Fact]
    public void SaveAndLoad_ModelRoundtrip()
    {
        var data = GenerateSyntheticData(100);
        var trainer = new MlModelTrainer();
        var result = trainer.Train(data);

        Assert.True(result.Success);

        var tempPath = Path.Combine(Path.GetTempPath(), $"test_model_{Guid.NewGuid()}.zip");
        try
        {
            trainer.SaveModel(result, tempPath);
            Assert.True(File.Exists(tempPath));

            var (ctx, model) = trainer.LoadModel(tempPath);
            Assert.NotNull(ctx);
            Assert.NotNull(model);

            // Loaded model can predict
            var predEngine = ctx.Model.CreatePredictionEngine<FeatureVector, MlPrediction>(model);
            var prediction = predEngine.Predict(data[0]);
            Assert.InRange(prediction.Probability, 0f, 1f);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    // ── Feature Importance ────────────────────────────────────────

    [Fact]
    public void Train_TopFeaturesJsonIsValidJson()
    {
        var data = GenerateSyntheticData(200);
        var trainer = new MlModelTrainer();

        var result = trainer.Train(data);

        Assert.True(result.Success);
        Assert.NotNull(result.TopFeatures);
        // TopFeaturesJson is always valid JSON (may be empty list if PFI unavailable)
        Assert.NotNull(result.TopFeaturesJson);
        Assert.StartsWith("[", result.TopFeaturesJson);
    }

    // ── MlModel Entity ───────────────────────────────────────────

    [Fact]
    public void MlModel_HasRequiredFields()
    {
        var model = new MlModel
        {
            MarketCode = "US_SP500",
            ModelVersion = 1,
            FeatureVersion = 1,
            ModelPath = "data/models/US_SP500/v1.zip",
            TrainedAt = DateTime.UtcNow,
            Auc = 0.72,
            Precision = 0.68,
            Recall = 0.75,
            F1Score = 0.71,
            Accuracy = 0.70,
            TrainingSamples = 800,
            ValidationSamples = 200,
            WinSamples = 500,
            LossSamples = 500,
            IsActive = true
        };

        Assert.Equal("US_SP500", model.MarketCode);
        Assert.True(model.Auc > MlModelTrainer.MinimumAuc);
        Assert.True(model.IsActive);
    }

    // ── RetrainModelHandler ───────────────────────────────────────

    [Fact]
    public async Task RetrainHandler_InsufficientData_ReturnsFailure()
    {
        var dbName = Guid.NewGuid().ToString();
        using var intelDb = TestIntelligenceDbContextFactory.Create(dbName);
        var logger = NullLogger<RetrainModelHandler>.Instance;

        // Only 5 labeled snapshots (below minimum 20)
        for (int i = 0; i < 5; i++)
        {
            intelDb.FeatureSnapshots.Add(MakeSnapshot("AAPL", i % 2 == 0 ? TradeOutcome.Win : TradeOutcome.Loss));
        }
        await intelDb.SaveChangesAsync();

        var result = await RetrainModelHandler.HandleAsync(
            new RetrainModelCommand("US_SP500"), intelDb, logger);

        Assert.False(result.Success);
        Assert.Null(result.Model);
    }

    [Fact]
    public async Task RetrainHandler_WithData_TrainsAndPersists()
    {
        var dbName = Guid.NewGuid().ToString();
        using var intelDb = TestIntelligenceDbContextFactory.Create(dbName);
        var logger = NullLogger<RetrainModelHandler>.Instance;

        // Create 100 labeled snapshots with real compressed features
        var indicators = CreateSampleIndicators();
        var context = CreateSampleContext();
        var features = FeatureExtractor.ExtractFeatures(indicators, context);
        var compressed = FeatureExtractor.CompressFeatures(features);

        var random = new Random(42);
        for (int i = 0; i < 100; i++)
        {
            var snapshot = new FeatureSnapshot
            {
                TradeId = Guid.NewGuid(),
                Symbol = "AAPL",
                MarketCode = "US_SP500",
                CapturedAt = DateTime.UtcNow.AddDays(-100 + i),
                FeatureVersion = 1,
                FeatureCount = 45,
                FeaturesJson = compressed,
                FeaturesHash = "hash",
                TradeOutcome = random.NextDouble() > 0.5 ? TradeOutcome.Win : TradeOutcome.Loss
            };
            intelDb.FeatureSnapshots.Add(snapshot);
        }
        await intelDb.SaveChangesAsync();

        var result = await RetrainModelHandler.HandleAsync(
            new RetrainModelCommand("US_SP500"), intelDb, logger);

        Assert.True(result.Success);
        Assert.NotNull(result.Model);
        Assert.Equal("US_SP500", result.Model!.MarketCode);
        Assert.Equal(1, result.Model.ModelVersion);
        Assert.True(result.Model.TrainingSamples > 0);

        // Verify persisted to DB
        var persisted = intelDb.MlModels.FirstOrDefault();
        Assert.NotNull(persisted);
        Assert.Equal(1, persisted!.ModelVersion);

        // Cleanup model file
        if (File.Exists(persisted.ModelPath))
            File.Delete(persisted.ModelPath);
    }

    [Fact]
    public async Task RetrainHandler_VersionIncrementsOnSecondTraining()
    {
        var dbName = Guid.NewGuid().ToString();
        using var intelDb = TestIntelligenceDbContextFactory.Create(dbName);
        var logger = NullLogger<RetrainModelHandler>.Instance;

        var indicators = CreateSampleIndicators();
        var context = CreateSampleContext();
        var features = FeatureExtractor.ExtractFeatures(indicators, context);
        var compressed = FeatureExtractor.CompressFeatures(features);

        var random = new Random(42);
        for (int i = 0; i < 100; i++)
        {
            intelDb.FeatureSnapshots.Add(new FeatureSnapshot
            {
                TradeId = Guid.NewGuid(),
                Symbol = "AAPL",
                MarketCode = "US_SP500",
                CapturedAt = DateTime.UtcNow.AddDays(-100 + i),
                FeatureVersion = 1, FeatureCount = 45,
                FeaturesJson = compressed, FeaturesHash = "hash",
                TradeOutcome = random.NextDouble() > 0.5 ? TradeOutcome.Win : TradeOutcome.Loss
            });
        }
        await intelDb.SaveChangesAsync();

        // Train first model
        await RetrainModelHandler.HandleAsync(
            new RetrainModelCommand("US_SP500"), intelDb, logger);

        // Train second model
        var result2 = await RetrainModelHandler.HandleAsync(
            new RetrainModelCommand("US_SP500"), intelDb, logger);

        Assert.True(result2.Success);
        Assert.Equal(2, result2.Model!.ModelVersion);

        // Cleanup
        foreach (var m in intelDb.MlModels.ToList())
            if (File.Exists(m.ModelPath)) File.Delete(m.ModelPath);
    }

    // ── Helpers ────────────────────────────────────────────────────

    private static List<FeatureVector> GenerateSyntheticData(int count)
    {
        var random = new Random(42);
        var data = new List<FeatureVector>();

        for (int i = 0; i < count; i++)
        {
            var rsi = (float)(random.NextDouble() * 100);
            var isWin = rsi > 50; // Simple pattern: high RSI → win

            data.Add(new FeatureVector
            {
                SmaShort = 148f + (float)random.NextDouble() * 10,
                SmaMedium = 145f + (float)random.NextDouble() * 10,
                SmaLong = 140f + (float)random.NextDouble() * 10,
                EmaShort = 149f + (float)random.NextDouble() * 10,
                EmaMedium = 146f + (float)random.NextDouble() * 10,
                EmaLong = 141f + (float)random.NextDouble() * 10,
                Rsi = rsi,
                MacdLine = (float)(random.NextDouble() * 5 - 2.5),
                MacdSignal = (float)(random.NextDouble() * 5 - 2.5),
                MacdHistogram = (float)(random.NextDouble() * 2 - 1),
                StochasticK = (float)(random.NextDouble() * 100),
                StochasticD = (float)(random.NextDouble() * 100),
                Atr = (float)(random.NextDouble() * 5 + 1),
                BollingerUpper = 160f + (float)random.NextDouble() * 5,
                BollingerMiddle = 150f + (float)random.NextDouble() * 5,
                BollingerLower = 140f + (float)random.NextDouble() * 5,
                BollingerBandwidth = (float)(random.NextDouble() * 10),
                BollingerPercentB = (float)random.NextDouble(),
                Obv = (float)(random.NextDouble() * 2_000_000),
                VolumeMa = (float)(random.NextDouble() * 1_000_000),
                RelativeVolume = (float)(random.NextDouble() * 3),
                ClosePrice = 150f + (float)random.NextDouble() * 20,
                HighPrice = 155f + (float)random.NextDouble() * 20,
                LowPrice = 145f + (float)random.NextDouble() * 10,
                OpenPrice = 148f + (float)random.NextDouble() * 15,
                DailyRange = (float)(random.NextDouble() * 10),
                PriceToSmaShort = 1f + (float)random.NextDouble() * 0.1f,
                PriceToSmaLong = 1f + (float)random.NextDouble() * 0.15f,
                PriceToEmaShort = 1f + (float)random.NextDouble() * 0.1f,
                AtrPercent = (float)(random.NextDouble() * 5),
                RegimeLabel = "Bullish",
                RegimeConfidence = (float)random.NextDouble(),
                DaysSinceRegimeChange = random.Next(1, 60),
                VixLevel = 15f + (float)random.NextDouble() * 20,
                BreadthScore = (float)random.NextDouble(),
                MarketCode = "US_SP500",
                Symbol = "AAPL",
                DayOfWeek = random.Next(0, 5),
                OrderSide = "Buy",
                WinStreak = random.Next(0, 5),
                LossStreak = random.Next(0, 5),
                RecentWinRate = (float)random.NextDouble(),
                PortfolioHeat = (float)(random.NextDouble() * 0.8),
                OpenPositionCount = random.Next(0, 10),
                TradePrice = 150f + (float)random.NextDouble() * 20,
                Label = isWin
            });
        }

        return data;
    }

    private static FeatureSnapshot MakeSnapshot(string symbol, TradeOutcome outcome)
    {
        var indicators = CreateSampleIndicators();
        var context = CreateSampleContext();
        var features = FeatureExtractor.ExtractFeatures(indicators, context);
        var compressed = FeatureExtractor.CompressFeatures(features);

        return new FeatureSnapshot
        {
            TradeId = Guid.NewGuid(),
            Symbol = symbol,
            MarketCode = "US_SP500",
            CapturedAt = DateTime.UtcNow,
            FeatureVersion = 1,
            FeatureCount = 45,
            FeaturesJson = compressed,
            FeaturesHash = "hash",
            TradeOutcome = outcome
        };
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

    private static FeatureExtractor.FeatureContext CreateSampleContext() => new(
        ClosePrice: 150m, HighPrice: 155m, LowPrice: 145m, OpenPrice: 148m,
        RegimeLabel: "Bullish", RegimeConfidence: 0.85m, DaysSinceRegimeChange: 15,
        VixLevel: 18.5m, BreadthScore: 0.72m,
        MarketCode: "US_SP500", Symbol: "AAPL", DayOfWeek: 3, OrderSide: "Buy",
        WinStreak: 3, LossStreak: 0, RecentWinRate: 0.65m,
        PortfolioHeat: 0.45m, OpenPositionCount: 5, TradePrice: 150m);
}
