namespace TradingAssistant.Contracts.Commands;

/// <summary>
/// Trigger a Claude-powered post-mortem on a strategy's performance for a specific month.
/// Auto-triggers when monthly return &lt; 0, or can be invoked manually.
/// </summary>
public record RunAutopsyCommand(
    Guid StrategyId,
    int Month,
    int Year);
