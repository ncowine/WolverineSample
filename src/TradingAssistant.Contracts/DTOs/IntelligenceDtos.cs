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
