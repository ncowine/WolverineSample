namespace TradingAssistant.Contracts.DTOs;

public record TournamentRunDto(
    Guid Id,
    string MarketCode,
    DateTime StartDate,
    DateTime? EndDate,
    string Status,
    int MaxEntries,
    int EntryCount,
    string Description);
