using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingAssistant.Application.Handlers.Intelligence;
using TradingAssistant.Application.Intelligence;
using TradingAssistant.Contracts;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Api.Endpoints;

public class FeedbackEndpoints : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/feedback")
            .WithTags("Feedback")
            .RequireAuthorization();

        group.MapGet("/pipeline-status", GetPipelineStatus)
            .WithSummary("Get latest pipeline run status per market");

        group.MapGet("/pipeline-status/{marketCode}", GetPipelineStatusByMarket)
            .WithSummary("Get latest pipeline run status for a specific market");

        group.MapPost("/generate-strategy", GenerateStrategy)
            .WithSummary("Generate a trading strategy using AI with auto-backtest validation");

        group.MapGet("/strategy-assignment/{marketCode}", GetStrategyAssignment)
            .WithSummary("Get current strategy assignment for a market");

        group.MapPost("/lock-strategy", LockStrategy)
            .WithSummary("Lock a strategy assignment (prevents automatic regime-based changes)");

        group.MapDelete("/lock-strategy/{marketCode}", UnlockStrategy)
            .WithSummary("Unlock a strategy assignment (allows automatic regime-based selection)");
    }

    private static async Task<IReadOnlyList<PipelineRunStatusDto>> GetPipelineStatus(
        IntelligenceDbContext db)
    {
        return await GetPipelineStatusHandler.HandleAsync(new GetPipelineStatusQuery(), db);
    }

    private static async Task<IReadOnlyList<PipelineRunStatusDto>> GetPipelineStatusByMarket(
        [FromRoute] string marketCode,
        IntelligenceDbContext db)
    {
        return await GetPipelineStatusHandler.HandleAsync(
            new GetPipelineStatusQuery(marketCode), db);
    }

    private static async Task<GenerateStrategyResultDto> GenerateStrategy(
        [FromBody] GenerateStrategyCommand command,
        IClaudeClient claude,
        MarketDataDbContext marketDb,
        BacktestDbContext backtestDb,
        ILogger<GenerateStrategyHandler> logger)
    {
        return await GenerateStrategyHandler.HandleAsync(command, claude, marketDb, backtestDb, logger);
    }

    private static async Task<IResult> GetStrategyAssignment(
        [FromRoute] string marketCode,
        IntelligenceDbContext db)
    {
        var assignment = await db.StrategyAssignments
            .FirstOrDefaultAsync(a => a.MarketCode == marketCode);

        if (assignment is null)
            return Results.NotFound($"No strategy assignment for market '{marketCode}'.");

        return Results.Ok(StrategyAssignmentMapper.MapToDto(assignment));
    }

    private static async Task<StrategyAssignmentDto> LockStrategy(
        [FromBody] LockStrategyCommand command,
        IntelligenceDbContext db)
    {
        return await LockStrategyHandler.HandleAsync(command, db);
    }

    private static async Task<IResult> UnlockStrategy(
        [FromRoute] string marketCode,
        IntelligenceDbContext db)
    {
        var result = await UnlockStrategyHandler.HandleAsync(new UnlockStrategyCommand(marketCode), db);
        return result is null
            ? Results.NotFound($"No strategy assignment for market '{marketCode}'.")
            : Results.Ok(result);
    }
}
