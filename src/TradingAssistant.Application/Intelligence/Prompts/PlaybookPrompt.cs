using System.Text.Json;
using TradingAssistant.Contracts.Backtesting;

namespace TradingAssistant.Application.Intelligence.Prompts;

/// <summary>
/// Prompt for Claude to generate a market-specific strategy template (playbook).
/// One call per (marketCode, templateType) pair.
/// </summary>
public static class PlaybookPrompt
{
    public static string BuildSystemPrompt() =>
        """
        You are an expert quantitative trading strategist specializing in market-specific
        strategy design. Generate strategy templates as structured JSON matching the
        StrategyDefinition schema exactly. Templates must be tuned for the specific market's
        characteristics (volatility, liquidity, trading hours, transaction costs).

        IMPORTANT: Respond ONLY with valid JSON matching the requested schema.
        Do not include any text outside the JSON object.
        """;

    public static string BuildUserPrompt(PlaybookInput input)
    {
        var marketDesc = string.IsNullOrWhiteSpace(input.MarketDescription)
            ? input.MarketCode
            : input.MarketDescription;

        return $"""
            Generate a {input.TemplateType} strategy template for {input.MarketCode} ({marketDesc}).

            Template type: {input.TemplateType}
            - Momentum: Trend-following with moving average crossovers, RSI confirmation, weekly trend alignment.
            - MeanReversion: Oversold entries using RSI/Bollinger Bands with trend filter, exits on mean recovery.
            - Breakout: Price/volume breakout above resistance with ATR-based volatility confirmation.

            Market characteristics:
            {marketDesc}

            Tune parameters for this market:
            - India markets: wider stops (3x ATR), higher volume thresholds (500K+), lower risk per trade (0.75%)
            - US markets: standard stops (2x ATR), moderate volume thresholds (200K+), standard risk (1%)

            Available indicators: RSI, MACD, SMA, EMA, BollingerBands, WMA, Stochastic, ATR, OBV, Price, Volume.
            Available comparisons: CrossAbove, CrossBelow, GreaterThan, LessThan, Between.
            Timeframes: Daily, Weekly, Monthly.
            StopLoss types: Atr, FixedPercent, Support.
            TakeProfit types: RMultiple, FixedPercent, Resistance.
            Position sizing methods: Fixed, Kelly.

            Respond with JSON matching this schema:
            entryConditions: array of objects with "timeframe" and "conditions" (array of objects with indicator, comparison, value, valueHigh?, period?, referenceIndicator?, referencePeriod?)
            exitConditions: same structure as entryConditions
            stopLoss: object with "type" and "multiplier"
            takeProfit: object with "type" and "multiplier"
            positionSizing: object with sizingMethod, riskPercent, maxPositions, maxPortfolioHeat, maxDrawdownPercent
            filters: object with minVolume?, minPrice?, maxPrice?

            Include 1-2 entry condition groups (multi-timeframe) and 1 exit condition group.
            Use concrete indicator periods.
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

public record PlaybookInput(
    string MarketCode,
    string TemplateType,
    string? MarketDescription = null);
