using Microsoft.Extensions.Logging.Abstractions;
using TradingAssistant.Application.Handlers.Intelligence;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.Tests.Helpers;

namespace TradingAssistant.Tests.Intelligence;

public class MistakeTaxonomyTests
{
    // ──────────────────────────────────────────────────────────────────
    // MistakeClassifier — heuristic classification tests
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Classify_RegimeMismatch_WhenRegimeChangedDuringTrade()
    {
        var context = new MistakeClassifier.TradeContext(
            PnlPercent: -2.5m, EntryPrice: 100m, ExitPrice: 97.5m,
            RegimeAtEntry: "Bull", RegimeAtExit: "Bear",
            OutcomeClass: OutcomeClass.BadEntry,
            AtrAtEntry: null, AverageLossPercent: -1.5m);

        var result = MistakeClassifier.Classify(context);

        Assert.Equal(MistakeType.RegimeMismatch, result);
    }

    [Fact]
    public void Classify_StopTooTight_WhenStoppedWithinOneAtr()
    {
        var context = new MistakeClassifier.TradeContext(
            PnlPercent: -0.8m, EntryPrice: 100m, ExitPrice: 99.2m,
            RegimeAtEntry: "Bull", RegimeAtExit: "Bull",
            OutcomeClass: OutcomeClass.StoppedPrematurely,
            AtrAtEntry: 1.0m, AverageLossPercent: -1.5m);

        var result = MistakeClassifier.Classify(context);

        Assert.Equal(MistakeType.StopTooTight, result);
    }

    [Fact]
    public void Classify_StopTooTight_RequiresStoppedPrematurelyOutcome()
    {
        // Same ATR condition but outcome is BadEntry — should NOT classify as StopTooTight
        var context = new MistakeClassifier.TradeContext(
            PnlPercent: -0.8m, EntryPrice: 100m, ExitPrice: 99.2m,
            RegimeAtEntry: "Bull", RegimeAtExit: "Bull",
            OutcomeClass: OutcomeClass.BadEntry,
            AtrAtEntry: 1.0m, AverageLossPercent: -1.5m);

        var result = MistakeClassifier.Classify(context);

        Assert.NotEqual(MistakeType.StopTooTight, result);
    }

    [Fact]
    public void Classify_StopTooTight_RequiresAtrData()
    {
        var context = new MistakeClassifier.TradeContext(
            PnlPercent: -0.8m, EntryPrice: 100m, ExitPrice: 99.2m,
            RegimeAtEntry: "Bull", RegimeAtExit: "Bull",
            OutcomeClass: OutcomeClass.StoppedPrematurely,
            AtrAtEntry: null, AverageLossPercent: -1.5m);

        var result = MistakeClassifier.Classify(context);

        // Without ATR data, should fall through to outcome-based classification
        Assert.NotEqual(MistakeType.StopTooTight, result);
    }

    [Fact]
    public void Classify_OversizedPosition_WhenLossExceedsTwoTimesAverage()
    {
        var context = new MistakeClassifier.TradeContext(
            PnlPercent: -5.0m, EntryPrice: 100m, ExitPrice: 95m,
            RegimeAtEntry: "Bull", RegimeAtExit: "Bull",
            OutcomeClass: OutcomeClass.BadEntry,
            AtrAtEntry: null, AverageLossPercent: -2.0m);

        var result = MistakeClassifier.Classify(context);

        Assert.Equal(MistakeType.OversizedPosition, result);
    }

    [Fact]
    public void Classify_OversizedPosition_NotTriggeredWhenLossIsNormal()
    {
        var context = new MistakeClassifier.TradeContext(
            PnlPercent: -1.5m, EntryPrice: 100m, ExitPrice: 98.5m,
            RegimeAtEntry: "Bull", RegimeAtExit: "Bull",
            OutcomeClass: OutcomeClass.BadEntry,
            AtrAtEntry: null, AverageLossPercent: -2.0m);

        var result = MistakeClassifier.Classify(context);

        Assert.NotEqual(MistakeType.OversizedPosition, result);
    }

    [Fact]
    public void Classify_StopTooLoose_WhenStoppedCorrectlyWithLargeLoss()
    {
        var context = new MistakeClassifier.TradeContext(
            PnlPercent: -4.0m, EntryPrice: 100m, ExitPrice: 96m,
            RegimeAtEntry: "Bull", RegimeAtExit: "Bull",
            OutcomeClass: OutcomeClass.StoppedCorrectly,
            AtrAtEntry: null, AverageLossPercent: -2.0m);

        var result = MistakeClassifier.Classify(context);

        Assert.Equal(MistakeType.StopTooLoose, result);
    }

