namespace TradingAssistant.Contracts.DTOs;

public record OptimizationResultDto(
    Dictionary<string, decimal> BlessedParameters,
    decimal AvgOutOfSampleSharpe,
    decimal AvgEfficiency,
    decimal AvgOverfittingScore,
    string OverfittingGrade,
    int WindowCount,
    Guid? OptimizedParamsId);
