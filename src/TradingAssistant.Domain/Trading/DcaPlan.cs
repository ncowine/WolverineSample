using TradingAssistant.Domain.Enums;
using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.Trading;

public class DcaPlan : BaseEntity
{
    public Guid AccountId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DcaFrequency Frequency { get; set; }
    public DateTime NextExecutionDate { get; set; }
    public bool IsActive { get; set; } = true;

    public Account Account { get; set; } = null!;
    public ICollection<DcaExecution> Executions { get; set; } = new List<DcaExecution>();
}
