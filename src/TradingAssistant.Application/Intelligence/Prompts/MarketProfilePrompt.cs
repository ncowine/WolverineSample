using System.Text.Json;
using System.Text.Json.Serialization;

namespace TradingAssistant.Application.Intelligence.Prompts;

public record MarketProfileInput(
    string MarketCode,
    string Exchange,
    int HistoricalYears,
    decimal AvgDailyVolume,
    decimal AnnualizedVolatility,
    string TypicalRegimes,
    string CorrelationPartners);

public record MarketProfileOutput
{
    [JsonPropertyName("behavioralProfile")]
    public string BehavioralProfile { get; init; } = string.Empty;

    [JsonPropertyName("typicalPatterns")]
    public IReadOnlyList<string> TypicalPatterns { get; init; } = [];

    [JsonPropertyName("regimeTendencies")]
    public IReadOnlyList<RegimeTendency> RegimeTendencies { get; init; } = [];

    [JsonPropertyName("riskCharacteristics")]
    public RiskCharacteristics RiskCharacteristics { get; init; } = new();

    [JsonPropertyName("tradingRecommendations")]
    public IReadOnlyList<string> TradingRecommendations { get; init; } = [];

    [JsonPropertyName("summary")]
    public string Summary { get; init; } = string.Empty;
}

public record RegimeTendency
{
    [JsonPropertyName("regime")]
    public string Regime { get; init; } = string.Empty;

    [JsonPropertyName("frequency")]
    public string Frequency { get; init; } = string.Empty;

    [JsonPropertyName("avgDuration")]
    public string AvgDuration { get; init; } = string.Empty;

    [JsonPropertyName("bestStrategies")]
    public IReadOnlyList<string> BestStrategies { get; init; } = [];
}

public record RiskCharacteristics
{
    [JsonPropertyName("maxHistoricalDrawdown")]
    public string MaxHistoricalDrawdown { get; init; } = string.Empty;

    [JsonPropertyName("tailRiskProfile")]
    public string TailRiskProfile { get; init; } = string.Empty;

    [JsonPropertyName("liquidityProfile")]
    public string LiquidityProfile { get; init; } = string.Empty;

    [JsonPropertyName("gapRisk")]
    public string GapRisk { get; init; } = string.Empty;
}

/// <summary>
/// Prompt template for Claude to generate a market DNA behavioral profile.
/// </summary>
public static class MarketProfilePrompt
{
    public static string BuildSystemPrompt() =>
        """
        You are an expert market analyst specializing in market microstructure and behavioral profiles.
        Generate comprehensive market DNA profiles that capture a market's personality,
        typical patterns, and risk characteristics. Be data-driven and specific.

        IMPORTANT: Respond ONLY with valid JSON matching the requested schema.
        Do not include any text outside the JSON object.
        """;

    public static string BuildUserPrompt(MarketProfileInput input)
    {
        return $"""
            Generate a DNA behavioral profile for market {input.MarketCode}:

            Exchange: {input.Exchange}
            Historical data: {input.HistoricalYears} years
            Avg daily volume: {input.AvgDailyVolume:F0}
            Annualized volatility: {input.AnnualizedVolatility:F1}%
            Typical regimes observed: {input.TypicalRegimes}
            Correlation partners: {input.CorrelationPartners}

            Respond with JSON matching this schema:
            behavioralProfile (string),
            typicalPatterns (array of strings),
            regimeTendencies (array of objects with: regime (string), frequency (string),
              avgDuration (string), bestStrategies (array of strings)),
            riskCharacteristics (object with: maxHistoricalDrawdown (string),
              tailRiskProfile (string), liquidityProfile (string), gapRisk (string)),
            tradingRecommendations (array of strings),
            summary (string).
            """;
    }

    public static MarketProfileOutput? ParseResponse(string json)
    {
        var trimmed = ExtractJson(json);
        return JsonSerializer.Deserialize<MarketProfileOutput>(trimmed, JsonOptions.Default);
    }

    private static string ExtractJson(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start)
            return text[start..(end + 1)];
        return text;
    }
}
