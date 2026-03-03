using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.Intelligence;

/// <summary>
/// Stored monthly performance attribution decomposing strategy returns
/// into alpha, beta, regime, and residual components.
/// </summary>
public class MonthlyAttribution : BaseEntity
{
    public string MarketCode { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }

    /// <summary>Total strategy return for the month (sum of trade PnL%).</summary>
    public decimal TotalReturn { get; set; }

    /// <summary>Alpha: strategy edge (skill component).</summary>
    public decimal Alpha { get; set; }

    /// <summary>Beta contribution: return from market exposure (beta × benchmark return).</summary>
    public decimal BetaContribution { get; set; }

    /// <summary>Regime contribution: return from regime positioning.</summary>
    public decimal RegimeContribution { get; set; }

    /// <summary>Residual: unexplained noise.</summary>
    public decimal Residual { get; set; }

    /// <summary>Computed beta for the period.</summary>
    public decimal Beta { get; set; }

    /// <summary>Benchmark return for the month.</summary>
    public decimal BenchmarkReturn { get; set; }

    public int TradeCount { get; set; }
    public int RegimeAlignedTrades { get; set; }
    public int RegimeMismatchedTrades { get; set; }

    public DateTime ComputedAt { get; set; } = DateTime.UtcNow;
}
