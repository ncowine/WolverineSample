using TradingAssistant.Application.Screening;

namespace TradingAssistant.Tests.Screening;

public class GradeHistoryTrackerTests
{
    [Fact]
    public void Record_and_retrieve_entries()
    {
        var tracker = new GradeHistoryTracker();
        tracker.Record(new GradeHistoryEntry
        {
            Symbol = "AAPL", SignalDate = new DateTime(2025, 6, 15),
            Direction = SignalDirection.Long, Grade = SignalGrade.A,
            Score = 92m, EntryPrice = 150m, StopPrice = 145m, TargetPrice = 165m
        });

        Assert.Equal(1, tracker.Count);
        Assert.Equal("AAPL", tracker.GetEntries()[0].Symbol);
    }

    [Fact]
    public void Resolve_outcome()
    {
        var tracker = new GradeHistoryTracker();
        var date = new DateTime(2025, 6, 15);
        tracker.Record(new GradeHistoryEntry
        {
            Symbol = "AAPL", SignalDate = date,
            Direction = SignalDirection.Long, Grade = SignalGrade.A, Score = 92m,
            EntryPrice = 150m, StopPrice = 145m, TargetPrice = 165m
        });

        tracker.ResolveOutcome("AAPL", date, wasProfitable: true, pnlPercent: 10m);

        var entry = tracker.GetEntries().Single();
        Assert.True(entry.WasProfitable);
        Assert.Equal(10m, entry.ActualPnLPercent);
    }

    [Fact]
    public void Resolve_nonexistent_does_nothing()
    {
        var tracker = new GradeHistoryTracker();
        tracker.ResolveOutcome("AAPL", DateTime.Today, true, 5m); // no entries → no-op
        Assert.Equal(0, tracker.Count);
    }

    [Fact]
    public void Accuracy_summary_by_grade()
    {
        var tracker = new GradeHistoryTracker();

        // 3 A-grade signals: 2 profitable, 1 unprofitable
        for (var i = 0; i < 3; i++)
        {
            var date = new DateTime(2025, 6, 10 + i);
            tracker.Record(new GradeHistoryEntry
            {
                Symbol = "AAPL", SignalDate = date,
                Direction = SignalDirection.Long, Grade = SignalGrade.A, Score = 92m,
                EntryPrice = 150m, StopPrice = 145m, TargetPrice = 165m
            });
            tracker.ResolveOutcome("AAPL", date, wasProfitable: i < 2, pnlPercent: i < 2 ? 10m : -3m);
        }

        // 2 B-grade signals: 1 profitable, 1 unresolved
        tracker.Record(new GradeHistoryEntry
        {
            Symbol = "MSFT", SignalDate = new DateTime(2025, 6, 20),
            Direction = SignalDirection.Long, Grade = SignalGrade.B, Score = 80m,
            EntryPrice = 400m, StopPrice = 390m, TargetPrice = 420m
        });
        tracker.ResolveOutcome("MSFT", new DateTime(2025, 6, 20), true, 5m);

        tracker.Record(new GradeHistoryEntry
        {
            Symbol = "GOOG", SignalDate = new DateTime(2025, 6, 21),
            Direction = SignalDirection.Long, Grade = SignalGrade.B, Score = 78m,
            EntryPrice = 170m, StopPrice = 165m, TargetPrice = 180m
        });
        // Not resolved

        var summary = tracker.GetAccuracySummary();

        var aGrade = summary.Single(s => s.Grade == SignalGrade.A);
        Assert.Equal(3, aGrade.TotalSignals);
        Assert.Equal(2, aGrade.ProfitableSignals);
        Assert.Equal(1, aGrade.UnprofitableSignals);
        Assert.Equal(0, aGrade.UnresolvedSignals);
        Assert.NotNull(aGrade.WinRate);
        Assert.Equal(200m / 3m, aGrade.WinRate!.Value, 1); // 66.7%

        var bGrade = summary.Single(s => s.Grade == SignalGrade.B);
        Assert.Equal(2, bGrade.TotalSignals);
        Assert.Equal(1, bGrade.ProfitableSignals);
        Assert.Equal(0, bGrade.UnprofitableSignals);
        Assert.Equal(1, bGrade.UnresolvedSignals);
        Assert.Equal(100m, bGrade.WinRate); // 1/1 resolved = 100%
    }

    [Fact]
    public void Accuracy_summary_empty_tracker()
    {
        var tracker = new GradeHistoryTracker();
        var summary = tracker.GetAccuracySummary();
        Assert.Empty(summary);
    }

    [Fact]
    public void Win_rate_null_when_no_resolved_signals()
    {
        var tracker = new GradeHistoryTracker();
        tracker.Record(new GradeHistoryEntry
        {
            Symbol = "AAPL", SignalDate = DateTime.Today,
            Grade = SignalGrade.A, Score = 92m,
            EntryPrice = 150m, StopPrice = 145m, TargetPrice = 165m
        });

        var summary = tracker.GetAccuracySummary();
        Assert.Null(summary.Single().WinRate);
    }
}
