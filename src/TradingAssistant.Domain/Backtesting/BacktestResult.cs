using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.Backtesting;

public class BacktestResult : BaseEntity
{
    public Guid BacktestRunId { get; set; }
    public int TotalTrades { get; set; }
    public decimal WinRate { get; set; }
    public decimal TotalReturn { get; set; }
    public decimal MaxDrawdown { get; set; }
    public decimal SharpeRatio { get; set; }
    public string ResultData { get; set; } = "{}"; // JSON for detailed trade-by-trade data

    public BacktestRun BacktestRun { get; set; } = null!;
}
