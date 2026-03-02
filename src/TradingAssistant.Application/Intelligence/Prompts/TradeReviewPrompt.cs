using System.Text.Json;
using System.Text.Json.Serialization;

namespace TradingAssistant.Application.Intelligence.Prompts;

public record TradeReviewInput(
    string Symbol,
    string Side,
    DateTime EntryDate,
    DateTime ExitDate,
    decimal EntryPrice,
    decimal ExitPrice,
    decimal PnlPercent,
    string StrategyName,
    IReadOnlyDictionary<string, decimal>? IndicatorValuesAtEntry = null);

public record TradeReviewOutput
{
    [JsonPropertyName("classification")]
    public string Classification { get; init; } = string.Empty;

    [JsonPropertyName("score")]
    public int Score { get; init; }

    [JsonPropertyName("strengths")]
    public IReadOnlyList<string> Strengths { get; init; } = [];

    [JsonPropertyName("weaknesses")]
    public IReadOnlyList<string> Weaknesses { get; init; } = [];

    [JsonPropertyName("lessonsLearned")]
    public IReadOnlyList<string> LessonsLearned { get; init; } = [];

    [JsonPropertyName("summary")]
    public string Summary { get; init; } = string.Empty;
}

/// <summary>
/// Prompt template for Claude to review a completed trade.
/// </summary>
public static class TradeReviewPrompt
{
    public static string BuildSystemPrompt() =>
        """
        You are an expert trade analyst reviewing completed trades. Classify each trade
        and provide actionable feedback. Be specific about what went right or wrong.

        Classifications: Good (well-executed, profitable), Bad (poor execution or logic),
        Lucky (profitable despite poor reasoning), Unlucky (good reasoning but adverse outcome).

        Score from 1 (worst) to 10 (best) based on execution quality, not just P&L.

        IMPORTANT: Respond ONLY with valid JSON matching the requested schema.
        Do not include any text outside the JSON object.
        """;

    public static string BuildUserPrompt(TradeReviewInput input)
    {
        var indicators = input.IndicatorValuesAtEntry is { Count: > 0 }
            ? string.Join(", ", input.IndicatorValuesAtEntry.Select(kv => $"{kv.Key}={kv.Value:F2}"))
            : "N/A";

        return $"""
            Review this completed trade:

            Symbol: {input.Symbol}
            Side: {input.Side}
            Strategy: {input.StrategyName}
            Entry: {input.EntryDate:yyyy-MM-dd} at ${input.EntryPrice:F2}
            Exit: {input.ExitDate:yyyy-MM-dd} at ${input.ExitPrice:F2}
            P&L: {input.PnlPercent:F2}%
            Indicators at entry: {indicators}

            Respond with JSON matching this schema:
            classification (one of: Good, Bad, Lucky, Unlucky),
            score (integer 1-10),
            strengths (array of strings),
            weaknesses (array of strings),
            lessonsLearned (array of strings),
            summary (string).
            """;
    }

    public static TradeReviewOutput? ParseResponse(string json)
    {
        var trimmed = ExtractJson(json);
        return JsonSerializer.Deserialize<TradeReviewOutput>(trimmed, JsonOptions.Default);
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
