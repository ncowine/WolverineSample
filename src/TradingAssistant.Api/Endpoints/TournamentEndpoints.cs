using Microsoft.AspNetCore.Mvc;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.SharedKernel;
using Wolverine;

namespace TradingAssistant.Api.Endpoints;

public class TournamentEndpoints : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tournament")
            .WithTags("Tournament")
            .RequireAuthorization();

        group.MapPost("/", CreateTournament)
            .WithSummary("Create a new tournament run for a market");

        group.MapPost("/{tournamentId:guid}/enter", EnterTournament)
            .WithSummary("Enter a strategy into a tournament with an isolated paper account");

        group.MapGet("/{tournamentId:guid}", GetTournament)
            .WithSummary("Get tournament run details");

        group.MapGet("/{tournamentId:guid}/entries", GetTournamentEntries)
            .WithSummary("Get all entries in a tournament ordered by performance");

        group.MapGet("/leaderboard/{marketCode}", GetLeaderboard)
            .WithSummary("Get tournament leaderboard for a market, sorted by Sharpe ratio");

        group.MapGet("/entries/{entryId:guid}", GetEntryDetail)
            .WithSummary("Get detailed tournament entry with daily equity curve");

        group.MapGet("/active-strategies/{marketCode}", GetActiveStrategies)
            .WithSummary("Get currently promoted strategies for a market");

        group.MapPost("/promote/{entryId:guid}", PromoteStrategy)
            .WithSummary("Promote a tournament entry to live trading");

        group.MapPost("/retire/{entryId:guid}", RetireStrategy)
            .WithSummary("Retire a tournament entry from trading");
    }

    private static async Task<TournamentRunDto> CreateTournament(
        [FromBody] CreateTournamentCommand command, IMessageBus bus)
    {
        return await bus.InvokeAsync<TournamentRunDto>(command);
    }

    private static async Task<EnterTournamentResultDto> EnterTournament(
        [FromRoute] Guid tournamentId,
        [FromBody] EnterTournamentRequest request,
        IMessageBus bus)
    {
        var command = new EnterTournamentCommand(
            tournamentId, request.StrategyId, request.PaperAccountBalance);
        return await bus.InvokeAsync<EnterTournamentResultDto>(command);
    }

    private static async Task<IResult> GetTournament(
        [FromRoute] Guid tournamentId, IMessageBus bus)
    {
        var result = await bus.InvokeAsync<TournamentRunDto?>(
            new GetTournamentQuery(tournamentId));

        return result is null
            ? Results.NotFound()
            : Results.Ok(result);
    }

    private static async Task<IReadOnlyList<TournamentEntryDto>> GetTournamentEntries(
        [FromRoute] Guid tournamentId, IMessageBus bus)
    {
        return await bus.InvokeAsync<IReadOnlyList<TournamentEntryDto>>(
            new GetTournamentEntriesQuery(tournamentId));
    }

    private static async Task<IReadOnlyList<LeaderboardEntryDto>> GetLeaderboard(
        [FromRoute] string marketCode, IMessageBus bus)
    {
        return await bus.InvokeAsync<IReadOnlyList<LeaderboardEntryDto>>(
            new GetLeaderboardQuery(marketCode));
    }

    private static async Task<IResult> GetEntryDetail(
        [FromRoute] Guid entryId, IMessageBus bus)
    {
        var result = await bus.InvokeAsync<TournamentEntryDetailDto?>(
            new GetTournamentEntryDetailQuery(entryId));

        return result is null
            ? Results.NotFound()
            : Results.Ok(result);
    }

    private static async Task<IReadOnlyList<ActiveStrategyDto>> GetActiveStrategies(
        [FromRoute] string marketCode, IMessageBus bus)
    {
        return await bus.InvokeAsync<IReadOnlyList<ActiveStrategyDto>>(
            new GetActiveStrategiesQuery(marketCode));
    }

    private static async Task<PromoteStrategyResultDto> PromoteStrategy(
        [FromRoute] Guid entryId,
        [FromBody] PromoteRetireRequest? request,
        IMessageBus bus)
    {
        return await bus.InvokeAsync<PromoteStrategyResultDto>(
            new PromoteStrategyCommand(entryId, request?.Force ?? false));
    }

    private static async Task<RetireStrategyResultDto> RetireStrategy(
        [FromRoute] Guid entryId,
        [FromBody] RetireRequest? request,
        IMessageBus bus)
    {
        return await bus.InvokeAsync<RetireStrategyResultDto>(
            new RetireStrategyCommand(entryId, request?.Reason, request?.Force ?? false));
    }
}

public record EnterTournamentRequest(Guid StrategyId, decimal PaperAccountBalance = 100_000m);
public record PromoteRetireRequest(bool Force = false);
public record RetireRequest(string? Reason = null, bool Force = false);
