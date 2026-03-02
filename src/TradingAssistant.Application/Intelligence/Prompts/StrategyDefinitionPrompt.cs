using System.Text.Json;
using TradingAssistant.Contracts.Backtesting;

namespace TradingAssistant.Application.Intelligence.Prompts;

/// <summary>
/// Prompt template for Claude to generate a full StrategyDefinition JSON
/// (the rich format consumed by BacktestEngine).
/// </summary>
public static class StrategyDefinitionPrompt
{
    public static string BuildSystemPrompt() =>
        """
        You are an expert quantitative trading strategist. Generate trading strategies
        as structured JSON matching the StrategyDefinition schema exactly.
        Your strategies must be specific, testable, and include concrete indicator parameters.

        IMPORTANT: Respond ONLY with valid JSON matching the requested schema.
        Do not include any text outside the JSON object.
        """;

    public static string BuildUserPrompt(StrategyDefinitionInput input)
    {
        var constraints = string.IsNullOrWhiteSpace(input.AdditionalConstraints)
            ? "None"
            : input.AdditionalConstraints;

        return $"""
            Generate a complete trading strategy for {input.MarketCode} optimized for a {input.RegimeType} market regime.

            Description from user: {input.Description}
            Max drawdown tolerance: {input.MaxDrawdownPercent}%
            Target Sharpe ratio: {input.TargetSharpe}
            Additional constraints: {constraints}

            Available indicators for conditions: RSI, MACD, SMA, EMA, BollingerBands, WMA, Stochastic, ATR, OBV, Price, Volume.
            Available comparisons: CrossAbove, CrossBelow, GreaterThan, LessThan, Between.
            Available timeframes for condition groups: Daily, Weekly, Monthly.
            StopLoss types: Atr, FixedPercent, Support.
            TakeProfit types: RMultiple, FixedPercent, Resistance.
            Position sizing methods: Fixed, Kelly.

            Respond with JSON matching this exact schema:

            entryConditions: array of condition groups. Each group has:
              - timeframe (string: Daily/Weekly/Monthly)
              - conditions: array of condition objects, each with:
                - indicator (string: one of the available indicators)
                - comparison (string: one of the available comparisons)
                - value (number: threshold value)
                - valueHigh (number, optional: upper bound for Between)
                - period (number, optional: indicator period override e.g. 50 for SMA50)
                - referenceIndicator (string, optional: for crossover comparisons)
                - referencePeriod (number, optional: period for reference indicator)

            exitConditions: same structure as entryConditions.

            stopLoss: object with type (string) and multiplier (number).
            takeProfit: object with type (string) and multiplier (number).

            positionSizing: object with:
              - sizingMethod (string: Fixed or Kelly)
              - riskPercent (number: % of account to risk per trade)
              - maxPositions (number: max simultaneous positions)
              - maxPortfolioHeat (number: max total portfolio risk %)
              - maxDrawdownPercent (number: circuit breaker threshold)

            filters: object with:
              - minVolume (number, optional)
              - minPrice (number, optional)
              - maxPrice (number, optional)

            Include at least 1-2 entry condition groups and 1 exit condition group.
            Use concrete indicator periods (e.g. period: 14 for RSI, period: 50 for SMA).
            """;
    }

    public static StrategyDefinition? ParseResponse(string json)
    {
        try
        {
            var trimmed = ExtractJson(json);
            return JsonSerializer.Deserialize<StrategyDefinition>(trimmed, JsonOptions.Default);
        }
        catch (JsonException)
        {
            return null;
        }
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

public record StrategyDefinitionInput(
    string Description,
    string MarketCode,
    string RegimeType,
    decimal MaxDrawdownPercent,
    decimal TargetSharpe,
    string? AdditionalConstraints = null);
