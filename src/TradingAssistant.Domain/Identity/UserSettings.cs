using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.Identity;

public class UserSettings : BaseEntity
{
    public Guid UserId { get; set; }
    public string DefaultCurrency { get; set; } = "GBP";
    public decimal DefaultInitialCapital { get; set; } = 1000m;
    public string CostProfileMarket { get; set; } = "UK";
    public string? BrokerSettingsJson { get; set; }

    public User User { get; set; } = null!;
}
