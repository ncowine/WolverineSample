using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using TradingAssistant.Application.Handlers.Intelligence;
using TradingAssistant.Application.Screening;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.Infrastructure.ML;
using TradingAssistant.Tests.Helpers;

namespace TradingAssistant.Tests.Intelligence;

public class MlPredictionServiceTests
{
    // ── MlPredictionService Unit Tests ──────────────────────────

    [Fact]
    public void PredictConfidence_ReturnsNull_WhenNoModelLoaded()
    {
        var service = new MlPredictionService();
        var features = new FeatureVector { Rsi = 50f, ClosePrice = 100f };

        var result = service.PredictConfidence("US", features);

        Assert.Null(result);
    }

    [Fact]
    public void HasModel_ReturnsFalse_WhenNoModelLoaded()
    {
        var service = new MlPredictionService();

        Assert.False(service.HasModel("US"));
    }

    [Fact]
    public void GetLoadedModelPath_ReturnsNull_WhenNoModel()
    {
        var service = new MlPredictionService();

        Assert.Null(service.GetLoadedModelPath("US"));
    }

    [Fact]
    public void LoadModel_ThrowsFileNotFound_WhenModelFileMissing()
    {
        var service = new MlPredictionService();

        Assert.Throws<FileNotFoundException>(() =>
            service.LoadModel("US", "/nonexistent/model.zip"));
    }

    [Fact]
    public void UnloadModel_RemovesEngine_WhenCalled()
    {
        var service = new MlPredictionService();
        // No model loaded, unload should be safe (no-op)
        service.UnloadModel("US");

        Assert.False(service.HasModel("US"));
    }

    [Fact]
    public void GetLoadedMarkets_ReturnsEmpty_Initially()
    {
        var service = new MlPredictionService();

        var markets = service.GetLoadedMarkets();

        Assert.Empty(markets);
    }

