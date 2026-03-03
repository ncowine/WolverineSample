using TradingAssistant.Application.Handlers.Intelligence;
using TradingAssistant.Application.Indicators;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.Infrastructure.ML;
using TradingAssistant.Tests.Helpers;

namespace TradingAssistant.Tests.Intelligence;

public class FeatureVectorTests
{
    // ── FeatureVector.FromDictionary ───────────────────────────────

    [Fact]
    public void FromDictionary_ConvertsAllNumericFeaturesToFloat()
    {
        var indicators = CreateSampleIndicators();
        var context = CreateSampleContext();
        var features = FeatureExtractor.ExtractFeatures(indicators, context);

        var vector = FeatureVector.FromDictionary(features, label: true);

        Assert.Equal((float)indicators.Rsi, vector.Rsi);
        Assert.Equal((float)indicators.Atr, vector.Atr);
        Assert.Equal((float)indicators.SmaShort, vector.SmaShort);
        Assert.Equal((float)context.ClosePrice, vector.ClosePrice);
        Assert.Equal((float)context.VixLevel, vector.VixLevel);
        Assert.True(vector.Label);
    }

    [Fact]
    public void FromDictionary_ConvertsStringFeatures()
    {
        var indicators = CreateSampleIndicators();
        var context = CreateSampleContext();
        var features = FeatureExtractor.ExtractFeatures(indicators, context);

        var vector = FeatureVector.FromDictionary(features);

        Assert.Equal("Bullish", vector.RegimeLabel);
        Assert.Equal("US_SP500", vector.MarketCode);
        Assert.Equal("AAPL", vector.Symbol);
        Assert.Equal("Buy", vector.OrderSide);
    }

    [Fact]
    public void FromDictionary_MissingKeysDefaultToZero()
    {
        var features = new Dictionary<string, object>
        {
            ["rsi"] = 55m,
            ["symbol"] = "TSLA"
        };

        var vector = FeatureVector.FromDictionary(features);

        Assert.Equal(55f, vector.Rsi);
        Assert.Equal("TSLA", vector.Symbol);
        // Missing keys → default
        Assert.Equal(0f, vector.SmaShort);
        Assert.Equal(0f, vector.Atr);
        Assert.Equal(string.Empty, vector.RegimeLabel);
    }

    [Fact]
    public void FromDictionary_SetsLabelCorrectly()
    {
        var features = new Dictionary<string, object>();

        var win = FeatureVector.FromDictionary(features, label: true);
        var loss = FeatureVector.FromDictionary(features, label: false);

        Assert.True(win.Label);
        Assert.False(loss.Label);
    }

    // ── FeatureVector.GetFeatureColumnNames ────────────────────────

    [Fact]
    public void GetFeatureColumnNames_Returns45Names()
    {
        var names = FeatureVector.GetFeatureColumnNames();

        Assert.Equal(FeatureVector.FeatureCount, names.Length);
    }

    [Fact]
    public void GetFeatureColumnNames_DoesNotIncludeLabel()
    {
        var names = FeatureVector.GetFeatureColumnNames();

        Assert.DoesNotContain("label", names);
    }

    // ── FeatureVector.ValidateSchema ──────────────────────────────

    [Fact]
    public void ValidateSchema_AllKeysPresent_ReturnsEmpty()
    {
        var indicators = CreateSampleIndicators();
        var context = CreateSampleContext();
        var features = FeatureExtractor.ExtractFeatures(indicators, context);

        var missing = FeatureVector.ValidateSchema(features.Keys);

        Assert.Empty(missing);
    }

    [Fact]
    public void ValidateSchema_MissingKeys_ReturnsThem()
    {
        var keys = new[] { "rsi", "atr", "symbol" };

        var missing = FeatureVector.ValidateSchema(keys);

        Assert.True(missing.Count > 0);
        Assert.Contains("smaShort", missing);
        Assert.Contains("closePrice", missing);
    }

    // ── FeatureVectorConverter.Extract ─────────────────────────────

    [Fact]
    public void Extract_ProducesTypedVector()
    {
        var indicators = CreateSampleIndicators();
        var context = CreateSampleContext();

        var vector = FeatureVectorConverter.Extract(indicators, context, label: true);

        Assert.Equal((float)indicators.Rsi, vector.Rsi);
        Assert.Equal((float)indicators.MacdLine, vector.MacdLine);
        Assert.Equal((float)context.TradePrice, vector.TradePrice);
        Assert.True(vector.Label);
    }

    [Fact]
    public void Extract_NullIndicators_DefaultsToZero()
    {
        var context = CreateSampleContext();

        var vector = FeatureVectorConverter.Extract(null, context);

        Assert.Equal(0f, vector.Rsi);
        Assert.Equal(0f, vector.SmaShort);
        Assert.Equal(0f, vector.Atr);
        // Price context still populated
        Assert.Equal((float)context.ClosePrice, vector.ClosePrice);
    }

