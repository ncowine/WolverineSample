using Microsoft.Extensions.Logging.Abstractions;
using TradingAssistant.Application.Handlers.Intelligence;
using TradingAssistant.Application.Intelligence.Prompts;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.Tests.Helpers;

namespace TradingAssistant.Tests.Intelligence;

public class TradeReviewTests
{
    private static string BuildValidReviewResponse(
        string outcomeClass = "GoodEntryGoodExit",
        string? mistakeType = null,
        int score = 8)
    {
        var mistakeJson = mistakeType is not null ? $"\"{mistakeType}\"" : "null";
        return $$"""
        {
          "outcomeClass": "{{outcomeClass}}",
          "mistakeType": {{mistakeJson}},
          "score": {{score}},
          "strengths": ["Good entry timing", "Proper position sizing"],
          "weaknesses": ["Exit could be improved"],
          "lessonsLearned": ["Wait for confirmation signals"],
          "summary": "Well-executed trade with room for exit improvement"
        }
        """;
    }

    #region ReviewTradeHandler Tests

    [Fact]
    public async Task HandleAsync_SuccessfulReview_SavesAndReturnsResult()
    {
        var db = TestIntelligenceDbContextFactory.Create();
        var claude = new FakeClaudeClient();
        claude.SetDefaultResponse(BuildValidReviewResponse());

        var command = new ReviewTradeCommand(
            TradeId: Guid.NewGuid(),
            Symbol: "AAPL",
            MarketCode: "US_SP500",
            StrategyName: "BullMomentum",
            EntryPrice: 180m,
            ExitPrice: 190m,
            EntryDate: new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc),
            ExitDate: new DateTime(2026, 1, 20, 14, 0, 0, DateTimeKind.Utc),
            PnlPercent: 5.56m,
            PnlAbsolute: 1000m,
            RegimeAtEntry: "Bull",
            RegimeAtExit: "Bull",
            Grade: 78.5m,
            MlConfidence: 0.72f);

        var result = await ReviewTradeHandler.HandleAsync(
            command, claude, db, NullLogger<ReviewTradeHandler>.Instance);

        Assert.True(result.Success);
        Assert.NotNull(result.ReviewId);
        Assert.Equal("GoodEntryGoodExit", result.OutcomeClass);
        Assert.Null(result.MistakeType); // Profitable → no mistake
        Assert.Equal(8, result.Score);
        Assert.Contains("exit improvement", result.Summary);

