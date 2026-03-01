using TradingAssistant.Contracts.Backtesting;

namespace TradingAssistant.Contracts.Commands;

public record CreateStrategyV2Command(
    string Name,
    string? Description,
    StrategyDefinition Definition);
