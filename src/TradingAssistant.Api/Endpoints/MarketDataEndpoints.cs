using Microsoft.AspNetCore.Authorization;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using Wolverine;
using Wolverine.Http;

namespace TradingAssistant.Api.Endpoints;

public static class MarketDataEndpoints
{
    [AllowAnonymous]
    [WolverinePost("/api/market-data/seed")]
    public static async Task<SeedMarketDataResponse> SeedMarketData(IMessageBus bus)
    {
        return await bus.InvokeAsync<SeedMarketDataResponse>(new SeedMarketDataCommand());
    }

    [Authorize]
    [WolverineGet("/api/market-data/stocks/{symbol}/price")]
    public static async Task<StockPriceDto> GetStockPrice(string symbol, IMessageBus bus)
    {
        return await bus.InvokeAsync<StockPriceDto>(new GetStockPriceQuery(symbol));
    }

    [Authorize]
    [WolverineGet("/api/market-data/stocks/{symbol}/history")]
    public static async Task<List<CandleDto>> GetHistoricalPrices(
        string symbol,
        DateTime startDate,
        DateTime endDate,
        string interval,
        IMessageBus bus)
    {
        return await bus.InvokeAsync<List<CandleDto>>(
            new GetHistoricalPricesQuery(symbol, startDate, endDate, interval ?? "Daily"));
    }
}
