namespace TradingAssistant.Contracts.DTOs;

public record OptimizedParameterSetDto(
    Guid Id,
    Guid StrategyId,
    Dictionary<string, decimal> Parameters,
    decimal AvgOutOfSampleSharpe,
    decimal AvgEfficiency,
    decimal AvgOverfittingScore,
    string OverfittingGrade,
    int WindowCount,
    int Version,
    bool IsActive,
    DateTime CreatedAt);

/// <summary>
/// Current active params + version history for a strategy.
/// </summary>
public record OptimizedParamsResponse(
    OptimizedParameterSetDto? Current,
    List<OptimizedParameterSetDto> History);
