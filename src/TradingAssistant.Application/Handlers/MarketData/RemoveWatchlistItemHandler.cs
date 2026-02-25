using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application.Exceptions;
using TradingAssistant.Application.Services;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.MarketData;

public class RemoveWatchlistItemHandler
{
    public static async Task<string> HandleAsync(
        RemoveWatchlistItemCommand command,
        MarketDataDbContext db,
        ICurrentUser currentUser)
    {
        var watchlist = await db.Watchlists
            .FirstOrDefaultAsync(w => w.Id == command.WatchlistId)
            ?? throw new InvalidOperationException($"Watchlist '{command.WatchlistId}' not found.");

        if (watchlist.UserId != currentUser.UserId)
            throw new ForbiddenAccessException("You do not have access to this watchlist.");

        var symbolUpper = command.Symbol.Trim().ToUpperInvariant();

        var item = await db.WatchlistItems
            .FirstOrDefaultAsync(i => i.WatchlistId == command.WatchlistId && i.Symbol == symbolUpper)
            ?? throw new InvalidOperationException($"Symbol '{symbolUpper}' not found in this watchlist.");

        db.WatchlistItems.Remove(item);
        await db.SaveChangesAsync();

        return $"Symbol '{symbolUpper}' removed from watchlist.";
    }
}
