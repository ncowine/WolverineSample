using System.Text.Json;
using System.Text.Json.Serialization;

namespace TradingAssistant.Application.Intelligence.Prompts;

public record StrategyAutopsyInput(
    string StrategyName,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal TotalReturnPercent,
    decimal MaxDrawdownPercent,
    decimal WinRate,
    decimal SharpeRatio,
    int TradeCount,
    IReadOnlyList<string>? WorstTrades = null,
    string? RegimesDuringPeriod = null);

public record StrategyAutopsyOutput
{
    [JsonPropertyName("primaryLossReason")]
    public string PrimaryLossReason { get; init; } = string.Empty;

    [JsonPropertyName("rootCauses")]
    public IReadOnlyList<string> RootCauses { get; init; } = [];

    [JsonPropertyName("marketConditionImpact")]
    public string MarketConditionImpact { get; init; } = string.Empty;

    [JsonPropertyName("recommendations")]
    public IReadOnlyList<string> Recommendations { get; init; } = [];

    [JsonPropertyName("shouldRetire")]
    public bool ShouldRetire { get; init; }

    [JsonPropertyName("confidence")]
    public decimal Confidence { get; init; }

    [JsonPropertyName("summary")]
    public string Summary { get; init; } = string.Empty;
}

/// <summary>
/// Prompt template for Claude to perform a post-mortem on an underperforming strategy.
/// </summary>
public static class StrategyAutopsyPrompt
{
    public static string BuildSystemPrompt() =>
        """
        You are an expert quantitative analyst performing strategy post-mortems.
        Analyze why a strategy underperformed and recommend whether to retire, modify, or keep it.
        Be data-driven and specific in your analysis.

        IMPORTANT: Respond ONLY with valid JSON matching the requested schema.
        Do not include any text outside the JSON object.
        """;

    public static string BuildUserPrompt(StrategyAutopsyInput input)
    {
        var worstTrades = input.WorstTrades is { Count: > 0 }
            ? string.Join("\n  - ", input.WorstTrades)
            : "N/A";
        var regimes = input.RegimesDuringPeriod ?? "Unknown";

        return $"""
            Perform an autopsy on this strategy:

            Strategy: {input.StrategyName}
            Period: {input.PeriodStart:yyyy-MM-dd} to {input.PeriodEnd:yyyy-MM-dd}
            Total Return: {input.TotalReturnPercent:F2}%
            Max Drawdown: {input.MaxDrawdownPercent:F2}%
            Win Rate: {input.WinRate:F2}%
            Sharpe Ratio: {input.SharpeRatio:F2}
            Trade Count: {input.TradeCount}
            Regimes during period: {regimes}
            Worst trades:
              - {worstTrades}

            Respond with JSON matching this schema:
            primaryLossReason (string — MUST be one of: "RegimeMismatch", "SignalDegradation", "BlackSwan", "PositionSizingError", "StopLossFailure"),
            rootCauses (array of strings),
            marketConditionImpact (string),
            recommendations (array of strings — actionable, e.g. "tighten stops", "reduce allocation", "retire"),
            shouldRetire (boolean),
            confidence (decimal 0.0-1.0),
            summary (string).
            """;
    }

    public static StrategyAutopsyOutput? ParseResponse(string json)
    {
        var trimmed = ExtractJson(json);
        return JsonSerializer.Deserialize<StrategyAutopsyOutput>(trimmed, JsonOptions.Default);
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
