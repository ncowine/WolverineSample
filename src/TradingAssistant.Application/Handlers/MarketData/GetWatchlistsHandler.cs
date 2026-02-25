using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application.Services;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.MarketData;

public class GetWatchlistsHandler
{
    public static async Task<List<WatchlistDto>> HandleAsync(
        GetWatchlistsQuery query,
        MarketDataDbContext db,
        ICurrentUser currentUser)
    {
        var watchlists = await db.Watchlists
            .Include(w => w.Items)
            .Where(w => w.UserId == currentUser.UserId)
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new WatchlistDto(
                w.Id,
                w.Name,
                w.CreatedAt,
                w.Items.OrderByDescending(i => i.AddedAt)
                    .Select(i => new WatchlistItemDto(i.Id, i.Symbol, i.AddedAt))
                    .ToList()))
            .ToListAsync();

        return watchlists;
    }
}
