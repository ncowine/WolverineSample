namespace TradingAssistant.Contracts.Commands;

/// <summary>
/// Trigger a manual screener scan.
/// </summary>
public record RunScreenerCommand(
    Guid? UniverseId = null,
    Guid? StrategyId = null,
    string? MinGrade = "B",
    int? MaxSignals = 20);
