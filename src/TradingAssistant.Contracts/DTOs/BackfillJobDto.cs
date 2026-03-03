namespace TradingAssistant.Contracts.DTOs;

public record BackfillJobDto(
    Guid Id,
    Guid UniverseId,
    int YearsBack,
    bool IsIncremental,
    string Status,
    int TotalSymbols,
    int CompletedSymbols,
    int FailedSymbols,
    string ErrorLog,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt);
