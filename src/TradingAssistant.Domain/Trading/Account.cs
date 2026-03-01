using TradingAssistant.Domain.Enums;
using TradingAssistant.Domain.Identity;
using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.Trading;

public class Account : BaseEntity
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "USD";
    public AccountType AccountType { get; set; } = AccountType.Live;

    public User User { get; set; } = null!;
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<Position> Positions { get; set; } = new List<Position>();
    public ICollection<DcaPlan> DcaPlans { get; set; } = new List<DcaPlan>();
    public Portfolio? Portfolio { get; set; }
}
