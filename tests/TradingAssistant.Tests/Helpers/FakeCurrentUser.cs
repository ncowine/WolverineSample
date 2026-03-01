using TradingAssistant.Application.Services;

namespace TradingAssistant.Tests.Helpers;

public class FakeCurrentUser : ICurrentUser
{
    public Guid UserId { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "test@example.com";
    public string Role { get; set; } = "User";
    public bool IsAuthenticated { get; set; } = true;
}
