namespace TradingAssistant.Contracts.DTOs;

public record StockUniverseDto(
    Guid Id,
    string Name,
    string Description,
    List<string> Symbols,
    bool IsActive,
    bool IncludesBenchmark,
    DateTime CreatedAt);
