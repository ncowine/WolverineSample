namespace TradingAssistant.Contracts.DTOs;

public record CandleDto(
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume,
    DateTime Timestamp,
    string Interval);
