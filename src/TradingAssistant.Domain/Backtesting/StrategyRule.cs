using TradingAssistant.Domain.Enums;
using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.Backtesting;

public class StrategyRule : BaseEntity
{
    public Guid StrategyId { get; set; }
    public IndicatorType IndicatorType { get; set; }
    public string Condition { get; set; } = string.Empty; // e.g., "GreaterThan", "LessThan", "CrossAbove"
    public decimal Threshold { get; set; }
    public SignalType SignalType { get; set; }

    public Strategy Strategy { get; set; } = null!;
}