    [Fact]
    public void Classify_BadTiming_WhenGoodEntryBadExit()
    {
        var context = new MistakeClassifier.TradeContext(
            PnlPercent: -1.0m, EntryPrice: 100m, ExitPrice: 99m,
            RegimeAtEntry: "Bull", RegimeAtExit: "Bull",
            OutcomeClass: OutcomeClass.GoodEntryBadExit,
            AtrAtEntry: null, AverageLossPercent: -1.5m);

        var result = MistakeClassifier.Classify(context);

        Assert.Equal(MistakeType.BadTiming, result);
    }

    [Fact]
    public void Classify_BadSignal_WhenBadEntry()
    {
        var context = new MistakeClassifier.TradeContext(
            PnlPercent: -1.0m, EntryPrice: 100m, ExitPrice: 99m,
            RegimeAtEntry: "Bull", RegimeAtExit: "Bull",
            OutcomeClass: OutcomeClass.BadEntry,
            AtrAtEntry: null, AverageLossPercent: -1.5m);

        var result = MistakeClassifier.Classify(context);

        Assert.Equal(MistakeType.BadSignal, result);
    }

    [Fact]
    public void Classify_BadSignal_DefaultFallback()
    {
        var context = new MistakeClassifier.TradeContext(
            PnlPercent: -1.0m, EntryPrice: 100m, ExitPrice: 99m,
            RegimeAtEntry: "Bull", RegimeAtExit: "Bull",
            OutcomeClass: OutcomeClass.GoodEntryGoodExit,
            AtrAtEntry: null, AverageLossPercent: -1.5m);

        var result = MistakeClassifier.Classify(context);

        Assert.Equal(MistakeType.BadSignal, result);
    }

    [Fact]
    public void Classify_RegimeMismatchTakesPriority_OverOtherRules()
    {
        // Regime changed AND loss is oversized — RegimeMismatch should win
        var context = new MistakeClassifier.TradeContext(
            PnlPercent: -6.0m, EntryPrice: 100m, ExitPrice: 94m,
            RegimeAtEntry: "Bull", RegimeAtExit: "Bear",
            OutcomeClass: OutcomeClass.StoppedPrematurely,
            AtrAtEntry: 6.5m, AverageLossPercent: -2.0m);

        var result = MistakeClassifier.Classify(context);

        Assert.Equal(MistakeType.RegimeMismatch, result);
    }

    [Fact]
    public void IsRegimeMismatch_FalseWhenBothEmpty()
    {
        var context = new MistakeClassifier.TradeContext(
            PnlPercent: -1m, EntryPrice: 100m, ExitPrice: 99m,
            RegimeAtEntry: "", RegimeAtExit: "",
            OutcomeClass: OutcomeClass.BadEntry,
            AtrAtEntry: null, AverageLossPercent: -1m);

        Assert.False(MistakeClassifier.IsRegimeMismatch(context));
    }

    [Fact]
    public void IsRegimeMismatch_CaseInsensitive()
    {
        var context = new MistakeClassifier.TradeContext(
            PnlPercent: -1m, EntryPrice: 100m, ExitPrice: 99m,
            RegimeAtEntry: "BULL", RegimeAtExit: "bull",
            OutcomeClass: OutcomeClass.BadEntry,
            AtrAtEntry: null, AverageLossPercent: -1m);

        Assert.False(MistakeClassifier.IsRegimeMismatch(context));
    }

    // ──────────────────────────────────────────────────────────────────
    // MistakePatternAnalyzer — aggregation tests
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Analyze_EmptyReviews_ReturnsEmptySummary()
    {
        var result = MistakePatternAnalyzer.Analyze("US_SP500", []);

        Assert.Equal("US_SP500", result.MarketCode);
        Assert.Equal(0, result.TotalTrades);
        Assert.Equal(0, result.LosingTrades);
        Assert.Null(result.MostCommonMistake);
        Assert.Empty(result.MistakeBreakdown);
    }

    [Fact]
    public void Analyze_MistakeBreakdown_CountsCorrectly()
    {
        var reviews = new List<TradeReview>
        {
            MakeReview(-1m, MistakeType.BadSignal, "Bull"),
            MakeReview(-2m, MistakeType.BadSignal, "Bull"),
            MakeReview(-1m, MistakeType.RegimeMismatch, "Bear"),
            MakeReview(3m, null, "Bull"), // winner — no mistake
        };

        var result = MistakePatternAnalyzer.Analyze("US_SP500", reviews);

        Assert.Equal(4, result.TotalTrades);
        Assert.Equal(3, result.LosingTrades);
        Assert.Equal("BadSignal", result.MostCommonMistake);
        Assert.Equal(2, result.MistakeBreakdown["BadSignal"]);
        Assert.Equal(1, result.MistakeBreakdown["RegimeMismatch"]);
    }