        // Verify saved to DB
        var saved = await db.TradeReviews.FindAsync(result.ReviewId);
        Assert.NotNull(saved);
        Assert.Equal("AAPL", saved!.Symbol);
        Assert.Equal("US_SP500", saved.MarketCode);
        Assert.Equal(OutcomeClass.GoodEntryGoodExit, saved.OutcomeClass);
        Assert.Null(saved.MistakeType);
        Assert.Equal(78.5m, saved.Grade);
        Assert.Equal(0.72f, saved.MlConfidence);
        Assert.True(saved.DurationHours > 0);
    }

    [Fact]
    public async Task HandleAsync_LosingTrade_ClassifiesMistakeType()
    {
        var db = TestIntelligenceDbContextFactory.Create();
        var claude = new FakeClaudeClient();
        claude.SetDefaultResponse(BuildValidReviewResponse(
            outcomeClass: "BadEntry",
            mistakeType: "BadTiming",
            score: 3));

        var command = new ReviewTradeCommand(
            TradeId: Guid.NewGuid(),
            Symbol: "TSLA",
            MarketCode: "US_SP500",
            StrategyName: "MeanReversion",
            EntryPrice: 200m,
            ExitPrice: 185m,
            EntryDate: new DateTime(2026, 2, 1, 9, 30, 0, DateTimeKind.Utc),
            ExitDate: new DateTime(2026, 2, 3, 15, 0, 0, DateTimeKind.Utc),
            PnlPercent: -7.5m,
            PnlAbsolute: -750m,
            RegimeAtEntry: "HighVol",
            RegimeAtExit: "Bear");

        var result = await ReviewTradeHandler.HandleAsync(
            command, claude, db, NullLogger<ReviewTradeHandler>.Instance);

        Assert.True(result.Success);
        Assert.Equal("BadEntry", result.OutcomeClass);
        Assert.Equal("BadTiming", result.MistakeType);
        Assert.Equal(3, result.Score);

        var saved = await db.TradeReviews.FindAsync(result.ReviewId);
        Assert.Equal(MistakeType.BadTiming, saved!.MistakeType);
        Assert.Equal(OutcomeClass.BadEntry, saved.OutcomeClass);
    }

    [Fact]
    public async Task HandleAsync_WinningTrade_NullMistakeType()
    {
        var db = TestIntelligenceDbContextFactory.Create();
        var claude = new FakeClaudeClient();
        // Even if Claude returns a mistake type, it's null for winners
        claude.SetDefaultResponse(BuildValidReviewResponse(
            outcomeClass: "GoodEntryGoodExit",
            mistakeType: "BadTiming",
            score: 7));

        var command = new ReviewTradeCommand(
            TradeId: Guid.NewGuid(),
            Symbol: "MSFT",
            MarketCode: "US_SP500",
            StrategyName: "Momentum",
            EntryPrice: 400m,
            ExitPrice: 420m,
            EntryDate: DateTime.UtcNow.AddDays(-5),
            ExitDate: DateTime.UtcNow,
            PnlPercent: 5m,
            PnlAbsolute: 500m,
            RegimeAtEntry: "Bull",
            RegimeAtExit: "Bull");

        var result = await ReviewTradeHandler.HandleAsync(
            command, claude, db, NullLogger<ReviewTradeHandler>.Instance);

        Assert.True(result.Success);
        Assert.Null(result.MistakeType); // PnlPercent >= 0 → no mistake
    }

    [Fact]
    public async Task HandleAsync_DuplicateTradeId_ReturnsError()
    {
        var db = TestIntelligenceDbContextFactory.Create();
        var claude = new FakeClaudeClient();
        var tradeId = Guid.NewGuid();

        // Pre-existing review
        db.TradeReviews.Add(new TradeReview
        {
            TradeId = tradeId,
            Symbol = "AAPL",
            MarketCode = "US_SP500",
            OutcomeClass = OutcomeClass.GoodEntryGoodExit,
            Score = 8,
            Summary = "Already reviewed"
        });
        await db.SaveChangesAsync();

        var command = new ReviewTradeCommand(
            TradeId: tradeId,
            Symbol: "AAPL",
            MarketCode: "US_SP500",
            StrategyName: "Test",
            EntryPrice: 100m,
            ExitPrice: 110m,
            EntryDate: DateTime.UtcNow.AddDays(-1),
            ExitDate: DateTime.UtcNow,
            PnlPercent: 10m,
            PnlAbsolute: 100m,
            RegimeAtEntry: "Bull",
            RegimeAtExit: "Bull");

        var result = await ReviewTradeHandler.HandleAsync(
            command, claude, db, NullLogger<ReviewTradeHandler>.Instance);

        Assert.False(result.Success);
        Assert.Contains("already been reviewed", result.Error);
        Assert.Equal(0, claude.CallCount);
    }

    [Fact]
    public async Task HandleAsync_RateLimited_ReturnsError()
    {
        var db = TestIntelligenceDbContextFactory.Create();
        var claude = new FakeClaudeClient(maxDailyCalls: 0);

        var command = new ReviewTradeCommand(
            TradeId: Guid.NewGuid(),
            Symbol: "AAPL",
            MarketCode: "US_SP500",
            StrategyName: "Test",
            EntryPrice: 100m,
            ExitPrice: 110m,
            EntryDate: DateTime.UtcNow.AddDays(-1),
            ExitDate: DateTime.UtcNow,
            PnlPercent: 10m,
            PnlAbsolute: 100m,
            RegimeAtEntry: "Bull",
            RegimeAtExit: "Bull");

        var result = await ReviewTradeHandler.HandleAsync(
            command, claude, db, NullLogger<ReviewTradeHandler>.Instance);

        Assert.False(result.Success);
        Assert.Contains("rate limit", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_ClaudeApiFails_ReturnsError()
    {
        var db = TestIntelligenceDbContextFactory.Create();
        var claude = new FakeClaudeClient(maxDailyCalls: 1);
        // Configure Claude to return failure
        claude.SetDefaultResponse("not valid json that won't parse as success");

        // Override: make the client return an error instead
        var errorClaude = new FakeClaudeClient(maxDailyCalls: 0);

        var command = new ReviewTradeCommand(
            TradeId: Guid.NewGuid(),
            Symbol: "AAPL",
            MarketCode: "US_SP500",
            StrategyName: "Test",
            EntryPrice: 100m,
            ExitPrice: 90m,
            EntryDate: DateTime.UtcNow.AddDays(-1),
            ExitDate: DateTime.UtcNow,
            PnlPercent: -10m,
            PnlAbsolute: -100m,
            RegimeAtEntry: "Bull",
            RegimeAtExit: "Bear");

        var result = await ReviewTradeHandler.HandleAsync(
            command, errorClaude, db, NullLogger<ReviewTradeHandler>.Instance);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task HandleAsync_WithFeatureSnapshot_IncludesIndicators()
    {
        var db = TestIntelligenceDbContextFactory.Create();
        var claude = new FakeClaudeClient();
        claude.SetDefaultResponse(BuildValidReviewResponse());

        var tradeId = Guid.NewGuid();

        // Add a feature snapshot with plain JSON indicators
        db.FeatureSnapshots.Add(new FeatureSnapshot
        {
            TradeId = tradeId,
            Symbol = "AAPL",
            MarketCode = "US_SP500",
            CapturedAt = DateTime.UtcNow.AddDays(-1),
            FeatureVersion = 1,
            FeatureCount = 2,
            FeaturesJson = """{"rsi": 65.5, "macd": 1.2}""",
            FeaturesHash = "abc123"
        });
        await db.SaveChangesAsync();

        var command = new ReviewTradeCommand(
            TradeId: tradeId,
            Symbol: "AAPL",
            MarketCode: "US_SP500",
            StrategyName: "Momentum",
            EntryPrice: 180m,
            ExitPrice: 190m,
            EntryDate: DateTime.UtcNow.AddDays(-1),
            ExitDate: DateTime.UtcNow,
            PnlPercent: 5.56m,
            PnlAbsolute: 500m,
            RegimeAtEntry: "Bull",
            RegimeAtExit: "Bull");

        var result = await ReviewTradeHandler.HandleAsync(
            command, claude, db, NullLogger<ReviewTradeHandler>.Instance);

        Assert.True(result.Success);
        Assert.Equal(1, claude.CallCount);

        // Verify indicators were saved
        var saved = await db.TradeReviews.FindAsync(result.ReviewId);
        Assert.NotEqual("{}", saved!.IndicatorValuesJson);
    }

    [Fact]
    public async Task HandleAsync_CapturesDuration()
    {
        var db = TestIntelligenceDbContextFactory.Create();
        var claude = new FakeClaudeClient();
        claude.SetDefaultResponse(BuildValidReviewResponse());

        var entryDate = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        var exitDate = new DateTime(2026, 1, 17, 14, 30, 0, DateTimeKind.Utc);

        var command = new ReviewTradeCommand(
            TradeId: Guid.NewGuid(),
            Symbol: "AAPL",
            MarketCode: "US_SP500",
            StrategyName: "Swing",
            EntryPrice: 180m,
            ExitPrice: 185m,
            EntryDate: entryDate,
            ExitDate: exitDate,
            PnlPercent: 2.78m,
            PnlAbsolute: 250m,
            RegimeAtEntry: "Bull",
            RegimeAtExit: "Bull");

        var result = await ReviewTradeHandler.HandleAsync(
            command, claude, db, NullLogger<ReviewTradeHandler>.Instance);

        Assert.True(result.Success);
        var saved = await db.TradeReviews.FindAsync(result.ReviewId);
        Assert.Equal(52.5, saved!.DurationHours, precision: 1);
    }

    #endregion

    #region ClassifyOutcome Tests

    [Theory]
    [InlineData("GoodEntryGoodExit", OutcomeClass.GoodEntryGoodExit)]
    [InlineData("GoodEntryBadExit", OutcomeClass.GoodEntryBadExit)]
    [InlineData("BadEntry", OutcomeClass.BadEntry)]
    [InlineData("RegimeMismatch", OutcomeClass.RegimeMismatch)]
    [InlineData("StoppedCorrectly", OutcomeClass.StoppedCorrectly)]
    [InlineData("StoppedPrematurely", OutcomeClass.StoppedPrematurely)]
    public void ClassifyOutcome_ExactMatch(string input, OutcomeClass expected)
    {
        Assert.Equal(expected, ReviewTradeHandler.ClassifyOutcome(input));
    }

    [Theory]
    [InlineData("good entry and good exit", OutcomeClass.GoodEntryGoodExit)]
    [InlineData("good entry but bad exit timing", OutcomeClass.GoodEntryBadExit)]
    [InlineData("regime mismatch detected", OutcomeClass.RegimeMismatch)]
    [InlineData("stopped prematurely", OutcomeClass.StoppedPrematurely)]
    [InlineData("stopped out correctly", OutcomeClass.StoppedCorrectly)]
    public void ClassifyOutcome_FuzzyMatch(string input, OutcomeClass expected)
    {
        Assert.Equal(expected, ReviewTradeHandler.ClassifyOutcome(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void ClassifyOutcome_EmptyOrNull_DefaultsToBadEntry(string? input)
    {
        Assert.Equal(OutcomeClass.BadEntry, ReviewTradeHandler.ClassifyOutcome(input!));
    }

    #endregion

    #region ClassifyMistake Tests

    [Theory]
    [InlineData("BadSignal", MistakeType.BadSignal)]
    [InlineData("BadTiming", MistakeType.BadTiming)]
    [InlineData("RegimeMismatch", MistakeType.RegimeMismatch)]
    [InlineData("StopTooTight", MistakeType.StopTooTight)]
    [InlineData("StopTooLoose", MistakeType.StopTooLoose)]
    [InlineData("OversizedPosition", MistakeType.OversizedPosition)]
    [InlineData("CorrelatedLoss", MistakeType.CorrelatedLoss)]
    [InlineData("BlackSwan", MistakeType.BlackSwan)]
    public void ClassifyMistake_ExactMatch(string input, MistakeType expected)
    {
        Assert.Equal(expected, ReviewTradeHandler.ClassifyMistake(input));
    }

    [Theory]
    [InlineData("timing was off", MistakeType.BadTiming)]
    [InlineData("regime mismatch", MistakeType.RegimeMismatch)]
    [InlineData("stop was too tight", MistakeType.StopTooTight)]
    [InlineData("stop too loose", MistakeType.StopTooLoose)]
    [InlineData("oversized position", MistakeType.OversizedPosition)]
    [InlineData("correlated assets", MistakeType.CorrelatedLoss)]
    [InlineData("black swan event", MistakeType.BlackSwan)]
    public void ClassifyMistake_FuzzyMatch(string input, MistakeType expected)
    {
        Assert.Equal(expected, ReviewTradeHandler.ClassifyMistake(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void ClassifyMistake_EmptyOrNull_DefaultsToBadSignal(string? input)
    {
        Assert.Equal(MistakeType.BadSignal, ReviewTradeHandler.ClassifyMistake(input));
    }

    #endregion

    #region Query Handler Tests

    [Fact]
    public async Task GetTradeReviews_ReturnsPaginatedResults()
    {
        var db = TestIntelligenceDbContextFactory.Create();

        for (int i = 0; i < 5; i++)
        {
            db.TradeReviews.Add(new TradeReview
            {
                TradeId = Guid.NewGuid(),
                Symbol = "AAPL",
                MarketCode = "US_SP500",
                OutcomeClass = OutcomeClass.GoodEntryGoodExit,
                Score = 7 + i % 3,
                Summary = $"Review {i}",
                ReviewedAt = DateTime.UtcNow.AddHours(-i)
            });
        }
        await db.SaveChangesAsync();

        var result = await GetTradeReviewsHandler.HandleAsync(
            new GetTradeReviewsQuery(Page: 1, PageSize: 3), db);

        Assert.Equal(5, result.TotalCount);
        Assert.Equal(3, result.Items.Count);
        Assert.Equal(1, result.Page);
    }

    [Fact]
    public async Task GetTradeReviews_FiltersBySymbol()
    {
        var db = TestIntelligenceDbContextFactory.Create();

        db.TradeReviews.Add(new TradeReview
        {
            TradeId = Guid.NewGuid(),
            Symbol = "AAPL",
            MarketCode = "US_SP500",
            OutcomeClass = OutcomeClass.GoodEntryGoodExit,
            Score = 8,
            Summary = "AAPL review"
        });
        db.TradeReviews.Add(new TradeReview
        {
            TradeId = Guid.NewGuid(),
            Symbol = "TSLA",
            MarketCode = "US_SP500",
            OutcomeClass = OutcomeClass.BadEntry,
            Score = 3,
            Summary = "TSLA review"
        });
        await db.SaveChangesAsync();

        var result = await GetTradeReviewsHandler.HandleAsync(
            new GetTradeReviewsQuery(Symbol: "AAPL"), db);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("AAPL", result.Items[0].Symbol);
    }

    [Fact]
    public async Task GetTradeReviews_FiltersByOutcomeClass()
    {
        var db = TestIntelligenceDbContextFactory.Create();

        db.TradeReviews.Add(new TradeReview
        {
            TradeId = Guid.NewGuid(),
            Symbol = "AAPL",
            MarketCode = "US_SP500",
            OutcomeClass = OutcomeClass.GoodEntryGoodExit,
            Score = 8,
            Summary = "Good"
        });
        db.TradeReviews.Add(new TradeReview
        {
            TradeId = Guid.NewGuid(),
            Symbol = "TSLA",
            MarketCode = "US_SP500",
            OutcomeClass = OutcomeClass.BadEntry,
            Score = 3,
            Summary = "Bad"
        });
        await db.SaveChangesAsync();

        var result = await GetTradeReviewsHandler.HandleAsync(
            new GetTradeReviewsQuery(OutcomeClass: "BadEntry"), db);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("BadEntry", result.Items[0].OutcomeClass);
    }

    [Fact]
    public async Task GetTradeReviews_FiltersByMarketCode()
    {
        var db = TestIntelligenceDbContextFactory.Create();

        db.TradeReviews.Add(new TradeReview
        {
            TradeId = Guid.NewGuid(),
            Symbol = "RELIANCE",
            MarketCode = "IN_NIFTY50",
            OutcomeClass = OutcomeClass.RegimeMismatch,
            Score = 4,
            Summary = "India trade"
        });
        db.TradeReviews.Add(new TradeReview
        {
            TradeId = Guid.NewGuid(),
            Symbol = "AAPL",
            MarketCode = "US_SP500",
            OutcomeClass = OutcomeClass.GoodEntryGoodExit,
            Score = 8,
            Summary = "US trade"
        });
        await db.SaveChangesAsync();

        var result = await GetTradeReviewsHandler.HandleAsync(
            new GetTradeReviewsQuery(MarketCode: "IN_NIFTY50"), db);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("IN_NIFTY50", result.Items[0].MarketCode);
    }

    [Fact]
    public async Task GetTradeReviewByTradeId_ReturnsReview()
    {
        var db = TestIntelligenceDbContextFactory.Create();
        var tradeId = Guid.NewGuid();

        db.TradeReviews.Add(new TradeReview
        {
            TradeId = tradeId,
            Symbol = "AAPL",
            MarketCode = "US_SP500",
            StrategyName = "BullMomentum",
            EntryPrice = 180m,
            ExitPrice = 190m,
            PnlPercent = 5.56m,
            OutcomeClass = OutcomeClass.GoodEntryGoodExit,
            Score = 8,
            Summary = "Good trade",
            StrengthsJson = """["Good timing"]""",
            WeaknessesJson = """["Hold longer"]""",
            LessonsLearnedJson = """["Momentum works"]"""
        });
        await db.SaveChangesAsync();

        var result = await GetTradeReviewByTradeIdHandler.HandleAsync(
            new GetTradeReviewByTradeIdQuery(tradeId), db);

        Assert.NotNull(result);
        Assert.Equal(tradeId, result!.TradeId);
        Assert.Equal("AAPL", result.Symbol);
        Assert.Equal("GoodEntryGoodExit", result.OutcomeClass);
        Assert.Single(result.Strengths);
        Assert.Single(result.Weaknesses);
        Assert.Single(result.LessonsLearned);
    }

    [Fact]
    public async Task GetTradeReviewByTradeId_NotFound_ReturnsNull()
    {
        var db = TestIntelligenceDbContextFactory.Create();

        var result = await GetTradeReviewByTradeIdHandler.HandleAsync(
            new GetTradeReviewByTradeIdQuery(Guid.NewGuid()), db);

        Assert.Null(result);
    }

    #endregion

    #region Prompt Tests

    [Fact]
    public void BuildSystemPrompt_ContainsMistakeTypes()
    {
        var prompt = TradeReviewPrompt.BuildSystemPrompt();

        Assert.Contains("BadSignal", prompt);
        Assert.Contains("BadTiming", prompt);
        Assert.Contains("StopTooTight", prompt);
        Assert.Contains("StopTooLoose", prompt);
        Assert.Contains("OversizedPosition", prompt);
        Assert.Contains("CorrelatedLoss", prompt);
        Assert.Contains("BlackSwan", prompt);
    }

    [Fact]
    public void BuildUserPrompt_IncludesRegimeAndGrade()
    {
        var input = new TradeReviewInput(
            Symbol: "AAPL",
            Side: "Long",
            EntryDate: new DateTime(2026, 1, 15),
            ExitDate: new DateTime(2026, 1, 20),
            EntryPrice: 180m,
            ExitPrice: 190m,
            PnlPercent: 5.56m,
            StrategyName: "BullMomentum",
            RegimeAtEntry: "Bull",
            RegimeAtExit: "HighVol",
            Grade: 78.5m,
            MlConfidence: 0.72f);

        var prompt = TradeReviewPrompt.BuildUserPrompt(input);

        Assert.Contains("Bull", prompt);
        Assert.Contains("HighVol", prompt);
        Assert.Contains("78.5", prompt);
        Assert.Contains("0.72", prompt);
    }

    [Fact]
    public void ParseResponse_ValidJson_ReturnsOutput()
    {
        var json = """
            {
              "outcomeClass": "StoppedCorrectly",
              "mistakeType": null,
              "score": 6,
              "strengths": ["Good stop placement"],
              "weaknesses": ["Market moved against"],
              "lessonsLearned": ["Accept good stops"],
              "summary": "Acceptable loss with well-placed stop"
            }
            """;

        var result = TradeReviewPrompt.ParseResponse(json);

        Assert.NotNull(result);
        Assert.Equal("StoppedCorrectly", result!.OutcomeClass);
        Assert.Null(result.MistakeType);
        Assert.Equal(6, result.Score);
    }

    [Fact]
    public void ParseResponse_WithMistakeType_ParsesCorrectly()
    {
        var json = """
            {
              "outcomeClass": "BadEntry",
              "mistakeType": "BadTiming",
              "score": 3,
              "strengths": [],
              "weaknesses": ["Entered too early"],
              "lessonsLearned": ["Wait for confirmation"],
              "summary": "Bad timing on entry"
            }
            """;

        var result = TradeReviewPrompt.ParseResponse(json);

        Assert.NotNull(result);
        Assert.Equal("BadEntry", result!.OutcomeClass);
        Assert.Equal("BadTiming", result.MistakeType);
        Assert.Equal(3, result.Score);
    }

    #endregion
}
