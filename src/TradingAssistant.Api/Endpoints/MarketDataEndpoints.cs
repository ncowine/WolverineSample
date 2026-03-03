using Microsoft.AspNetCore.Mvc;
using TradingAssistant.Application.Handlers.MarketData;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Infrastructure.Persistence;
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

        group.MapPost("/backfill", InitiateBackfill)
            .WithSummary("Start bulk data backfill for a stock universe")
            .RequireAuthorization();

        group.MapGet("/backfill/{jobId:guid}", GetBackfillStatus)
            .WithSummary("Get backfill job status and progress")
            .RequireAuthorization();

        group.MapGet("/backfill", GetBackfillJobs)
            .WithSummary("List backfill jobs with optional universe filter")
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

    private static async Task<BackfillJobDto> InitiateBackfill(
        [FromBody] BackfillCommand command,
        MarketDataDbContext db,
        ILogger<InitiateBackfillHandler> logger)
    {
        return await InitiateBackfillHandler.HandleAsync(command, db, logger);
    }

    private static async Task<IResult> GetBackfillStatus(
        [FromRoute] Guid jobId,
        MarketDataDbContext db)
    {
        var result = await GetBackfillStatusHandler.HandleAsync(
            new GetBackfillStatusQuery(jobId), db);
        return result is null
            ? Results.NotFound($"Backfill job '{jobId}' not found.")
            : Results.Ok(result);
    }

    private static async Task<IReadOnlyList<BackfillJobDto>> GetBackfillJobs(
        [FromQuery] Guid? universeId,
        MarketDataDbContext db)
    {
        return await GetBackfillJobsHandler.HandleAsync(
            new GetBackfillJobsQuery(universeId), db);
    }
}
