using TradingAssistant.Domain.Enums;
using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.Backtesting;

public class BacktestRun : BaseEntity
{
    public Guid StrategyId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public BacktestRunStatus Status { get; set; } = BacktestRunStatus.Pending;

    public Strategy Strategy { get; set; } = null!;
    public BacktestResult? Result { get; set; }
}
