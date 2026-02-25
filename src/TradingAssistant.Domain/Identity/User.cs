using TradingAssistant.Domain.Trading;
using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.Identity;

public class User : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "User";

    public ICollection<Account> Accounts { get; set; } = new List<Account>();
}
