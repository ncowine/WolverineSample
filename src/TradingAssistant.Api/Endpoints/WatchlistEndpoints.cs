using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using Wolverine;
using Wolverine.Http;

namespace TradingAssistant.Api.Endpoints;

public class WatchlistEndpoints : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/watchlists")
            .WithTags("Watchlists")
            .RequireAuthorization();

        group.MapPostToWolverine<CreateWatchlistCommand, WatchlistDto>("/")
            .WithSummary("Create a new watchlist");

        group.MapGet("/", GetWatchlists)
            .WithSummary("Get all watchlists for the current user");

        // Route param + body — needs manual handler
        group.MapPost("/{watchlistId}/items", AddItem)
            .WithSummary("Add a stock to a watchlist");

        group.MapDelete("/{watchlistId}/items/{symbol}", RemoveItem)
            .WithSummary("Remove a stock from a watchlist");

        group.MapDelete("/{watchlistId}", DeleteWatchlist)
            .WithSummary("Delete a watchlist");

        group.MapGet("/{watchlistId}/prices", GetWatchlistPrices)
            .WithSummary("Get current prices for all stocks in a watchlist");
    }

    private static async Task<List<WatchlistDto>> GetWatchlists(IMessageBus bus)
    {
        return await bus.InvokeAsync<List<WatchlistDto>>(new GetWatchlistsQuery());
    }

    private static async Task<WatchlistItemDto> AddItem(
        Guid watchlistId, AddWatchlistItemCommand command, IMessageBus bus)
    {
        return await bus.InvokeAsync<WatchlistItemDto>(
            command with { WatchlistId = watchlistId });
    }

    private static async Task<string> RemoveItem(
        Guid watchlistId, string symbol, IMessageBus bus)
    {
        return await bus.InvokeAsync<string>(
            new RemoveWatchlistItemCommand(watchlistId, symbol));
    }

    private static async Task<string> DeleteWatchlist(
        Guid watchlistId, IMessageBus bus)
    {
        return await bus.InvokeAsync<string>(
            new DeleteWatchlistCommand(watchlistId));
    }

    private static async Task<List<StockPriceDto>> GetWatchlistPrices(
        Guid watchlistId, IMessageBus bus)
    {
        return await bus.InvokeAsync<List<StockPriceDto>>(
            new GetWatchlistPricesQuery(watchlistId));
    }
}
