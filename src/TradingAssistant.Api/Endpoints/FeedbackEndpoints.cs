using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TradingAssistant.Application.Handlers.Intelligence;
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
}
