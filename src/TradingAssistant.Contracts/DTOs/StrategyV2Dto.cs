using TradingAssistant.Contracts.Backtesting;

namespace TradingAssistant.Contracts.DTOs;

public record StrategyV2Dto(
    Guid Id,
    string Name,
    string Description,
    bool IsActive,
    bool UsesV2Engine,
    StrategyDefinition? Definition,
    int EntryConditionCount,
    int ExitConditionCount,
    string StopLossDescription,
    string TakeProfitDescription,
    DateTime CreatedAt);
