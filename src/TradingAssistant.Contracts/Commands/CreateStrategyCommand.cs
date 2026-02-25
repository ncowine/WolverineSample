namespace TradingAssistant.Contracts.Commands;

public record CreateStrategyCommand(
    string Name,
    string Description,
    List<StrategyRuleDto> Rules);

public record StrategyRuleDto(
    string IndicatorType,
    string Condition,
    decimal Threshold,
    string SignalType);
