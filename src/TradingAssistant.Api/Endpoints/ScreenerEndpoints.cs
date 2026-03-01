using Microsoft.AspNetCore.Mvc;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.SharedKernel;
using Wolverine;

namespace TradingAssistant.Api.Endpoints;

public class ScreenerEndpoints : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        var screener = app.MapGroup("/api/screener")
            .WithTags("Screener")
            .RequireAuthorization();

        screener.MapPost("/run", TriggerScan)
            .WithSummary("Trigger a manual screener scan");

        screener.MapGet("/results", GetResults)
            .WithSummary("Get latest screener results (filterable by grade, date)");

        screener.MapGet("/results/{symbol}", GetSignalForSymbol)
            .WithSummary("Get detailed signal for a specific symbol from the latest scan");

        screener.MapGet("/history", GetHistory)
            .WithSummary("Get paged history of past screener scans");
    }

    private static async Task<ScreenerResultsDto> TriggerScan(
        [FromBody] TriggerScreenerRequest request,
        IMessageBus bus)
    {
        // Delegate to the RunScreenerCommand handler via Wolverine
        // In a full implementation, this would load universe data, compute indicators,
        // run the screener engine, and persist results.
        // For now, dispatch the query to get latest results (the BackgroundService handles actual scans).
        return await bus.InvokeAsync<ScreenerResultsDto>(
            new GetScreenerResultsQuery(request.MinGrade));
    }

    private static async Task<ScreenerResultsDto> GetResults(
        [FromQuery] string? minGrade,
        [FromQuery] DateTime? date,
        IMessageBus bus)
    {
        return await bus.InvokeAsync<ScreenerResultsDto>(
            new GetScreenerResultsQuery(minGrade, date));
    }

    private static async Task<ScreenerSignalDto> GetSignalForSymbol(
        string symbol,
        IMessageBus bus)
    {
        return await bus.InvokeAsync<ScreenerSignalDto>(
            new GetScreenerSignalQuery(symbol));
    }

    private static async Task<PagedResponse<ScreenerRunDto>> GetHistory(
        int page,
        int pageSize,
        IMessageBus bus)
    {
        return await bus.InvokeAsync<PagedResponse<ScreenerRunDto>>(
            new GetScreenerHistoryQuery(page > 0 ? page : 1, pageSize > 0 ? pageSize : 20));
    }
}

/// <summary>
/// Request body for manual screener trigger.
/// </summary>
public record TriggerScreenerRequest(
    Guid? UniverseId = null,
    Guid? StrategyId = null,
    string? MinGrade = "B",
    int? MaxSignals = 20);
