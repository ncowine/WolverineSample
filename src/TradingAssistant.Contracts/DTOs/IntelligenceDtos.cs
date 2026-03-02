namespace TradingAssistant.Contracts.DTOs;

public record BreadthSnapshotDto(
    Guid Id,
    string MarketCode,
    DateTime SnapshotDate,
    decimal AdvanceDeclineRatio,
    decimal PctAbove200Sma,
    decimal PctAbove50Sma,
    int NewHighs,
    int NewLows,
    int TotalStocks,
    int Advancing,
    int Declining);

public record CorrelationMatrixDto(
    Guid Id,
    DateTime SnapshotDate,
    int LookbackDays,
    string MatrixJson);

public record PipelineStepStatusDto(
    string StepName,
    int StepOrder,
    string Status,
    double DurationSeconds,
    string? ErrorMessage,
    int RetryCount);

public record PipelineRunStatusDto(
    string MarketCode,
    DateTime RunDate,
    string OverallStatus,
    int CompletedSteps,
    int FailedSteps,
    int SkippedSteps,
    int TotalSteps,
    IReadOnlyList<PipelineStepStatusDto> Steps);
