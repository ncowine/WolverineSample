using TradingAssistant.Domain.Enums;
using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.Trading;

public class Order : BaseEntity
{
    public Guid AccountId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public OrderSide Side { get; set; }
    public OrderType Type { get; set; }
    public decimal Quantity { get; set; }
    public decimal? Price { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public DateTime? FilledAt { get; set; }

    public Account Account { get; set; } = null!;
    public ICollection<TradeExecution> TradeExecutions { get; set; } = new List<TradeExecution>();
}
