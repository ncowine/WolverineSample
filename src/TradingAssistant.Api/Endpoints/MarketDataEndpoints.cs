using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using Wolverine;
using Wolverine.Http;

namespace TradingAssistant.Api.Endpoints;

public class MarketDataEndpoints : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/market-data")
            .WithTags("Market Data");

        group.MapPostToWolverine<SeedMarketDataCommand, SeedMarketDataResponse>("/seed")
            .WithSummary("Seed stocks and historical candle data")
            .AllowAnonymous();

        group.MapPostToWolverine<FetchMarketDataCommand, FetchMarketDataResponse>("/fetch")
            .WithSummary("Fetch raw daily candles from Yahoo Finance")
            .RequireAuthorization();

        group.MapPostToWolverine<IngestMarketDataCommand, IngestMarketDataResponse>("/ingest")
            .WithSummary("Ingest market data with daily/weekly/monthly aggregation")
            .RequireAuthorization();

        group.MapGet("/stocks/{symbol}/price", GetStockPrice)
            .WithSummary("Get current price for a stock")
            .RequireAuthorization();

        group.MapGet("/stocks/{symbol}/history", GetHistoricalPrices)
            .WithSummary("Get historical price candles for a stock")
            .RequireAuthorization();
    }

    private static async Task<StockPriceDto> GetStockPrice(string symbol, IMessageBus bus)
    {
        return await bus.InvokeAsync<StockPriceDto>(new GetStockPriceQuery(symbol));
    }

    private static async Task<List<CandleDto>> GetHistoricalPrices(
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
