using Microsoft.AspNetCore.Authorization;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using Wolverine;
using Wolverine.Http;

namespace TradingAssistant.Api.Endpoints;

public static class WatchlistEndpoints
{
    [Authorize]
    [WolverinePost("/api/watchlists")]
    public static async Task<WatchlistDto> CreateWatchlist(
        CreateWatchlistCommand command, IMessageBus bus)
    {
        return await bus.InvokeAsync<WatchlistDto>(command);
    }

    [Authorize]
    [WolverineGet("/api/watchlists")]
    public static async Task<List<WatchlistDto>> GetWatchlists(IMessageBus bus)
    {
        return await bus.InvokeAsync<List<WatchlistDto>>(new GetWatchlistsQuery());
    }

    [Authorize]
    [WolverinePost("/api/watchlists/{watchlistId}/items")]
    public static async Task<WatchlistItemDto> AddItem(
        Guid watchlistId, AddWatchlistItemCommand command, IMessageBus bus)
    {
        return await bus.InvokeAsync<WatchlistItemDto>(
            command with { WatchlistId = watchlistId });
    }

    [Authorize]
    [WolverineDelete("/api/watchlists/{watchlistId}/items/{symbol}")]
    public static async Task<string> RemoveItem(
        Guid watchlistId, string symbol, IMessageBus bus)
    {
        return await bus.InvokeAsync<string>(
            new RemoveWatchlistItemCommand(watchlistId, symbol));
    }

    [Authorize]
    [WolverineDelete("/api/watchlists/{watchlistId}")]
    public static async Task<string> DeleteWatchlist(
        Guid watchlistId, IMessageBus bus)
    {
        return await bus.InvokeAsync<string>(
            new DeleteWatchlistCommand(watchlistId));
    }

    [Authorize]
    [WolverineGet("/api/watchlists/{watchlistId}/prices")]
    public static async Task<List<StockPriceDto>> GetWatchlistPrices(
        Guid watchlistId, IMessageBus bus)
    {
        return await bus.InvokeAsync<List<StockPriceDto>>(
            new GetWatchlistPricesQuery(watchlistId));
    }
}
