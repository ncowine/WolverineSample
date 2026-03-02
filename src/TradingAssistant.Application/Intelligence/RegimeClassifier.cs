using System.Text.Json;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Domain.Intelligence.Enums;

namespace TradingAssistant.Application.Intelligence;

/// <summary>
/// Input indicators used for regime classification.
/// </summary>
public record RegimeInputs(
    decimal SmaSlope50,
    decimal SmaSlope200,
    decimal VixLevel,
    decimal PctAbove200Sma,
    decimal PctAbove50Sma,
    decimal AdvanceDeclineRatio);

/// <summary>
/// Per-market thresholds for regime classification, parsed from MarketProfile.ConfigJson.
/// </summary>
public record RegimeThresholds(
    decimal HighVolThreshold,
    decimal BullBreadthThreshold,
    decimal BearBreadthThreshold)
{
    /// <summary>US market defaults: highVol=30, bull=0.60, bear=0.40.</summary>
    public static readonly RegimeThresholds UsDefault = new(30m, 0.60m, 0.40m);

    /// <summary>India market defaults: highVol=25, bull=0.55, bear=0.35.</summary>
    public static readonly RegimeThresholds IndiaDefault = new(25m, 0.55m, 0.35m);

    /// <summary>
    /// Parse thresholds from a MarketProfile's ConfigJson.
    /// Falls back to US defaults if parsing fails.
    /// </summary>
    public static RegimeThresholds FromConfigJson(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
            return UsDefault;

        try
        {
            using var doc = JsonDocument.Parse(configJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("regimeThresholds", out var thresholds))
                return UsDefault;

            var highVol = thresholds.TryGetProperty("highVol", out var hv) ? hv.GetDecimal() : UsDefault.HighVolThreshold;
            var bullBreadth = thresholds.TryGetProperty("bullBreadth", out var bb) ? bb.GetDecimal() : UsDefault.BullBreadthThreshold;
            var bearBreadth = thresholds.TryGetProperty("bearBreadth", out var br) ? br.GetDecimal() : UsDefault.BearBreadthThreshold;

            // Normalize: if thresholds are expressed as percentages (>1), convert to decimals
            if (bullBreadth > 1m) bullBreadth /= 100m;
            if (bearBreadth > 1m) bearBreadth /= 100m;

            return new RegimeThresholds(highVol, bullBreadth, bearBreadth);
        }
        catch (JsonException)
        {
            return UsDefault;
        }
    }
}

/// <summary>
/// Pure function that classifies a market into a regime based on indicator inputs and thresholds.
/// Backtestable — no side effects, no database access.
/// </summary>
public static class RegimeClassifier
{
    /// <summary>
    /// Classify a market regime and compute a confidence score.
    /// </summary>
    public static (RegimeType Regime, decimal Confidence) Classify(
        RegimeInputs inputs,
        RegimeThresholds thresholds)
    {
        // Rule 1: HighVolatility — VIX above threshold dominates
        if (inputs.VixLevel > thresholds.HighVolThreshold)
        {
            var excess = (inputs.VixLevel - thresholds.HighVolThreshold) / thresholds.HighVolThreshold;
            var confidence = Math.Min(1.0m, 0.60m + 0.40m * excess);
            return (RegimeType.HighVolatility, Math.Round(confidence, 4));
        }

        // Rule 2: Bull — both SMA slopes positive AND breadth above bull threshold
        if (inputs.SmaSlope50 > 0 && inputs.SmaSlope200 > 0 && inputs.PctAbove200Sma > thresholds.BullBreadthThreshold)
        {
            var slopeFactor = Math.Min(1.0m, (inputs.SmaSlope50 + inputs.SmaSlope200) * 200m);
            var breadthExcess = (inputs.PctAbove200Sma - thresholds.BullBreadthThreshold) / (1m - thresholds.BullBreadthThreshold);
            var confidence = Math.Min(1.0m, 0.50m + 0.25m * slopeFactor + 0.25m * Math.Min(1.0m, breadthExcess));
            return (RegimeType.Bull, Math.Round(confidence, 4));
        }

        // Rule 3: Bear — both SMA slopes negative AND breadth below bear threshold
        if (inputs.SmaSlope50 < 0 && inputs.SmaSlope200 < 0 && inputs.PctAbove200Sma < thresholds.BearBreadthThreshold)
        {
            var slopeFactor = Math.Min(1.0m, (-inputs.SmaSlope50 + -inputs.SmaSlope200) * 200m);
            var breadthDeficit = (thresholds.BearBreadthThreshold - inputs.PctAbove200Sma) / thresholds.BearBreadthThreshold;
            var confidence = Math.Min(1.0m, 0.50m + 0.25m * slopeFactor + 0.25m * Math.Min(1.0m, breadthDeficit));
            return (RegimeType.Bear, Math.Round(confidence, 4));
        }

        // Rule 4: Sideways — default when no strong directional signal
        // Confidence inversely related to how close we are to a directional regime
        var nearBull = inputs.SmaSlope50 > 0 && inputs.SmaSlope200 > 0 ? 0.3m : 0m;
        var nearBear = inputs.SmaSlope50 < 0 && inputs.SmaSlope200 < 0 ? 0.3m : 0m;
        var sidewaysConfidence = Math.Max(0.40m, 0.80m - nearBull - nearBear);
        return (RegimeType.Sideways, Math.Round(sidewaysConfidence, 4));
    }

    /// <summary>
    /// Compute SMA slope as the rate of change over a lookback period.
    /// Slope = (SMA_current - SMA_lookback_ago) / lookback.
    /// </summary>
    /// <param name="smaValues">Full SMA array (from SmaCalculator).</param>
    /// <param name="lookback">Number of bars to measure slope over (default 20).</param>
    /// <returns>The slope value, or 0 if insufficient data.</returns>
    public static decimal ComputeSmaSlope(decimal[] smaValues, int lookback = 20)
    {
        if (smaValues.Length < lookback + 1)
            return 0m;

        var lastIndex = smaValues.Length - 1;
        var current = smaValues[lastIndex];
        var previous = smaValues[lastIndex - lookback];

        if (current == 0 || previous == 0)
            return 0m;

        return (current - previous) / lookback;
    }

    /// <summary>
    /// Build a BreadthScore (0-100) that aggregates multiple breadth indicators.
    /// Used as a summary metric stored in MarketRegime.BreadthScore.
    /// </summary>
    public static decimal ComputeBreadthScore(BreadthSnapshot breadth)
    {
        // Weight: PctAbove200Sma (40%), PctAbove50Sma (30%), A/D ratio (30%)
        var pct200Component = breadth.PctAbove200Sma * 100m * 0.40m;
        var pct50Component = breadth.PctAbove50Sma * 100m * 0.30m;

        // Normalize A/D ratio: 1.0 = neutral (50), >2.0 caps at 100, <0.5 caps at 0
        var adNormalized = Math.Min(100m, Math.Max(0m, (breadth.AdvanceDeclineRatio - 0.5m) / 1.5m * 100m));
        var adComponent = adNormalized * 0.30m;

        return Math.Round(pct200Component + pct50Component + adComponent, 2);
    }
}
