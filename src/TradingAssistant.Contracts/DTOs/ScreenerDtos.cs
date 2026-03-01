namespace TradingAssistant.Contracts.DTOs;

public record ScreenerRunDto(
    Guid Id,
    DateTime ScanDate,
    string StrategyName,
    int SymbolsScanned,
    int SignalsFound,
    DateTime CreatedAt);

public record ScreenerSignalDto(
    string Symbol,
    string Grade,
    decimal Score,
    string Direction,
    decimal EntryPrice,
    decimal StopPrice,
    decimal TargetPrice,
    decimal RiskRewardRatio,
    decimal? HistoricalWinRate,
    DateTime SignalDate,
    List<ScreenerBreakdownEntryDto> Breakdown);

public record ScreenerBreakdownEntryDto(
    string Factor,
    decimal RawScore,
    decimal Weight,
    decimal WeightedScore,
    string Reason);

public record ScreenerResultsDto(
    Guid RunId,
    DateTime ScanDate,
    string StrategyName,
    int SymbolsScanned,
    int SignalsFound,
    int SignalsPassingFilter,
    List<ScreenerSignalDto> Signals,
    List<string> Warnings);
