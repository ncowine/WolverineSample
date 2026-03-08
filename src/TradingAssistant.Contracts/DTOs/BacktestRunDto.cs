namespace TradingAssistant.Contracts.DTOs;

public record BacktestRunDto(
    Guid Id,
    Guid StrategyId,
    string StrategyName,
    string Symbol,
    string Status,
    DateTime StartDate,
    DateTime EndDate,
    DateTime CreatedAt,
    // Portfolio fields (nullable)
    Guid? UniverseId = null,
    string? UniverseName = null,
    decimal InitialCapital = 100_000m,
    int MaxPositions = 10,
    int? TotalSymbols = null,
    int? SymbolsWithData = null);
