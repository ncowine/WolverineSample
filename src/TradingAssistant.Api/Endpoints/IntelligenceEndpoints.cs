using Microsoft.AspNetCore.Mvc;
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
}
