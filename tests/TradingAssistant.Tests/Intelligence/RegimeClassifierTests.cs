using TradingAssistant.Application.Intelligence;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Domain.Intelligence.Enums;

namespace TradingAssistant.Tests.Intelligence;

public class RegimeClassifierTests
{
    private static readonly RegimeThresholds UsThresholds = RegimeThresholds.UsDefault;
    private static readonly RegimeThresholds IndiaThresholds = RegimeThresholds.IndiaDefault;

    // ── HighVolatility Classification ──

    [Fact]
    public void Classify_VixAboveThreshold_ReturnsHighVolatility()
    {
        var inputs = new RegimeInputs(
            SmaSlope50: 0.001m,
            SmaSlope200: 0.0005m,
            VixLevel: 35m, // above US threshold of 30
            PctAbove200Sma: 0.65m,
            PctAbove50Sma: 0.70m,
            AdvanceDeclineRatio: 1.5m);

        var (regime, confidence) = RegimeClassifier.Classify(inputs, UsThresholds);

        Assert.Equal(RegimeType.HighVolatility, regime);
        Assert.True(confidence >= 0.60m);
    }

    [Fact]
    public void Classify_VixWayAboveThreshold_HighConfidence()
    {
        var inputs = new RegimeInputs(
            SmaSlope50: -0.002m,
            SmaSlope200: -0.001m,
            VixLevel: 60m, // 2x threshold → should be near max confidence
            PctAbove200Sma: 0.20m,
            PctAbove50Sma: 0.15m,
            AdvanceDeclineRatio: 0.5m);

        var (regime, confidence) = RegimeClassifier.Classify(inputs, UsThresholds);

        Assert.Equal(RegimeType.HighVolatility, regime);
        Assert.True(confidence >= 0.90m);
    }

    [Fact]
    public void Classify_VixJustAboveThreshold_LowerConfidence()
    {
        var inputs = new RegimeInputs(
            SmaSlope50: 0m,
            SmaSlope200: 0m,
            VixLevel: 31m, // barely above 30
            PctAbove200Sma: 0.50m,
            PctAbove50Sma: 0.50m,
            AdvanceDeclineRatio: 1.0m);

        var (regime, confidence) = RegimeClassifier.Classify(inputs, UsThresholds);

        Assert.Equal(RegimeType.HighVolatility, regime);
        Assert.True(confidence <= 0.70m, $"Expected <=0.70 but got {confidence}");
    }

    [Fact]
    public void Classify_IndiaVixThreshold_UsesLowerValue()
    {
        // VIX = 27 → above India threshold (25) but below US threshold (30)
        var inputs = new RegimeInputs(
            SmaSlope50: 0.001m,
            SmaSlope200: 0.0005m,
            VixLevel: 27m,
            PctAbove200Sma: 0.60m,
            PctAbove50Sma: 0.65m,
            AdvanceDeclineRatio: 1.5m);

        var (regimeIndia, _) = RegimeClassifier.Classify(inputs, IndiaThresholds);
        var (regimeUs, _) = RegimeClassifier.Classify(inputs, UsThresholds);

        Assert.Equal(RegimeType.HighVolatility, regimeIndia);
        Assert.NotEqual(RegimeType.HighVolatility, regimeUs);
    }

    // ── Bull Classification ──

    [Fact]
    public void Classify_PositiveSlopesAndStrongBreadth_ReturnsBull()
    {
        var inputs = new RegimeInputs(
            SmaSlope50: 0.003m,
            SmaSlope200: 0.001m,
            VixLevel: 15m,
            PctAbove200Sma: 0.75m, // above US bull threshold of 0.60
            PctAbove50Sma: 0.80m,
            AdvanceDeclineRatio: 2.0m);

        var (regime, confidence) = RegimeClassifier.Classify(inputs, UsThresholds);

        Assert.Equal(RegimeType.Bull, regime);
        Assert.True(confidence >= 0.50m);
    }

    [Fact]
    public void Classify_StrongBull_HighConfidence()
    {
        var inputs = new RegimeInputs(
            SmaSlope50: 0.005m,
            SmaSlope200: 0.003m,
            VixLevel: 12m,
            PctAbove200Sma: 0.90m,
            PctAbove50Sma: 0.92m,
            AdvanceDeclineRatio: 3.0m);

        var (regime, confidence) = RegimeClassifier.Classify(inputs, UsThresholds);

        Assert.Equal(RegimeType.Bull, regime);
        Assert.True(confidence >= 0.70m, $"Expected strong bull confidence >=0.70 but got {confidence}");
    }