    [Fact]
    public void Analyze_RegimeBreakdown_GroupsByRegime()
    {
        var reviews = new List<TradeReview>
        {
            MakeReview(-1m, MistakeType.BadSignal, "Bull"),
            MakeReview(-2m, MistakeType.StopTooTight, "Bull"),
            MakeReview(-1m, MistakeType.BadSignal, "Bear"),
        };

        var result = MistakePatternAnalyzer.Analyze("US_SP500", reviews);

        Assert.Equal(2, result.RegimeBreakdown.Count);
        Assert.Equal(1, result.RegimeBreakdown["Bull"]["BadSignal"]);
        Assert.Equal(1, result.RegimeBreakdown["Bull"]["StopTooTight"]);
        Assert.Equal(1, result.RegimeBreakdown["Bear"]["BadSignal"]);
    }

    [Fact]
    public void Analyze_Recommendations_GeneratedForTopMistake()
    {
        var reviews = new List<TradeReview>
        {
            MakeReview(-1m, MistakeType.StopTooTight, "Bull"),
            MakeReview(-2m, MistakeType.StopTooTight, "Bull"),
            MakeReview(-1m, MistakeType.BadSignal, "Bear"),
        };

        var result = MistakePatternAnalyzer.Analyze("US_SP500", reviews);

        Assert.NotEmpty(result.Recommendations);
        Assert.Contains(result.Recommendations, r => r.Contains("StopTooTight"));
    }

    [Fact]
    public void Analyze_HighLossRate_TriggersWarningRecommendation()
    {
        // 8 losing out of 10 = 80% loss rate
        var reviews = new List<TradeReview>();
        for (int i = 0; i < 8; i++)
            reviews.Add(MakeReview(-1m, MistakeType.BadSignal, "Bull"));
        for (int i = 0; i < 2; i++)
            reviews.Add(MakeReview(1m, null, "Bull"));

        var result = MistakePatternAnalyzer.Analyze("US_SP500", reviews);

        Assert.Contains(result.Recommendations, r => r.Contains("loss rate"));
    }

    [Fact]
    public void GenerateRecommendations_RegimeSpecific_WhenEnoughOccurrences()
    {
        var reviews = new List<TradeReview>
        {
            MakeReview(-1m, MistakeType.RegimeMismatch, "HighVol"),
            MakeReview(-2m, MistakeType.RegimeMismatch, "HighVol"),
            MakeReview(-1m, MistakeType.RegimeMismatch, "HighVol"),
        };

        var result = MistakePatternAnalyzer.Analyze("US_SP500", reviews);

        Assert.Contains(result.Recommendations, r => r.Contains("HighVol"));
    }

    // ──────────────────────────────────────────────────────────────────
    // GetMistakeSummaryHandler — endpoint handler tests
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMistakeSummary_ReturnsBreakdownForMarket()
    {
        var db = TestIntelligenceDbContextFactory.Create();

        db.TradeReviews.Add(MakeReview(-1m, MistakeType.BadSignal, "Bull", "US_SP500"));
        db.TradeReviews.Add(MakeReview(-2m, MistakeType.StopTooTight, "Bull", "US_SP500"));
        db.TradeReviews.Add(MakeReview(3m, null, "Bull", "US_SP500"));
        db.TradeReviews.Add(MakeReview(-1m, MistakeType.BadSignal, "Bull", "IN_NIFTY50")); // different market
        await db.SaveChangesAsync();

        var result = await GetMistakeSummaryHandler.HandleAsync(
            new GetMistakeSummaryQuery("US_SP500"), db);

        Assert.Equal("US_SP500", result.MarketCode);
        Assert.Equal(3, result.TotalTrades);
        Assert.Equal(2, result.LosingTrades);
        Assert.Equal(2, result.MistakeBreakdown.Count);
    }

    [Fact]
    public async Task GetMistakeSummary_IncludesLastReportDate()
    {
        var db = TestIntelligenceDbContextFactory.Create();

        var report = new MistakePatternReport
        {
            MarketCode = "US_SP500",
            TradeCount = 50,
            LosingTradeCount = 20,
            MostCommonMistake = "BadSignal",
            AnalyzedAt = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc)
        };
        db.MistakePatternReports.Add(report);

        // Add a review after the report
        var review = MakeReview(-1m, MistakeType.BadSignal, "Bull", "US_SP500");
        review.ReviewedAt = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        db.TradeReviews.Add(review);
        await db.SaveChangesAsync();

        var result = await GetMistakeSummaryHandler.HandleAsync(
            new GetMistakeSummaryQuery("US_SP500"), db);