    [Fact]
    public void Extract_DerivedFeaturesComputed()
    {
        var indicators = CreateSampleIndicators();
        var context = CreateSampleContext();

        var vector = FeatureVectorConverter.Extract(indicators, context);

        // priceToSmaShort = closePrice / smaShort = 150 / 148 ≈ 1.0135
        Assert.True(vector.PriceToSmaShort > 1.0f);
        // dailyRange = high - low = 155 - 145 = 10
        Assert.Equal(10f, vector.DailyRange);
        // atrPercent = atr / closePrice * 100 = 3.5 / 150 * 100 ≈ 2.33
        Assert.True(vector.AtrPercent > 2f);
    }

    // ── FeatureVectorConverter.FromSnapshot ────────────────────────

    [Fact]
    public void FromSnapshot_DecompressesAndConverts()
    {
        var indicators = CreateSampleIndicators();
        var context = CreateSampleContext();
        var features = FeatureExtractor.ExtractFeatures(indicators, context);
        var compressed = FeatureExtractor.CompressFeatures(features);

        var snapshot = new FeatureSnapshot
        {
            TradeId = Guid.NewGuid(),
            Symbol = "AAPL",
            MarketCode = "US_SP500",
            CapturedAt = DateTime.UtcNow,
            FeatureVersion = 1,
            FeatureCount = features.Count,
            FeaturesJson = compressed,
            FeaturesHash = FeatureExtractor.ComputeHash(features),
            TradeOutcome = TradeOutcome.Win
        };

        var vector = FeatureVectorConverter.FromSnapshot(snapshot);

        Assert.NotNull(vector);
        Assert.True(vector!.Label); // Win → true
        Assert.Equal("AAPL", vector.Symbol);
        Assert.True(vector.Rsi > 0);
    }

    [Fact]
    public void FromSnapshot_LossOutcome_LabelFalse()
    {
        var features = FeatureExtractor.ExtractFeatures(
            CreateSampleIndicators(), CreateSampleContext());
        var compressed = FeatureExtractor.CompressFeatures(features);

        var snapshot = new FeatureSnapshot
        {
            TradeId = Guid.NewGuid(),
            Symbol = "GOOG",
            MarketCode = "US_SP500",
            CapturedAt = DateTime.UtcNow,
            FeatureVersion = 1,
            FeatureCount = features.Count,
            FeaturesJson = compressed,
            FeaturesHash = "hash",
            TradeOutcome = TradeOutcome.Loss
        };

        var vector = FeatureVectorConverter.FromSnapshot(snapshot);

        Assert.NotNull(vector);
        Assert.False(vector!.Label); // Loss → false
    }

    // ── FeatureVectorConverter.BatchConvert ────────────────────────

    [Fact]
    public void BatchConvert_ConvertsLabeledSnapshots()
    {
        var features = FeatureExtractor.ExtractFeatures(
            CreateSampleIndicators(), CreateSampleContext());
        var compressed = FeatureExtractor.CompressFeatures(features);

        var snapshots = new List<FeatureSnapshot>
        {
            MakeSnapshot("AAPL", compressed, TradeOutcome.Win),
            MakeSnapshot("GOOG", compressed, TradeOutcome.Loss),
            MakeSnapshot("MSFT", compressed, TradeOutcome.Win),
        };

        var vectors = FeatureVectorConverter.BatchConvert(snapshots);

        Assert.Equal(3, vectors.Count);
        Assert.Equal(2, vectors.Count(v => v.Label));  // 2 wins
        Assert.Equal(1, vectors.Count(v => !v.Label)); // 1 loss
    }

    [Fact]
    public void BatchConvert_SkipsPendingByDefault()
    {
        var features = FeatureExtractor.ExtractFeatures(
            CreateSampleIndicators(), CreateSampleContext());
        var compressed = FeatureExtractor.CompressFeatures(features);

        var snapshots = new List<FeatureSnapshot>
        {
            MakeSnapshot("AAPL", compressed, TradeOutcome.Win),
            MakeSnapshot("GOOG", compressed, TradeOutcome.Pending),
            MakeSnapshot("MSFT", compressed, TradeOutcome.Loss),
        };

        var vectors = FeatureVectorConverter.BatchConvert(snapshots);

        Assert.Equal(2, vectors.Count); // Pending skipped
    }

    [Fact]
    public void BatchConvert_IncludeUnlabeled_IncludesPending()
    {
        var features = FeatureExtractor.ExtractFeatures(
            CreateSampleIndicators(), CreateSampleContext());
        var compressed = FeatureExtractor.CompressFeatures(features);

        var snapshots = new List<FeatureSnapshot>
        {
            MakeSnapshot("AAPL", compressed, TradeOutcome.Win),
            MakeSnapshot("GOOG", compressed, TradeOutcome.Pending),
        };

        var vectors = FeatureVectorConverter.BatchConvert(snapshots, includeUnlabeled: true);

        Assert.Equal(2, vectors.Count);
    }

