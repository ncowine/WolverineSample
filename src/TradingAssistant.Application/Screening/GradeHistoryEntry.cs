namespace TradingAssistant.Application.Screening;

/// <summary>
/// Tracks a graded signal's outcome for historical accuracy analysis.
/// </summary>
public class GradeHistoryEntry
{
    public string Symbol { get; init; } = string.Empty;
    public DateTime SignalDate { get; init; }
    public SignalDirection Direction { get; init; }
    public SignalGrade Grade { get; init; }
    public decimal Score { get; init; }
    public decimal EntryPrice { get; init; }
    public decimal StopPrice { get; init; }
    public decimal TargetPrice { get; init; }

    /// <summary>
    /// True if the trade hit its target before its stop (profitable).
    /// Null if outcome not yet determined.
    /// </summary>
    public bool? WasProfitable { get; set; }

    /// <summary>
    /// Actual P&amp;L percent if trade was taken. Null if not yet resolved.
    /// </summary>
    public decimal? ActualPnLPercent { get; set; }
}

/// <summary>
/// Summary statistics for grade accuracy tracking.
/// </summary>
public record GradeAccuracySummary
{
    public SignalGrade Grade { get; init; }
    public int TotalSignals { get; init; }
    public int ProfitableSignals { get; init; }
    public int UnprofitableSignals { get; init; }
    public int UnresolvedSignals { get; init; }

    /// <summary>
    /// Win rate for resolved signals (0-100). Null if no resolved signals.
    /// </summary>
    public decimal? WinRate => ProfitableSignals + UnprofitableSignals > 0
        ? (decimal)ProfitableSignals / (ProfitableSignals + UnprofitableSignals) * 100m
        : null;
}

/// <summary>
/// Tracks grade accuracy over time. Thread-safe for concurrent screener use.
/// </summary>
public class GradeHistoryTracker
{
    private readonly List<GradeHistoryEntry> _entries = new();
    private readonly object _lock = new();

    public void Record(GradeHistoryEntry entry)
    {
        lock (_lock) _entries.Add(entry);
    }

    public void ResolveOutcome(string symbol, DateTime signalDate, bool wasProfitable, decimal pnlPercent)
    {
        lock (_lock)
        {
            var entry = _entries.FirstOrDefault(e =>
                e.Symbol == symbol && e.SignalDate == signalDate);
            if (entry is not null)
            {
                entry.WasProfitable = wasProfitable;
                entry.ActualPnLPercent = pnlPercent;
            }
        }
    }

    public List<GradeAccuracySummary> GetAccuracySummary()
    {
        lock (_lock)
        {
            return _entries
                .GroupBy(e => e.Grade)
                .Select(g => new GradeAccuracySummary
                {
                    Grade = g.Key,
                    TotalSignals = g.Count(),
                    ProfitableSignals = g.Count(e => e.WasProfitable == true),
                    UnprofitableSignals = g.Count(e => e.WasProfitable == false),
                    UnresolvedSignals = g.Count(e => e.WasProfitable is null)
                })
                .OrderBy(s => s.Grade)
                .ToList();
        }
    }

    public IReadOnlyList<GradeHistoryEntry> GetEntries()
    {
        lock (_lock) return _entries.ToList();
    }

    public int Count
    {
        get { lock (_lock) return _entries.Count; }
    }
}