        Assert.NotNull(result.LastReportDate);
        Assert.Equal(1, result.TradesSinceLastReport);
    }

    [Fact]
    public async Task GetMistakeSummary_NoReviews_ReturnsEmptySummary()
    {
        var db = TestIntelligenceDbContextFactory.Create();

        var result = await GetMistakeSummaryHandler.HandleAsync(
            new GetMistakeSummaryQuery("US_SP500"), db);

        Assert.Equal(0, result.TotalTrades);
        Assert.Null(result.MostCommonMistake);
        Assert.Empty(result.MistakeBreakdown);
    }

    // ──────────────────────────────────────────────────────────────────
    // GeneratePatternReportHandler — report generation tests
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GeneratePatternReport_SavesReportWithAnalysis()
    {
        var db = TestIntelligenceDbContextFactory.Create();
        var claude = new FakeClaudeClient();
        claude.SetDefaultResponse("The dominant pattern is bad signal entries during bull markets.");

        db.TradeReviews.Add(MakeReview(-1m, MistakeType.BadSignal, "Bull", "US_SP500"));
        db.TradeReviews.Add(MakeReview(-2m, MistakeType.BadSignal, "Bull", "US_SP500"));
        db.TradeReviews.Add(MakeReview(-1m, MistakeType.StopTooTight, "Bear", "US_SP500"));
        await db.SaveChangesAsync();

        var result = await GeneratePatternReportHandler.HandleAsync(
            new GeneratePatternReportCommand("US_SP500"),
            claude, db, NullLogger<GeneratePatternReportHandler>.Instance);

        Assert.Equal("US_SP500", result.MarketCode);
        Assert.Equal(3, result.TradeCount);
        Assert.Equal(3, result.LosingTradeCount);
        Assert.Equal("BadSignal", result.MostCommonMistake);
        Assert.NotNull(result.ClaudeAnalysis);
        Assert.True(claude.CallCount > 0);

        // Verify saved to DB
        var saved = await db.MistakePatternReports.FindAsync(result.Id);
        Assert.NotNull(saved);
        Assert.Equal("US_SP500", saved.MarketCode);
    }

    [Fact]
    public async Task GeneratePatternReport_RateLimited_SkipsClaude()
    {
        var db = TestIntelligenceDbContextFactory.Create();
        var claude = new FakeClaudeClient(maxDailyCalls: 0); // already exhausted

        db.TradeReviews.Add(MakeReview(-1m, MistakeType.BadSignal, "Bull", "US_SP500"));
        await db.SaveChangesAsync();

        var result = await GeneratePatternReportHandler.HandleAsync(
            new GeneratePatternReportCommand("US_SP500"),
            claude, db, NullLogger<GeneratePatternReportHandler>.Instance);

        Assert.Null(result.ClaudeAnalysis);
        Assert.Equal(0, claude.CallCount);
    }

    [Fact]
    public async Task GeneratePatternReport_NoLosingTrades_SkipsClaude()
    {
        var db = TestIntelligenceDbContextFactory.Create();
        var claude = new FakeClaudeClient();

        db.TradeReviews.Add(MakeReview(3m, null, "Bull", "US_SP500")); // winner
        await db.SaveChangesAsync();

        var result = await GeneratePatternReportHandler.HandleAsync(
            new GeneratePatternReportCommand("US_SP500"),
            claude, db, NullLogger<GeneratePatternReportHandler>.Instance);

        Assert.Null(result.ClaudeAnalysis);
        Assert.Equal(0, claude.CallCount);
        Assert.Equal("None", result.MostCommonMistake);
    }

    // ──────────────────────────────────────────────────────────────────
    // Helper
    // ──────────────────────────────────────────────────────────────────

    private static TradeReview MakeReview(
        decimal pnlPercent,
        MistakeType? mistakeType,
        string regime,
        string marketCode = "US_SP500")
    {
        return new TradeReview
        {
            TradeId = Guid.NewGuid(),
            Symbol = "AAPL",
            MarketCode = marketCode,
            StrategyName = "TestStrategy",
            EntryPrice = 100m,
            ExitPrice = 100m + pnlPercent,
            EntryDate = DateTime.UtcNow.AddDays(-5),
            ExitDate = DateTime.UtcNow,
            PnlPercent = pnlPercent,
            PnlAbsolute = pnlPercent * 10,
            DurationHours = 48,
            RegimeAtEntry = regime,
            RegimeAtExit = regime,
            OutcomeClass = pnlPercent >= 0 ? OutcomeClass.GoodEntryGoodExit : OutcomeClass.BadEntry,
            MistakeType = mistakeType,
            Score = 5,
            Summary = "Test review",
            ReviewedAt = DateTime.UtcNow
        };
    }
}
