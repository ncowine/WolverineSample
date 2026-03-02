using System.Text.Json;
using System.Text.Json.Serialization;

namespace TradingAssistant.Application.Intelligence.Prompts;

public record RuleDiscoveryInput(
    string MarketCode,
    IReadOnlyList<TradeSummary> Trades);

public record TradeSummary(
    string Symbol,
    string Side,
    decimal PnlPercent,
    decimal EntryRsi,
    decimal EntryMacdHistogram,
    decimal EntrySmaSlope,
    decimal EntryAtr,
    decimal EntryVolume,
    bool WonTrade);

public record RuleDiscoveryOutput
{
    [JsonPropertyName("discoveredRules")]
    public IReadOnlyList<DiscoveredRule> DiscoveredRules { get; init; } = [];

    [JsonPropertyName("patterns")]
    public IReadOnlyList<string> Patterns { get; init; } = [];

    [JsonPropertyName("summary")]
    public string Summary { get; init; } = string.Empty;
}

public record DiscoveredRule
{
    [JsonPropertyName("rule")]
    public string Rule { get; init; } = string.Empty;

    [JsonPropertyName("confidence")]
    public decimal Confidence { get; init; }

    [JsonPropertyName("supportingTradeCount")]
    public int SupportingTradeCount { get; init; }

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;
}

/// <summary>
/// Prompt template for Claude to discover trading rules from historical trades.
/// </summary>
public static class RuleDiscoveryPrompt
{
    public static string BuildSystemPrompt() =>
        """
        You are an expert pattern recognition analyst for trading systems.
        Analyze historical trade data to discover repeating patterns and rules.
        Focus on indicator conditions that correlate with winning vs losing trades.

        IMPORTANT: Respond ONLY with valid JSON matching the requested schema.
        Do not include any text outside the JSON object.
        """;

    public static string BuildUserPrompt(RuleDiscoveryInput input)
    {
        var tradesText = string.Join("\n", input.Trades.Select((t, i) =>
            $"  {i + 1}. {t.Symbol} {t.Side} PnL={t.PnlPercent:F1}% Won={t.WonTrade} " +
            $"RSI={t.EntryRsi:F1} MACD_H={t.EntryMacdHistogram:F3} SMA_slope={t.EntrySmaSlope:F4} " +
            $"ATR={t.EntryAtr:F2} Vol={t.EntryVolume:F0}"));

        return $"""
            Analyze these {input.Trades.Count} trades from market {input.MarketCode} and discover rules:

            Trades:
            {tradesText}

            Look for patterns that distinguish winners from losers. Identify indicator thresholds
            and combinations that predict trade outcomes.

            Respond with JSON matching this schema:
            discoveredRules (array of objects with: rule (string), confidence (decimal 0-1),
              supportingTradeCount (integer), description (string)),
            patterns (array of strings),
            summary (string).
            """;
    }

    public static RuleDiscoveryOutput? ParseResponse(string json)
    {
        var trimmed = ExtractJson(json);
        return JsonSerializer.Deserialize<RuleDiscoveryOutput>(trimmed, JsonOptions.Default);
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
