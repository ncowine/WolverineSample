using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.Intelligence;

/// <summary>
/// AI-generated review of a closed trade, produced by Claude.
/// Captures comprehensive trade context and qualitative analysis.
/// </summary>
public class TradeReview : BaseEntity
{
    /// <summary>Cross-DB reference to TradingDb.Order or Position.</summary>
    public Guid TradeId { get; set; }

    public string Symbol { get; set; } = string.Empty;
    public string MarketCode { get; set; } = string.Empty;

    /// <summary>Strategy name used for the trade.</summary>
    public string StrategyName { get; set; } = string.Empty;

    // --- Trade Data ---
    public decimal EntryPrice { get; set; }
    public decimal ExitPrice { get; set; }
    public DateTime EntryDate { get; set; }
    public DateTime ExitDate { get; set; }
    public decimal PnlPercent { get; set; }
    public decimal PnlAbsolute { get; set; }

    /// <summary>Trade duration in hours.</summary>
    public double DurationHours { get; set; }

    // --- Context at Entry ---
    public string RegimeAtEntry { get; set; } = string.Empty;
    public string RegimeAtExit { get; set; } = string.Empty;

    /// <summary>Confidence grade at entry (0-100).</summary>
    public decimal? Grade { get; set; }

    /// <summary>ML model confidence at entry (0-1).</summary>
    public float? MlConfidence { get; set; }

    /// <summary>JSON dictionary of indicator values at entry.</summary>
    public string IndicatorValuesJson { get; set; } = "{}";

    // --- Claude Analysis ---
    public OutcomeClass OutcomeClass { get; set; }
    public MistakeType? MistakeType { get; set; }

    /// <summary>Claude's execution quality score (1-10).</summary>
    public int Score { get; set; }

    /// <summary>JSON array of strength strings.</summary>
    public string StrengthsJson { get; set; } = "[]";

    /// <summary>JSON array of weakness strings.</summary>
    public string WeaknessesJson { get; set; } = "[]";

    /// <summary>JSON array of lessons learned.</summary>
    public string LessonsLearnedJson { get; set; } = "[]";

    /// <summary>Human-readable summary from Claude.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>When the review was performed.</summary>
    public DateTime ReviewedAt { get; set; } = DateTime.UtcNow;
}
