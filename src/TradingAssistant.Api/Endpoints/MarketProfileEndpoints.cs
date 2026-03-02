using Microsoft.AspNetCore.Mvc;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using Wolverine;
using Wolverine.Http;

namespace TradingAssistant.Api.Endpoints;

public class MarketProfileEndpoints : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/market-profiles")
            .WithTags("Market Profiles")
            .RequireAuthorization();

        group.MapPostToWolverine<CreateMarketProfileCommand, MarketProfileDto>("/")
            .WithSummary("Create a new market profile");

        group.MapGet("/", GetProfiles)
            .WithSummary("Get all market profiles");

        group.MapGet("/{marketCode}", GetProfile)
            .WithSummary("Get a market profile by market code");

        group.MapPut("/{profileId}", UpdateProfile)
            .WithSummary("Update a market profile");
    }

    private static async Task<List<MarketProfileDto>> GetProfiles(IMessageBus bus)
    {
        return await bus.InvokeAsync<List<MarketProfileDto>>(new GetMarketProfilesQuery());
    }

    private static async Task<MarketProfileDto> GetProfile(
        [FromRoute] string marketCode, IMessageBus bus)
    {
        return await bus.InvokeAsync<MarketProfileDto>(new GetMarketProfileQuery(marketCode));
    }

    private static async Task<MarketProfileDto> UpdateProfile(
        [FromRoute] Guid profileId, [FromBody] UpdateMarketProfileCommand command, IMessageBus bus)
    {
        return await bus.InvokeAsync<MarketProfileDto>(
            command with { ProfileId = profileId });
    }
}

public class CostProfileEndpoints : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cost-profiles")
            .WithTags("Cost Profiles")
            .RequireAuthorization();

        group.MapPostToWolverine<CreateCostProfileCommand, CostProfileDto>("/")
            .WithSummary("Create a new cost profile");

        group.MapGet("/", GetProfiles)
            .WithSummary("Get cost profiles, optionally filtered by market code");

        group.MapPut("/{profileId}", UpdateProfile)
            .WithSummary("Update a cost profile");
    }

    private static async Task<List<CostProfileDto>> GetProfiles(
        [FromQuery] string? marketCode, IMessageBus bus)
    {
        return await bus.InvokeAsync<List<CostProfileDto>>(new GetCostProfilesQuery(marketCode));
    }

    private static async Task<CostProfileDto> UpdateProfile(
        [FromRoute] Guid profileId, [FromBody] UpdateCostProfileCommand command, IMessageBus bus)
    {
        return await bus.InvokeAsync<CostProfileDto>(
            command with { ProfileId = profileId });
    }
}
