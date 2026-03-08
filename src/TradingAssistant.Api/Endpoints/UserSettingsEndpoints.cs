using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using Wolverine;

namespace TradingAssistant.Api.Endpoints;

public class UserSettingsEndpoints : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/user/settings")
            .WithTags("User Settings")
            .RequireAuthorization();

        group.MapGet("/", GetSettings)
            .WithSummary("Get current user settings (auto-creates defaults if none exist)");

        group.MapPut("/", UpdateSettings)
            .WithSummary("Update current user settings");
    }

    private static async Task<UserSettingsDto> GetSettings(IMessageBus bus)
    {
        return await bus.InvokeAsync<UserSettingsDto>(new GetUserSettingsQuery());
    }

    private static async Task<UserSettingsDto> UpdateSettings(
        UpdateUserSettingsCommand command, IMessageBus bus)
    {
        return await bus.InvokeAsync<UserSettingsDto>(command);
    }
}
