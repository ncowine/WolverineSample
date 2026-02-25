using TradingAssistant.Contracts.Commands;

namespace TradingAssistant.Contracts.DTOs;

public record StrategyDto(
    Guid Id,
    string Name,
    string Description,
    bool IsActive,
    List<StrategyRuleDto> Rules,
    DateTime CreatedAt);
