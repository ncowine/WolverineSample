using System.Collections.Concurrent;

namespace TradingAssistant.Application.Backtesting;

/// <summary>
/// In-memory progress tracking for auto-optimize operations.
/// Keyed by backtest run ID. Entries auto-expire after 10 minutes.
/// </summary>
public static class AutoOptimizeProgressStore
{
    private static readonly ConcurrentDictionary<Guid, ProgressEntry> Store = new();

    /// <summary>
    /// Static field for the active auto-optimize run ID.
    /// Used by RunOptimizationHandler to report walk-forward progress.
    /// Thread-safe via Interlocked on the backing string representation.
    /// </summary>
    private static string? _activeRunId;

    public static Guid? ActiveRunId
    {
        get
        {
            var val = Volatile.Read(ref _activeRunId);
            return val is not null && Guid.TryParse(val, out var g) ? g : null;
        }
        set => Volatile.Write(ref _activeRunId, value?.ToString());
    }

    public static void Update(Guid runId, string step, int windowIndex, int totalWindows,
        long completedCombos, long totalCombos)
    {
        Store[runId] = new ProgressEntry
        {
            Step = step,
            WindowIndex = windowIndex,
            TotalWindows = totalWindows,
            CompletedCombos = completedCombos,
            TotalCombos = totalCombos,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    public static ProgressEntry? Get(Guid runId)
    {
        if (Store.TryGetValue(runId, out var entry))
        {
            if ((DateTime.UtcNow - entry.UpdatedAt).TotalMinutes > 10)
            {
                Store.TryRemove(runId, out _);
                return null;
            }
            return entry;
        }
        return null;
    }

    public static void Remove(Guid runId) => Store.TryRemove(runId, out _);

    public class ProgressEntry
    {
        public string Step { get; init; } = "";
        public int WindowIndex { get; init; }
        public int TotalWindows { get; init; }
        public long CompletedCombos { get; init; }
        public long TotalCombos { get; init; }
        public DateTime UpdatedAt { get; init; }
    }
}
