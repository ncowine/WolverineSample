using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.Backtesting;

public class Strategy : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<StrategyRule> Rules { get; set; } = new List<StrategyRule>();
    public ICollection<BacktestRun> BacktestRuns { get; set; } = new List<BacktestRun>();
}
