using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.Trading;

public class TradeNote : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? PositionId { get; set; }
    public string Content { get; set; } = string.Empty;
}