    [Fact]
    public void Classify_BullRequiresBothSlopesPositive()
    {
        // SMA50 positive but SMA200 negative → not bull
        var inputs = new RegimeInputs(
            SmaSlope50: 0.003m,
            SmaSlope200: -0.001m,
            VixLevel: 15m,
            PctAbove200Sma: 0.75m,
            PctAbove50Sma: 0.80m,
            AdvanceDeclineRatio: 2.0m);

        var (regime, _) = RegimeClassifier.Classify(inputs, UsThresholds);

        Assert.NotEqual(RegimeType.Bull, regime);
    }

    [Fact]
    public void Classify_BullRequiresBreadthAboveThreshold()
    {
        // Both slopes positive but breadth below threshold
        var inputs = new RegimeInputs(
            SmaSlope50: 0.003m,
            SmaSlope200: 0.001m,
            VixLevel: 15m,
            PctAbove200Sma: 0.55m, // below US bull threshold of 0.60
            PctAbove50Sma: 0.60m,
            AdvanceDeclineRatio: 1.5m);

        var (regime, _) = RegimeClassifier.Classify(inputs, UsThresholds);

        Assert.NotEqual(RegimeType.Bull, regime);
    }

    // ── Bear Classification ──

    [Fact]
    public void Classify_NegativeSlopesAndWeakBreadth_ReturnsBear()
    {
        var inputs = new RegimeInputs(
            SmaSlope50: -0.003m,
            SmaSlope200: -0.001m,
            VixLevel: 22m,
            PctAbove200Sma: 0.30m, // below US bear threshold of 0.40
            PctAbove50Sma: 0.25m,
            AdvanceDeclineRatio: 0.6m);

        var (regime, confidence) = RegimeClassifier.Classify(inputs, UsThresholds);

        Assert.Equal(RegimeType.Bear, regime);
        Assert.True(confidence >= 0.50m);
    }

    [Fact]
    public void Classify_StrongBear_HighConfidence()
    {
        var inputs = new RegimeInputs(
            SmaSlope50: -0.005m,
            SmaSlope200: -0.003m,
            VixLevel: 28m,
            PctAbove200Sma: 0.10m,
            PctAbove50Sma: 0.08m,
            AdvanceDeclineRatio: 0.3m);

        var (regime, confidence) = RegimeClassifier.Classify(inputs, UsThresholds);

        Assert.Equal(RegimeType.Bear, regime);
        Assert.True(confidence >= 0.70m, $"Expected strong bear confidence >=0.70 but got {confidence}");
    }

    [Fact]
    public void Classify_BearRequiresBothSlopesNegative()
    {
        // SMA50 negative but SMA200 positive → not bear
        var inputs = new RegimeInputs(
            SmaSlope50: -0.003m,
            SmaSlope200: 0.001m,
            VixLevel: 22m,
            PctAbove200Sma: 0.30m,
            PctAbove50Sma: 0.25m,
            AdvanceDeclineRatio: 0.6m);

        var (regime, _) = RegimeClassifier.Classify(inputs, UsThresholds);

        Assert.NotEqual(RegimeType.Bear, regime);
    }

    // ── Sideways Classification ──

    [Fact]
    public void Classify_MixedSignals_ReturnsSideways()
    {
        // SMA slopes disagree
        var inputs = new RegimeInputs(
            SmaSlope50: 0.001m,
            SmaSlope200: -0.0005m,
            VixLevel: 18m,
            PctAbove200Sma: 0.50m,
            PctAbove50Sma: 0.55m,
            AdvanceDeclineRatio: 1.1m);

        var (regime, confidence) = RegimeClassifier.Classify(inputs, UsThresholds);

        Assert.Equal(RegimeType.Sideways, regime);
        Assert.True(confidence >= 0.40m);
    }

