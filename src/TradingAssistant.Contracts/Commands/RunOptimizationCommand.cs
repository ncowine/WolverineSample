using TradingAssistant.Contracts.DTOs;

namespace TradingAssistant.Contracts.Commands;

public record RunOptimizationCommand(
    Guid StrategyId,
    string Symbol,
    DateTime StartDate,
    DateTime EndDate,
    List<ParameterRangeDto> ParameterRanges,
    Guid? UniverseId = null,
    decimal InitialCapital = 100_000m,
    int MaxPositions = 10,
    string CostProfileMarket = "US");