    [Fact]
    public void BatchConvert_EmptyInput_ReturnsEmpty()
    {
        var vectors = FeatureVectorConverter.BatchConvert([]);

        Assert.Empty(vectors);
    }

    // ── Feature Name Matching ─────────────────────────────────────

    [Fact]
    public void FeatureNames_MatchBetweenExtractorAndVector()
    {
        var indicators = CreateSampleIndicators();
        var context = CreateSampleContext();
        var dictFeatures = FeatureExtractor.ExtractFeatures(indicators, context);
        var vectorNames = FeatureVector.GetFeatureColumnNames();

        // Every vector column name should exist as a dictionary key
        foreach (var name in vectorNames)
        {
            Assert.True(dictFeatures.ContainsKey(name),
                $"FeatureVector column '{name}' not found in ExtractFeatures dictionary");
        }

        // Every dictionary key should exist as a vector column name
        var vectorNameSet = new HashSet<string>(vectorNames);
        foreach (var key in dictFeatures.Keys)
        {
            Assert.True(vectorNameSet.Contains(key),
                $"ExtractFeatures key '{key}' not found in FeatureVector columns");
        }
    }

    [Fact]
    public void FeatureCount_ConsistentAcrossAllApis()
    {
        var indicators = CreateSampleIndicators();
        var context = CreateSampleContext();
        var dictFeatures = FeatureExtractor.ExtractFeatures(indicators, context);
        var vectorNames = FeatureVector.GetFeatureColumnNames();

        Assert.Equal(FeatureVector.FeatureCount, dictFeatures.Count);
        Assert.Equal(FeatureVector.FeatureCount, vectorNames.Length);
    }

    // ── GetTrainingDataHandler ─────────────────────────────────────

    [Fact]
    public async Task GetTrainingData_ReturnsMetadata()
    {
        var dbName = Guid.NewGuid().ToString();
        using var intelDb = TestIntelligenceDbContextFactory.Create(dbName);

        var features = FeatureExtractor.ExtractFeatures(
            CreateSampleIndicators(), CreateSampleContext());
        var compressed = FeatureExtractor.CompressFeatures(features);

        intelDb.FeatureSnapshots.Add(MakeSnapshot("AAPL", compressed, TradeOutcome.Win));
        intelDb.FeatureSnapshots.Add(MakeSnapshot("GOOG", compressed, TradeOutcome.Loss));
        intelDb.FeatureSnapshots.Add(MakeSnapshot("MSFT", compressed, TradeOutcome.Pending));
        await intelDb.SaveChangesAsync();

        var result = await GetTrainingDataHandler.HandleAsync(
            new Contracts.Queries.GetTrainingDataQuery(), intelDb);

        Assert.Equal(2, result.TotalSnapshots); // Pending excluded by query
        Assert.Equal(2, result.ConvertedVectors);
        Assert.Equal(1, result.WinCount);
        Assert.Equal(1, result.LossCount);
        Assert.Equal(FeatureVector.FeatureCount, result.FeatureColumns.Length);
    }

    [Fact]
    public async Task GetTrainingData_FiltersBySymbol()
    {
        var dbName = Guid.NewGuid().ToString();
        using var intelDb = TestIntelligenceDbContextFactory.Create(dbName);

        var features = FeatureExtractor.ExtractFeatures(
            CreateSampleIndicators(), CreateSampleContext());
        var compressed = FeatureExtractor.CompressFeatures(features);

        intelDb.FeatureSnapshots.Add(MakeSnapshot("AAPL", compressed, TradeOutcome.Win));
        intelDb.FeatureSnapshots.Add(MakeSnapshot("GOOG", compressed, TradeOutcome.Loss));
        await intelDb.SaveChangesAsync();

        var result = await GetTrainingDataHandler.HandleAsync(
            new Contracts.Queries.GetTrainingDataQuery(Symbol: "AAPL"), intelDb);

        Assert.Equal(1, result.TotalSnapshots);
        Assert.Equal(1, result.WinCount);
    }

    // ── Helpers ────────────────────────────────────────────────────

    private static IndicatorValues CreateSampleIndicators() => new()
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

    private static FeatureSnapshot MakeSnapshot(
        string symbol, string compressed, TradeOutcome outcome) => new()
    {
        TradeId = Guid.NewGuid(),
        Symbol = symbol,
        MarketCode = "US_SP500",
        CapturedAt = DateTime.UtcNow,
        FeatureVersion = 1,
        FeatureCount = 45,
        FeaturesJson = compressed,
        FeaturesHash = "test_hash",
        TradeOutcome = outcome
    };
}
