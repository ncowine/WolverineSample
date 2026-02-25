using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.MarketData;

public class Watchlist : BaseEntity
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<WatchlistItem> Items { get; set; } = new List<WatchlistItem>();
}
