using Microsoft.AspNetCore.Mvc;
using TradingAssistant.Application.Handlers.Intelligence;
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
}
