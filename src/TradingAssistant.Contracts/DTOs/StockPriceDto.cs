namespace TradingAssistant.Contracts.DTOs;

public record StockPriceDto(
    string Symbol,
    string Name,
    decimal CurrentPrice,
    decimal Change,
    decimal ChangePercent,
    long Volume,
    DateTime Timestamp);
