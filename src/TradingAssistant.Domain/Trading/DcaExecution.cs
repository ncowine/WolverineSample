using TradingAssistant.Domain.Enums;
using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.Trading;

public class DcaExecution : BaseEntity
{
    public Guid DcaPlanId { get; set; }
    public Guid? OrderId { get; set; }
    public decimal Amount { get; set; }
    public decimal? ExecutedPrice { get; set; }
    public decimal? Quantity { get; set; }
    public DcaExecutionStatus Status { get; set; }
    public string? ErrorReason { get; set; }

    public DcaPlan DcaPlan { get; set; } = null!;
}
