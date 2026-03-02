using System.Text.Json;
using System.Text.Json.Serialization;

namespace TradingAssistant.Application.Intelligence.Prompts;

public record StrategyGenerationInput(
    string MarketCode,
    string RegimeType,
    IReadOnlyList<string> AvailableIndicators,
    decimal MaxDrawdownPercent,
    decimal TargetSharpe,
    string? AdditionalConstraints = null);

public record StrategyGenerationOutput
{
    [JsonPropertyName("strategyName")]
    public string StrategyName { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("timeframeMinutes")]
    public int TimeframeMinutes { get; init; }

    [JsonPropertyName("entryRules")]
    public IReadOnlyList<string> EntryRules { get; init; } = [];

    [JsonPropertyName("exitRules")]
    public IReadOnlyList<string> ExitRules { get; init; } = [];

    [JsonPropertyName("stopLossPercent")]
    public decimal StopLossPercent { get; init; }

    [JsonPropertyName("takeProfitPercent")]
    public decimal TakeProfitPercent { get; init; }

    [JsonPropertyName("indicatorConfigs")]
    public IReadOnlyList<IndicatorConfig> IndicatorConfigs { get; init; } = [];

    [JsonPropertyName("rationale")]
    public string Rationale { get; init; } = string.Empty;
}

public record IndicatorConfig
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("parameters")]
    public JsonElement Parameters { get; init; }
}

/// <summary>
/// Prompt template for Claude to generate a trading strategy.
/// </summary>
public static class StrategyGenerationPrompt
{
    public static string BuildSystemPrompt() =>
        """
        You are an expert quantitative trading strategist. Generate trading strategies
        as structured JSON. Your strategies must be specific, testable, and include
        concrete indicator parameters.

        IMPORTANT: Respond ONLY with valid JSON matching the requested schema.
        Do not include any text outside the JSON object.
        """;

    public static string BuildUserPrompt(StrategyGenerationInput input)
    {
        var indicators = string.Join(", ", input.AvailableIndicators);
        var constraints = string.IsNullOrWhiteSpace(input.AdditionalConstraints)
            ? "None"
            : input.AdditionalConstraints;

        return $"""
            Generate a trading strategy for market {input.MarketCode} optimized for {input.RegimeType} regime.

            Available indicators: {indicators}
            Max drawdown: {input.MaxDrawdownPercent}%
            Target Sharpe ratio: {input.TargetSharpe}
            Additional constraints: {constraints}

            Respond with JSON matching this schema:
            strategyName (string), description (string), timeframeMinutes (number),
            entryRules (array of strings), exitRules (array of strings),
            stopLossPercent (number), takeProfitPercent (number),
            indicatorConfigs (array of objects with name and parameters),
            rationale (string).
            """;
    }

    public static StrategyGenerationOutput? ParseResponse(string json)
    {
        var trimmed = ExtractJson(json);
        return JsonSerializer.Deserialize<StrategyGenerationOutput>(trimmed, JsonOptions.Default);
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