    [Fact]
    public void Classify_FlatSlopes_ReturnsSideways()
    {
        var inputs = new RegimeInputs(
            SmaSlope50: 0m,
            SmaSlope200: 0m,
            VixLevel: 16m,
            PctAbove200Sma: 0.50m,
            PctAbove50Sma: 0.50m,
            AdvanceDeclineRatio: 1.0m);

        var (regime, _) = RegimeClassifier.Classify(inputs, UsThresholds);

        Assert.Equal(RegimeType.Sideways, regime);
    }

    [Fact]
    public void Classify_PositiveSlopesButWeakBreadth_ReturnsSideways()
    {
        // Both slopes positive but breadth in the middle ground
        var inputs = new RegimeInputs(
            SmaSlope50: 0.002m,
            SmaSlope200: 0.001m,
            VixLevel: 18m,
            PctAbove200Sma: 0.50m, // between bear (0.40) and bull (0.60)
            PctAbove50Sma: 0.55m,
            AdvanceDeclineRatio: 1.2m);

        var (regime, _) = RegimeClassifier.Classify(inputs, UsThresholds);

        Assert.Equal(RegimeType.Sideways, regime);
    }

    // ── VIX Priority ──

    [Fact]
    public void Classify_HighVixOverridesBullSignals()
    {
        // All bull signals + high VIX → HighVolatility wins
        var inputs = new RegimeInputs(
            SmaSlope50: 0.005m,
            SmaSlope200: 0.003m,
            VixLevel: 35m,
            PctAbove200Sma: 0.80m,
            PctAbove50Sma: 0.85m,
            AdvanceDeclineRatio: 3.0m);

        var (regime, _) = RegimeClassifier.Classify(inputs, UsThresholds);

        Assert.Equal(RegimeType.HighVolatility, regime);
    }

    // ── SMA Slope Computation ──

    [Fact]
    public void ComputeSmaSlope_RisingValues_PositiveSlope()
    {
        // SMA values steadily rising
        var smaValues = Enumerable.Range(0, 30).Select(i => 100m + i * 0.5m).ToArray();

        var slope = RegimeClassifier.ComputeSmaSlope(smaValues);

        Assert.True(slope > 0);
        Assert.Equal(0.5m, slope); // (100+14.5 - 100+4.5) / 20 = 10/20 = 0.5
    }

    [Fact]
    public void ComputeSmaSlope_FallingValues_NegativeSlope()
    {
        var smaValues = Enumerable.Range(0, 30).Select(i => 200m - i * 0.3m).ToArray();

        var slope = RegimeClassifier.ComputeSmaSlope(smaValues);

        Assert.True(slope < 0);
    }

    [Fact]
    public void ComputeSmaSlope_FlatValues_ZeroSlope()
    {
        var smaValues = Enumerable.Repeat(100m, 30).ToArray();

        var slope = RegimeClassifier.ComputeSmaSlope(smaValues);

        Assert.Equal(0m, slope);
    }

    [Fact]
    public void ComputeSmaSlope_InsufficientData_ReturnsZero()
    {
        var smaValues = new[] { 100m, 101m, 102m }; // only 3, need 21

        var slope = RegimeClassifier.ComputeSmaSlope(smaValues);

        Assert.Equal(0m, slope);
    }

    [Fact]
    public void ComputeSmaSlope_ZeroValues_ReturnsZero()
    {
        // Warmup period zeros
        var smaValues = new decimal[30]; // all zeros

        var slope = RegimeClassifier.ComputeSmaSlope(smaValues);

        Assert.Equal(0m, slope);
    }

    // ── Breadth Score Computation ──

    [Fact]
    public void ComputeBreadthScore_StrongBreadth_HighScore()
    {
        var breadth = new BreadthSnapshot
        {
            PctAbove200Sma = 0.80m,
            PctAbove50Sma = 0.85m,
            AdvanceDeclineRatio = 2.5m
        };

        var score = RegimeClassifier.ComputeBreadthScore(breadth);

        Assert.True(score >= 70m, $"Expected >=70 but got {score}");
        Assert.True(score <= 100m);
    }

    [Fact]
    public void ComputeBreadthScore_WeakBreadth_LowScore()
    {
        var breadth = new BreadthSnapshot
        {
            PctAbove200Sma = 0.20m,
            PctAbove50Sma = 0.15m,
            AdvanceDeclineRatio = 0.4m
        };

        var score = RegimeClassifier.ComputeBreadthScore(breadth);

        Assert.True(score <= 30m, $"Expected <=30 but got {score}");
        Assert.True(score >= 0m);
    }

