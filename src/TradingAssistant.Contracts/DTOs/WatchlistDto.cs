namespace TradingAssistant.Contracts.DTOs;

public record WatchlistDto(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    List<WatchlistItemDto> Items);

public record WatchlistItemDto(
    Guid Id,
    string Symbol,
    DateTime AddedAt);
