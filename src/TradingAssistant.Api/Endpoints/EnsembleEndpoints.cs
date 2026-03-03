using Microsoft.AspNetCore.Mvc;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.SharedKernel;
using Wolverine;

namespace TradingAssistant.Api.Endpoints;

public class EnsembleEndpoints : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ensemble")
            .WithTags("Ensemble Voting")
            .RequireAuthorization();

        group.MapPost("/compute", ComputeEnsembleSignals)
            .WithSummary("Compute ensemble signals by aggregating votes from promoted strategies");

        group.MapGet("/signals/{marketCode}", GetEnsembleSignals)
            .WithSummary("Get ensemble signals for a market, optionally filtered by date");

        group.MapPost("/replay", ReplayEnsemble)
            .WithSummary("Replay ensemble voting logic on historical screener data");
    }

    private static async Task<EnsembleComputeResultDto> ComputeEnsembleSignals(
        [FromBody] ComputeEnsembleSignalsCommand command, IMessageBus bus)
    {
        return await bus.InvokeAsync<EnsembleComputeResultDto>(command);
    }

    private static async Task<IReadOnlyList<EnsembleSignalDto>> GetEnsembleSignals(
        [FromRoute] string marketCode,
        [FromQuery] DateTime? date,
        IMessageBus bus)
    {
        return await bus.InvokeAsync<IReadOnlyList<EnsembleSignalDto>>(
            new GetEnsembleSignalsQuery(marketCode, date));
    }

    private static async Task<EnsembleReplayResultDto> ReplayEnsemble(
        [FromBody] ReplayEnsembleCommand command, IMessageBus bus)
    {
        return await bus.InvokeAsync<EnsembleReplayResultDto>(command);
    }
}
