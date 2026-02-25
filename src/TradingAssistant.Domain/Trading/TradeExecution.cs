using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.Trading;

public class TradeExecution : BaseEntity
{
    public Guid OrderId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Fee { get; set; }
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;

    public Order Order { get; set; } = null!;
}
