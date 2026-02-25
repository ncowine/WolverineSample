namespace TradingAssistant.Contracts.Commands;

public record RunBacktestCommand(
    Guid StrategyId,
    string Symbol,
    DateTime StartDate,
    DateTime EndDate);
