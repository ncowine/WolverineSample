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
    string RegimeAtEntry,
    string RegimeAtExit,
    decimal? Grade = null,
    float? MlConfidence = null,
    IReadOnlyDictionary<string, decimal>? IndicatorValuesAtEntry = null);

public record TradeReviewOutput
{
    [JsonPropertyName("outcomeClass")]
    public string OutcomeClass { get; init; } = string.Empty;

    [JsonPropertyName("mistakeType")]
    public string? MistakeType { get; init; }

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
/// Prompt template for Claude to review a completed trade with
/// outcome classification and mistake type analysis.
/// </summary>
public static class TradeReviewPrompt
{
    public static string BuildSystemPrompt() =>
        """
        You are an expert trade analyst reviewing completed trades. For each trade you must:
        1. Classify the outcome into one of the defined outcome classes
        2. For losing trades, classify the primary mistake type
        3. Score execution quality (1-10) independent of P&L
        4. Identify specific strengths, weaknesses, and lessons learned

        Outcome classes:
        - GoodEntryGoodExit: Well-timed entry AND exit, profitable
        - GoodEntryBadExit: Good entry but poor exit timing (held too long, exited too early)
        - BadEntry: Entry was poorly timed regardless of exit
        - RegimeMismatch: Trade was wrong for the current market regime
        - StoppedCorrectly: Stopped out but the stop was well-placed (acceptable loss)
        - StoppedPrematurely: Stopped out but price later moved in the intended direction

        Mistake types (ONLY for losing trades, null for winners):
        - BadSignal: Entry signal was incorrect or misread
        - BadTiming: Right direction but wrong timing
        - RegimeMismatch: Strategy doesn't suit the current regime
        - StopTooTight: Stop loss was too close to entry
        - StopTooLoose: Stop loss was too far, allowing excessive loss
        - OversizedPosition: Position was too large for the risk
        - CorrelatedLoss: Loss due to correlated positions
        - BlackSwan: Unforeseeable market event

        IMPORTANT: Respond ONLY with valid JSON matching the requested schema.
        Do not include any text outside the JSON object.
        """;

    public static string BuildUserPrompt(TradeReviewInput input)
    {
        var indicators = input.IndicatorValuesAtEntry is { Count: > 0 }
            ? string.Join(", ", input.IndicatorValuesAtEntry.Select(kv => $"{kv.Key}={kv.Value:F2}"))
            : "N/A";

        var gradeStr = input.Grade.HasValue ? $"{input.Grade.Value:F1}/100" : "N/A";
        var mlStr = input.MlConfidence.HasValue ? $"{input.MlConfidence.Value:F2}" : "N/A";
        var duration = (input.ExitDate - input.EntryDate).TotalHours;

        return $"""
            Review this completed trade:

            Symbol: {input.Symbol}
            Side: {input.Side}
            Strategy: {input.StrategyName}
            Entry: {input.EntryDate:yyyy-MM-dd HH:mm} at ${input.EntryPrice:F2}
            Exit: {input.ExitDate:yyyy-MM-dd HH:mm} at ${input.ExitPrice:F2}
            Duration: {duration:F1} hours
            P&L: {input.PnlPercent:F2}%
            Regime at entry: {input.RegimeAtEntry}
            Regime at exit: {input.RegimeAtExit}
            Confidence grade: {gradeStr}
            ML confidence: {mlStr}
            Indicators at entry: {indicators}

            Respond with JSON matching this schema:
            outcomeClass (one of: GoodEntryGoodExit, GoodEntryBadExit, BadEntry, RegimeMismatch, StoppedCorrectly, StoppedPrematurely),
            mistakeType (one of: BadSignal, BadTiming, RegimeMismatch, StopTooTight, StopTooLoose, OversizedPosition, CorrelatedLoss, BlackSwan — or null if profitable),
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
