namespace TradingAssistant.Domain.MarketData;

public class WatchlistItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WatchlistId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    public Watchlist Watchlist { get; set; } = null!;
}
