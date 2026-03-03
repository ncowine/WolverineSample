using Microsoft.AspNetCore.Mvc;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.SharedKernel;
using Wolverine;

namespace TradingAssistant.Api.Endpoints;

public class IntelligenceEndpoints : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/intelligence")
            .WithTags("Intelligence")
            .RequireAuthorization();

        group.MapGet("/regime/{marketCode}", GetCurrentRegime)
            .WithSummary("Get current market regime for a market");

        group.MapGet("/regime/{marketCode}/history", GetRegimeHistory)
            .WithSummary("Get paginated regime classification history");

        group.MapGet("/breadth/{marketCode}", GetLatestBreadth)
            .WithSummary("Get latest breadth snapshot for a market");

        group.MapGet("/correlations", GetCorrelationMatrix)
            .WithSummary("Get latest cross-market correlation matrix");

        group.MapGet("/market-profile/{marketCode}", GetMarketProfile)
            .WithSummary("Get market DNA profile");

        group.MapPost("/autopsy", RunAutopsy)
            .WithSummary("Run Claude-powered post-mortem on a losing strategy month");

        group.MapGet("/autopsy/{strategyId:guid}", GetAutopsyHistory)
            .WithSummary("Get autopsy history for a strategy");

        group.MapPost("/discover-rules", DiscoverRules)
            .WithSummary("Analyze trade history to discover patterns distinguishing winners from losers");

        group.MapGet("/discovered-rules/{strategyId:guid}", GetDiscoveredRules)
            .WithSummary("Get rule discovery history for a strategy");
    }

    private static async Task<MarketRegimeDto> GetCurrentRegime(
        [FromRoute] string marketCode, IMessageBus bus)
    {
        return await bus.InvokeAsync<MarketRegimeDto>(
            new GetCurrentRegimeQuery(marketCode));
    }

    private static async Task<PagedResponse<MarketRegimeDto>> GetRegimeHistory(
        [FromRoute] string marketCode,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        IMessageBus bus)
    {
        return await bus.InvokeAsync<PagedResponse<MarketRegimeDto>>(
            new GetRegimeHistoryQuery(marketCode, page > 0 ? page : 1, pageSize > 0 ? pageSize : 20));
    }

    private static async Task<BreadthSnapshotDto> GetLatestBreadth(
        [FromRoute] string marketCode, IMessageBus bus)
    {
        return await bus.InvokeAsync<BreadthSnapshotDto>(
            new GetLatestBreadthQuery(marketCode));
    }

    private static async Task<CorrelationMatrixDto> GetCorrelationMatrix(IMessageBus bus)
    {
        return await bus.InvokeAsync<CorrelationMatrixDto>(
            new GetCorrelationMatrixQuery());
    }

    private static async Task<MarketProfileDto> GetMarketProfile(
        [FromRoute] string marketCode, IMessageBus bus)
    {
        return await bus.InvokeAsync<MarketProfileDto>(
            new GetMarketProfileQuery(marketCode));
    }

    private static async Task<StrategyAutopsyResultDto> RunAutopsy(
        [FromBody] RunAutopsyCommand command, IMessageBus bus)
    {
        return await bus.InvokeAsync<StrategyAutopsyResultDto>(command);
    }

    private static async Task<IReadOnlyList<StrategyAutopsyDto>> GetAutopsyHistory(
        [FromRoute] Guid strategyId, IMessageBus bus)
    {
        return await bus.InvokeAsync<IReadOnlyList<StrategyAutopsyDto>>(
            new GetAutopsyHistoryQuery(strategyId));
    }

    private static async Task<DiscoverRulesResultDto> DiscoverRules(
        [FromBody] DiscoverRulesCommand command, IMessageBus bus)
    {
        return await bus.InvokeAsync<DiscoverRulesResultDto>(command);
    }

    private static async Task<IReadOnlyList<DiscoverRulesResultDto>> GetDiscoveredRules(
        [FromRoute] Guid strategyId, IMessageBus bus)
    {
        return await bus.InvokeAsync<IReadOnlyList<DiscoverRulesResultDto>>(
            new GetDiscoveredRulesQuery(strategyId));
    }
}
