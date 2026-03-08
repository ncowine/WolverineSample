namespace TradingAssistant.Contracts.DTOs;

public record AutoOptimizeResultDto(
    List<string> Diagnoses,
    List<ParameterRangeDto> GeneratedRanges,
    decimal BeforeWinRate,
    decimal BeforeSharpe,
    decimal BeforeMaxDrawdown,
    decimal BeforeCagr,
    decimal BeforeProfitFactor,
    OptimizationResultDto Optimization);
