using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.Backtesting;

public class Strategy : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// JSON-serialized StrategyDefinition for the v2 engine.
    /// When populated, the new backtest engine uses this. When empty, legacy Rules are used.
    /// </summary>
    public string? RulesJson { get; set; }

    /// <summary>
    /// True if this strategy uses the v2 definition model (RulesJson populated).
    /// </summary>
    public bool UsesV2Engine => !string.IsNullOrWhiteSpace(RulesJson);

    // Legacy v1 rules (backward compatible)
    public ICollection<StrategyRule> Rules { get; set; } = new List<StrategyRule>();
    public ICollection<BacktestRun> BacktestRuns { get; set; } = new List<BacktestRun>();
}