    [Fact]
    public void ComputeBreadthScore_NeutralBreadth_MidScore()
    {
        var breadth = new BreadthSnapshot
        {
            PctAbove200Sma = 0.50m,
            PctAbove50Sma = 0.50m,
            AdvanceDeclineRatio = 1.0m
        };

        var score = RegimeClassifier.ComputeBreadthScore(breadth);

        Assert.True(score >= 30m && score <= 60m, $"Expected 30-60 but got {score}");
    }

    // ── Threshold Parsing ──

    [Fact]
    public void RegimeThresholds_FromConfigJson_ParsesCorrectly()
    {
        var json = """{"regimeThresholds":{"highVol":25,"bullBreadth":0.55,"bearBreadth":0.35}}""";

        var thresholds = RegimeThresholds.FromConfigJson(json);

        Assert.Equal(25m, thresholds.HighVolThreshold);
        Assert.Equal(0.55m, thresholds.BullBreadthThreshold);
        Assert.Equal(0.35m, thresholds.BearBreadthThreshold);
    }

    [Fact]
    public void RegimeThresholds_FromConfigJson_PercentageNormalization()
    {
        // Thresholds expressed as percentages (60 instead of 0.60)
        var json = """{"regimeThresholds":{"highVol":30,"bullBreadth":60,"bearBreadth":40}}""";

        var thresholds = RegimeThresholds.FromConfigJson(json);

        Assert.Equal(30m, thresholds.HighVolThreshold);
        Assert.Equal(0.60m, thresholds.BullBreadthThreshold);
        Assert.Equal(0.40m, thresholds.BearBreadthThreshold);
    }

    [Fact]
    public void RegimeThresholds_FromConfigJson_NullJson_ReturnsUsDefaults()
    {
        var thresholds = RegimeThresholds.FromConfigJson(null);

        Assert.Equal(RegimeThresholds.UsDefault, thresholds);
    }

    [Fact]
    public void RegimeThresholds_FromConfigJson_InvalidJson_ReturnsUsDefaults()
    {
        var thresholds = RegimeThresholds.FromConfigJson("not json");

        Assert.Equal(RegimeThresholds.UsDefault, thresholds);
    }

    [Fact]
    public void RegimeThresholds_FromConfigJson_MissingRegimeThresholds_ReturnsUsDefaults()
    {
        var json = """{"tradingHours":{"open":"09:30","close":"16:00"}}""";

        var thresholds = RegimeThresholds.FromConfigJson(json);

        Assert.Equal(RegimeThresholds.UsDefault, thresholds);
    }

    [Fact]
    public void RegimeThresholds_FromConfigJson_PartialThresholds_FallsBackToDefaults()
    {
        // Only highVol specified → bull/bear use defaults
        var json = """{"regimeThresholds":{"highVol":28}}""";

        var thresholds = RegimeThresholds.FromConfigJson(json);

        Assert.Equal(28m, thresholds.HighVolThreshold);
        Assert.Equal(RegimeThresholds.UsDefault.BullBreadthThreshold, thresholds.BullBreadthThreshold);
        Assert.Equal(RegimeThresholds.UsDefault.BearBreadthThreshold, thresholds.BearBreadthThreshold);
    }

    // ── Confidence Score Bounds ──

    [Fact]
    public void Classify_ConfidenceAlwaysBetweenZeroAndOne()
    {
        var scenarios = new[]
        {
            new RegimeInputs(0.01m, 0.01m, 5m, 0.99m, 0.99m, 5.0m),       // extreme bull
            new RegimeInputs(-0.01m, -0.01m, 5m, 0.01m, 0.01m, 0.1m),     // extreme bear
            new RegimeInputs(0m, 0m, 100m, 0.50m, 0.50m, 1.0m),           // extreme vol
            new RegimeInputs(0m, 0m, 10m, 0.50m, 0.50m, 1.0m),            // dead sideways
        };

        foreach (var inputs in scenarios)
        {
            var (_, confidence) = RegimeClassifier.Classify(inputs, UsThresholds);

            Assert.True(confidence >= 0m, $"Confidence {confidence} < 0 for {inputs}");
            Assert.True(confidence <= 1.0m, $"Confidence {confidence} > 1 for {inputs}");
        }
    }
}
