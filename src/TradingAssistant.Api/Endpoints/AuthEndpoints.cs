using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using Wolverine.Http;

namespace TradingAssistant.Api.Endpoints;

public class AuthEndpoints : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Auth")
            .AllowAnonymous();

        group.MapPostToWolverine<RegisterUserCommand, AuthResponseDto>("/register")
            .WithSummary("Register a new user");

        group.MapPostToWolverine<LoginUserCommand, LoginResponseDto>("/login")
            .WithSummary("Login and receive a JWT token");
    }
}
