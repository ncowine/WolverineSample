using Microsoft.AspNetCore.Authorization;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using Wolverine;
using Wolverine.Http;

namespace TradingAssistant.Api.Endpoints;

public static class AuthEndpoints
{
    [AllowAnonymous]
    [WolverinePost("/api/auth/register")]
    public static async Task<AuthResponseDto> Register(RegisterUserCommand command, IMessageBus bus)
    {
        return await bus.InvokeAsync<AuthResponseDto>(command);
    }

    [AllowAnonymous]
    [WolverinePost("/api/auth/login")]
    public static async Task<LoginResponseDto> Login(LoginUserCommand command, IMessageBus bus)
    {
        return await bus.InvokeAsync<LoginResponseDto>(command);
    }
}
