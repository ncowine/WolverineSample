using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application.Exceptions;
using TradingAssistant.Application.Services;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Domain.MarketData;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.MarketData;

public class AddWatchlistItemHandler
{
    public static async Task<WatchlistItemDto> HandleAsync(
        AddWatchlistItemCommand command,
        MarketDataDbContext db,
        ICurrentUser currentUser)
    {
        var watchlist = await db.Watchlists
            .FirstOrDefaultAsync(w => w.Id == command.WatchlistId)
            ?? throw new InvalidOperationException($"Watchlist '{command.WatchlistId}' not found.");

        if (watchlist.UserId != currentUser.UserId)
            throw new ForbiddenAccessException("You do not have access to this watchlist.");

        var symbolUpper = command.Symbol.Trim().ToUpperInvariant();

        var stockExists = await db.Stocks.AnyAsync(s => s.Symbol == symbolUpper);
        if (!stockExists)
            throw new InvalidOperationException($"Stock symbol '{symbolUpper}' not found.");

        var alreadyAdded = await db.WatchlistItems
            .AnyAsync(i => i.WatchlistId == command.WatchlistId && i.Symbol == symbolUpper);
        if (alreadyAdded)
            throw new InvalidOperationException($"Symbol '{symbolUpper}' is already in this watchlist.");

        var item = new WatchlistItem
        {
            WatchlistId = command.WatchlistId,
            Symbol = symbolUpper
        };

        db.WatchlistItems.Add(item);
        await db.SaveChangesAsync();

        return new WatchlistItemDto(item.Id, item.Symbol, item.AddedAt);
    }
}
