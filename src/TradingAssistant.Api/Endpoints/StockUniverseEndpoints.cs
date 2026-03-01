using Microsoft.AspNetCore.Mvc;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using Wolverine;
using Wolverine.Http;

namespace TradingAssistant.Api.Endpoints;

public class StockUniverseEndpoints : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/universes")
            .WithTags("Stock Universes")
            .RequireAuthorization();

        group.MapPostToWolverine<CreateStockUniverseCommand, StockUniverseDto>("/")
            .WithSummary("Create a new stock universe");

        group.MapGet("/", GetUniverses)
            .WithSummary("List all stock universes");

        group.MapPost("/{universeId}/symbols", AddSymbols)
            .WithSummary("Add symbols to a universe");

        group.MapDelete("/{universeId}/symbols", RemoveSymbols)
            .WithSummary("Remove symbols from a universe");

        group.MapGet("/benchmark", GetBenchmarkData)
            .WithSummary("Get SPY benchmark data for a date range");
    }

    private static async Task<List<StockUniverseDto>> GetUniverses(IMessageBus bus)
    {
        return await bus.InvokeAsync<List<StockUniverseDto>>(new GetStockUniversesQuery());
    }

    private static async Task<StockUniverseDto> AddSymbols(
        [FromRoute] Guid universeId, [FromBody] AddUniverseSymbolsCommand command, IMessageBus bus)
    {
        return await bus.InvokeAsync<StockUniverseDto>(
            command with { UniverseId = universeId });
    }

    private static async Task<StockUniverseDto> RemoveSymbols(
        [FromRoute] Guid universeId, [FromBody] RemoveUniverseSymbolsCommand command, IMessageBus bus)
    {
        return await bus.InvokeAsync<StockUniverseDto>(
            command with { UniverseId = universeId });
    }

    private static async Task<List<CandleDto>> GetBenchmarkData(
        DateTime startDate, DateTime endDate, IMessageBus bus)
    {
        return await bus.InvokeAsync<List<CandleDto>>(
            new GetBenchmarkDataQuery(startDate, endDate));
    }
}
