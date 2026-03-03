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
using TradingAssistant.SharedKernel;

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

        group.MapPost("/review-trade", ReviewTrade)
            .WithSummary("Run Claude AI review on a completed trade");

        group.MapGet("/trade-reviews", GetTradeReviews)
            .WithSummary("Get paginated trade reviews with optional filters");

        group.MapGet("/trade-reviews/{tradeId:guid}", GetTradeReviewByTradeId)
            .WithSummary("Get trade review for a specific trade");

        group.MapPost("/check-decay", CheckDecay)
            .WithSummary("Check if a strategy is showing signs of decay");

        group.MapGet("/decay-alerts", GetDecayAlerts)
            .WithSummary("Get active decay alerts with optional market filter");

        group.MapPost("/decay-alerts/{alertId:guid}/resolve", ResolveDecayAlert)
            .WithSummary("Resolve (acknowledge) a decay alert");
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

    private static async Task<ReviewTradeResultDto> ReviewTrade(
        [FromBody] ReviewTradeCommand command,
        IClaudeClient claude,
        IntelligenceDbContext db,
        ILogger<ReviewTradeHandler> logger)
    {
        return await ReviewTradeHandler.HandleAsync(command, claude, db, logger);
    }

    private static async Task<PagedResponse<TradeReviewDto>> GetTradeReviews(
        [FromQuery] string? symbol,
        [FromQuery] string? marketCode,
        [FromQuery] string? outcomeClass,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        IntelligenceDbContext db)
    {
        return await GetTradeReviewsHandler.HandleAsync(
            new GetTradeReviewsQuery(symbol, marketCode, outcomeClass,
                page > 0 ? page : 1, pageSize > 0 ? pageSize : 20),
            db);
    }

    private static async Task<IResult> GetTradeReviewByTradeId(
        [FromRoute] Guid tradeId,
        IntelligenceDbContext db)
    {
        var result = await GetTradeReviewByTradeIdHandler.HandleAsync(
            new GetTradeReviewByTradeIdQuery(tradeId), db);
        return result is null
            ? Results.NotFound($"No review found for trade '{tradeId}'.")
            : Results.Ok(result);
    }

    private static async Task<CheckDecayResultDto> CheckDecay(
        [FromBody] CheckDecayCommand command,
        IClaudeClient claude,
        IntelligenceDbContext db,
        ILogger<CheckDecayHandler> logger)
    {
        return await CheckDecayHandler.HandleAsync(command, claude, db, logger);
    }

    private static async Task<IReadOnlyList<StrategyDecayAlertDto>> GetDecayAlerts(
        [FromQuery] string? marketCode,
        [FromQuery] bool includeResolved,
        IntelligenceDbContext db)
    {
        return await GetDecayAlertsHandler.HandleAsync(
            new GetDecayAlertsQuery(marketCode, includeResolved), db);
    }

    private static async Task<IResult> ResolveDecayAlert(
        [FromRoute] Guid alertId,
        [FromBody] ResolveDecayAlertCommand command,
        IntelligenceDbContext db)
    {
        var result = await ResolveDecayAlertHandler.HandleAsync(
            new ResolveDecayAlertCommand(alertId, command.Note), db);
        return result is null
            ? Results.NotFound($"Decay alert '{alertId}' not found.")
            : Results.Ok(result);
    }
}
