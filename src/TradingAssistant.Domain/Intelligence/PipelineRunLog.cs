using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.Intelligence;

public class PipelineRunLog : BaseEntity
{
    public string MarketCode { get; set; } = string.Empty;
    public DateTime RunDate { get; set; }
    public string StepName { get; set; } = string.Empty;
    public int StepOrder { get; set; }
    public PipelineStepStatus Status { get; set; } = PipelineStepStatus.Pending;
    public TimeSpan Duration { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
}
