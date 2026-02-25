using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.Trading;

public class Portfolio : BaseEntity
{
    public Guid AccountId { get; set; }
    public decimal TotalValue { get; set; }
    public decimal CashBalance { get; set; }
    public decimal InvestedValue { get; set; }
    public decimal TotalPnL { get; set; }
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

    public Account Account { get; set; } = null!;
}
