using TradingAssistant.Application.Services;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Domain.MarketData;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.MarketData;

public class CreateWatchlistHandler
{
    public static async Task<WatchlistDto> HandleAsync(
        CreateWatchlistCommand command,
        MarketDataDbContext db,
        ICurrentUser currentUser)
    {
        var watchlist = new Watchlist
        {
            UserId = currentUser.UserId,
            Name = command.Name.Trim()
        };

        db.Watchlists.Add(watchlist);
        await db.SaveChangesAsync();

        return new WatchlistDto(
            watchlist.Id,
            watchlist.Name,
            watchlist.CreatedAt,
            new List<WatchlistItemDto>());
    }
}