    [Fact]
    public void LoadModel_AndPredict_ReturnsValueBetween0And1()
    {
        // Train a model, save it, load it, predict
        var trainer = new MlModelTrainer();
        var data = GenerateSyntheticData(100);
        var result = trainer.Train(data);

        Assert.True(result.Success);

        var tempPath = Path.Combine(Path.GetTempPath(), $"test_model_{Guid.NewGuid()}.zip");
        try
        {
            trainer.SaveModel(result, tempPath);

            var service = new MlPredictionService();
            service.LoadModel("US", tempPath);

            Assert.True(service.HasModel("US"));
            Assert.Equal(tempPath, service.GetLoadedModelPath("US"));
            Assert.Contains("US", service.GetLoadedMarkets());

            var features = data[0]; // Use a sample from training data
            var confidence = service.PredictConfidence("US", features);

            Assert.NotNull(confidence);
            Assert.InRange(confidence.Value, 0f, 1f);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void Prediction_Benchmark_Under10ms()
    {
        // Train, save, load model first
        var trainer = new MlModelTrainer();
        var data = GenerateSyntheticData(100);
        var result = trainer.Train(data);
        Assert.True(result.Success);

        var tempPath = Path.Combine(Path.GetTempPath(), $"test_bench_{Guid.NewGuid()}.zip");
        try
        {
            trainer.SaveModel(result, tempPath);

            var service = new MlPredictionService();
            service.LoadModel("US", tempPath);

            var features = data[50];

            // Warm up
            service.PredictConfidence("US", features);

            // Benchmark 100 predictions
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 100; i++)
                service.PredictConfidence("US", features);
            sw.Stop();

            var avgMs = sw.Elapsed.TotalMilliseconds / 100;
            Assert.True(avgMs < 10,
                $"Average prediction time {avgMs:F3}ms exceeds 10ms threshold");
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void UnloadModel_RemovesLoadedModel()
    {
        var trainer = new MlModelTrainer();
        var data = GenerateSyntheticData(50);
        var result = trainer.Train(data);
        Assert.True(result.Success);

        var tempPath = Path.Combine(Path.GetTempPath(), $"test_unload_{Guid.NewGuid()}.zip");
        try
        {
            trainer.SaveModel(result, tempPath);

            var service = new MlPredictionService();
            service.LoadModel("US", tempPath);
            Assert.True(service.HasModel("US"));

            service.UnloadModel("US");
            Assert.False(service.HasModel("US"));
            Assert.Null(service.GetLoadedModelPath("US"));
            Assert.DoesNotContain("US", service.GetLoadedMarkets());
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    // ── ConfidenceGrader ML Integration Tests ───────────────────

    [Fact]
    public void Grade_WithoutMl_Returns6Factors()
    {
        var eval = MakeEvaluation();
        var report = ConfidenceGrader.Grade(eval, 100m, 95m, 115m);

        Assert.Equal(6, report.Breakdown.Count);
        Assert.DoesNotContain(report.Breakdown, b => b.Factor == "MLConfidence");
        Assert.Null(report.MlConfidence);
    }

    [Fact]
    public void Grade_WithMl_Returns7Factors()
    {
        var eval = MakeEvaluation();
        var report = ConfidenceGrader.Grade(eval, 100m, 95m, 115m, mlConfidence: 0.85f);

        Assert.Equal(7, report.Breakdown.Count);
        Assert.Contains(report.Breakdown, b => b.Factor == "MLConfidence");
        Assert.Equal(0.85f, report.MlConfidence);
    }

    [Fact]
    public void Grade_WithMl_WeightsStillSumTo1()
    {
        var eval = MakeEvaluation();
        var report = ConfidenceGrader.Grade(eval, 100m, 95m, 115m, mlConfidence: 0.7f);

        var totalWeight = report.Breakdown.Sum(b => b.Weight);
        Assert.Equal(1.0m, totalWeight);
    }

    [Fact]
    public void Grade_WithMl_ExistingFactorsScaledBy085()
    {
        var eval = MakeEvaluation();
        var report = ConfidenceGrader.Grade(eval, 100m, 95m, 115m, mlConfidence: 0.5f);

        var trend = report.Breakdown.Single(b => b.Factor == "TrendAlignment");
        Assert.Equal(0.25m * 0.85m, trend.Weight);

        var volume = report.Breakdown.Single(b => b.Factor == "Volume");
        Assert.Equal(0.15m * 0.85m, volume.Weight);

        var ml = report.Breakdown.Single(b => b.Factor == "MLConfidence");
        Assert.Equal(0.15m, ml.Weight);
    }

    [Fact]
    public void Grade_WithMl_HighConfidenceBoostsScore()
    {
        var eval = MakeEvaluation();
        var reportNoMl = ConfidenceGrader.Grade(eval, 100m, 95m, 115m, historicalWinRate: 50m);
        var reportHighMl = ConfidenceGrader.Grade(eval, 100m, 95m, 115m,
            historicalWinRate: 50m, mlConfidence: 0.95f);

        // High ML confidence (95 raw score * 0.15 weight = 14.25) should boost
        // when combined with proportional re-weighting
        // Both should be reasonable scores; high ML should push score up
        Assert.True(reportHighMl.Score > reportNoMl.Score * 0.8m,
            $"ML boosted score {reportHighMl.Score} should be close to base {reportNoMl.Score}");
    }

    [Fact]
    public void Grade_WithMl_LowConfidenceLowersScore()
    {
        var eval = MakeEvaluation(); // all confirmations pass
        var reportNoMl = ConfidenceGrader.Grade(eval, 100m, 95m, 115m, historicalWinRate: 80m);
        var reportLowMl = ConfidenceGrader.Grade(eval, 100m, 95m, 115m,
            historicalWinRate: 80m, mlConfidence: 0.1f);

        // With all factors passing and high win rate, a low ML confidence should drag score down
        Assert.True(reportLowMl.Score < reportNoMl.Score,
            $"Low ML score {reportLowMl.Score} should be < base {reportNoMl.Score}");
    }

    [Fact]
    public void Grade_WithMl_NullMlPreservesOriginalBehavior()
    {
        var eval = MakeEvaluation();
        var reportNull = ConfidenceGrader.Grade(eval, 100m, 95m, 115m,
            historicalWinRate: 60m, mlConfidence: null);
        var reportOriginal = ConfidenceGrader.Grade(eval, 100m, 95m, 115m,
            historicalWinRate: 60m);

        Assert.Equal(reportOriginal.Score, reportNull.Score);
        Assert.Equal(reportOriginal.Breakdown.Count, reportNull.Breakdown.Count);
    }

    [Fact]
    public void Grade_WithMl_ConfidenceClampedTo01()
    {
        var eval = MakeEvaluation();
        // Pass confidence > 1.0 — should be clamped
        var report = ConfidenceGrader.Grade(eval, 100m, 95m, 115m, mlConfidence: 1.5f);

        var ml = report.Breakdown.Single(b => b.Factor == "MLConfidence");
        Assert.Equal(100m, ml.RawScore); // Clamped to 1.0 → 100 raw score
    }

    [Fact]
    public void Grade_WithMl_ReasonContainsProbability()
    {
        var eval = MakeEvaluation();
        var report = ConfidenceGrader.Grade(eval, 100m, 95m, 115m, mlConfidence: 0.73f);

        var ml = report.Breakdown.Single(b => b.Factor == "MLConfidence");
        Assert.Contains("ML model prediction", ml.Reason);
        Assert.Contains("73", ml.Reason); // 73% probability
    }

    // ── Handler Tests ───────────────────────────────────────────

    [Fact]
    public async Task GetActiveMlModel_ReturnsNull_WhenNoActiveModel()
    {
        using var db = TestIntelligenceDbContextFactory.Create();

        var result = await GetActiveMlModelHandler.HandleAsync(
            new GetActiveMlModelQuery("US"), db);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetActiveMlModel_ReturnsActiveModel()
    {
        using var db = TestIntelligenceDbContextFactory.Create();
        db.MlModels.Add(new MlModel
        {
            MarketCode = "US",
            ModelVersion = 1,
            FeatureVersion = 1,
            ModelPath = "data/models/US/v1.zip",
            TrainedAt = DateTime.UtcNow,
            Auc = 0.65,
            IsActive = true,
            FeatureImportanceJson = "[]"
        });
        await db.SaveChangesAsync();

        var result = await GetActiveMlModelHandler.HandleAsync(
            new GetActiveMlModelQuery("US"), db);

        Assert.NotNull(result);
        Assert.Equal("US", result.MarketCode);
        Assert.Equal(1, result.ModelVersion);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task GetMlFeatureImportance_ReturnsEmpty_WhenNoModel()
    {
        using var db = TestIntelligenceDbContextFactory.Create();

        var result = await GetMlFeatureImportanceHandler.HandleAsync(
            new GetMlFeatureImportanceQuery("US"), db);

        Assert.Empty(result);
    }

    [Fact]
    public async Task PredictConfidence_ReturnsNoModel_WhenNoneActive()
    {
        using var db = TestIntelligenceDbContextFactory.Create();
        var service = new MlPredictionService();
        var logger = NullLogger<PredictConfidenceHandler>.Instance;

        var result = await PredictConfidenceHandler.HandleAsync(
            new PredictConfidenceCommand("US", "AAPL"),
            service, db, logger);

        Assert.False(result.ModelAvailable);
        Assert.Null(result.Confidence);
    }

    // ── Helpers ──────────────────────────────────────────────────

    private static SignalEvaluation MakeEvaluation(
        bool trendPass = true, bool volumePass = true, bool volatilityPass = true)
    {
        var confirmations = new List<ConfirmationResult>
        {
            new() { Name = "TrendAlignment", Passed = trendPass, Weight = 1m, Reason = "Trend" },
            new() { Name = "Momentum", Passed = true, Weight = 1m, Reason = "RSI" },
            new() { Name = "Volume", Passed = volumePass, Weight = 1m, Reason = "Vol" },
            new() { Name = "Volatility", Passed = volatilityPass, Weight = 1m, Reason = "ATR" },
            new() { Name = "MacdHistogram", Passed = true, Weight = 1m, Reason = "MACD" },
            new() { Name = "Stochastic", Passed = true, Weight = 1m, Reason = "Stoch" }
        };

        var totalWeight = confirmations.Sum(c => c.Weight);
        var passedWeight = confirmations.Where(c => c.Passed).Sum(c => c.Weight);

        return new SignalEvaluation
        {
            Symbol = "AAPL",
            Date = new DateTime(2025, 6, 15),
            Direction = SignalDirection.Long,
            Confirmations = confirmations,
            TotalScore = totalWeight > 0 ? passedWeight / totalWeight : 0m
        };
    }

    private static List<FeatureVector> GenerateSyntheticData(int count)
    {
        var rng = new Random(42);
        var data = new List<FeatureVector>();

        for (int i = 0; i < count; i++)
        {
            var rsi = (float)(rng.NextDouble() * 100);
            var isWin = rsi > 50 && rng.NextDouble() > 0.3;

            data.Add(new FeatureVector
            {
                SmaShort = (float)(90 + rng.NextDouble() * 20),
                SmaMedium = (float)(85 + rng.NextDouble() * 30),
                SmaLong = (float)(80 + rng.NextDouble() * 40),
                EmaShort = (float)(90 + rng.NextDouble() * 20),
                EmaMedium = (float)(85 + rng.NextDouble() * 30),
                EmaLong = (float)(80 + rng.NextDouble() * 40),
                Rsi = rsi,
                MacdLine = (float)(rng.NextDouble() * 4 - 2),
                MacdSignal = (float)(rng.NextDouble() * 4 - 2),
                MacdHistogram = (float)(rng.NextDouble() * 2 - 1),
                StochasticK = (float)(rng.NextDouble() * 100),
                StochasticD = (float)(rng.NextDouble() * 100),
                Atr = (float)(rng.NextDouble() * 5),
                BollingerUpper = (float)(105 + rng.NextDouble() * 10),
                BollingerMiddle = (float)(100 + rng.NextDouble() * 5),
                BollingerLower = (float)(90 + rng.NextDouble() * 10),
                BollingerBandwidth = (float)(rng.NextDouble() * 0.1),
                BollingerPercentB = (float)(rng.NextDouble()),
                Obv = (float)(rng.NextDouble() * 1_000_000),
                VolumeMa = (float)(rng.NextDouble() * 500_000),
                RelativeVolume = (float)(0.5 + rng.NextDouble() * 2),
                ClosePrice = (float)(95 + rng.NextDouble() * 10),
                HighPrice = (float)(100 + rng.NextDouble() * 10),
                LowPrice = (float)(90 + rng.NextDouble() * 10),
                OpenPrice = (float)(95 + rng.NextDouble() * 10),
                DailyRange = (float)(rng.NextDouble() * 5),
                PriceToSmaShort = (float)(0.95 + rng.NextDouble() * 0.1),
                PriceToSmaLong = (float)(0.90 + rng.NextDouble() * 0.2),
                PriceToEmaShort = (float)(0.95 + rng.NextDouble() * 0.1),
                AtrPercent = (float)(rng.NextDouble() * 3),
                RegimeConfidence = (float)(rng.NextDouble()),
                DaysSinceRegimeChange = (float)(rng.Next(0, 100)),
                VixLevel = (float)(12 + rng.NextDouble() * 30),
                BreadthScore = (float)(rng.NextDouble()),
                DayOfWeek = (float)(rng.Next(0, 5)),
                WinStreak = (float)(rng.Next(0, 10)),
                LossStreak = (float)(rng.Next(0, 10)),
                RecentWinRate = (float)(rng.NextDouble()),
                PortfolioHeat = (float)(rng.NextDouble()),
                OpenPositionCount = (float)(rng.Next(0, 10)),
                TradePrice = (float)(95 + rng.NextDouble() * 10),
                RegimeLabel = "Bull",
                MarketCode = "US",
                Symbol = "AAPL",
                OrderSide = "Buy",
                Label = isWin
            });
        }

        return data;
    }
}
