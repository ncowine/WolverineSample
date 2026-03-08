namespace TradingAssistant.Application.Backtesting;

/// <summary>
/// Explains why a trade was entered — serialized to TradeRecord.ReasoningJson.
/// </summary>
public record TradeReasoning
{
    public List<string> ConditionsFired { get; init; } = new();
    public decimal CompositeScore { get; init; }
    public Dictionary<string, decimal> FactorContributions { get; init; } = new();
    public string Regime { get; init; } = string.Empty;
    public decimal RegimeConfidence { get; init; }
}
