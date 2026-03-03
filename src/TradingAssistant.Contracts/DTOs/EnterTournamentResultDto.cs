namespace TradingAssistant.Contracts.DTOs;

public record EnterTournamentResultDto(
    bool Success,
    Guid? EntryId,
    Guid? PaperAccountId,
    string StrategyName,
    string? Error = null);
