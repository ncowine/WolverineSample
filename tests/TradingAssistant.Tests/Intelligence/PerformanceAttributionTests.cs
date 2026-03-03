using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TradingAssistant.Application.Handlers.Intelligence;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.Domain.MarketData;
using TradingAssistant.Tests.Helpers;

namespace TradingAssistant.Tests.Intelligence;

public class PerformanceAttributionTests
{
    // ──────────────────────────────────────────────────────────────────
    // ComputeBeta tests — known covariance/variance scenarios
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeBeta_PerfectCorrelation_ReturnsBetaOne()
    {
        // Strategy returns = benchmark returns → beta should be ~1.0
        var history = new List<PerformanceAttributor.MonthlyReturn>
        {
            new(2026, 1, 2.0m, 2.0m),
            new(2026, 2, -1.0m, -1.0m),
            new(2026, 3, 3.0m, 3.0m),
            new(2026, 4, -0.5m, -0.5m),
        };

        var beta = PerformanceAttributor.ComputeBeta(history);

        Assert.Equal(1.0m, beta, 2);
    }

    [Fact]
    public void ComputeBeta_DoubleLeverage_ReturnsBetaTwo()
    {
        // Strategy = 2× benchmark → beta should be ~2.0
        var history = new List<PerformanceAttributor.MonthlyReturn>
        {
            new(2026, 1, 4.0m, 2.0m),
            new(2026, 2, -2.0m, -1.0m),
            new(2026, 3, 6.0m, 3.0m),
            new(2026, 4, -1.0m, -0.5m),
        };

        var beta = PerformanceAttributor.ComputeBeta(history);

        Assert.Equal(2.0m, beta, 2);
    }

    [Fact]
    public void ComputeBeta_ZeroBenchmarkVariance_ReturnsZero()
    {
        // Benchmark is flat → variance = 0 → beta = 0
        var history = new List<PerformanceAttributor.MonthlyReturn>
        {
            new(2026, 1, 2.0m, 1.0m),
            new(2026, 2, -1.0m, 1.0m),
            new(2026, 3, 3.0m, 1.0m),
        };

        var beta = PerformanceAttributor.ComputeBeta(history);

        Assert.Equal(0m, beta);
    }

    [Fact]
    public void ComputeBeta_InsufficientData_ReturnsZero()
    {
        var history = new List<PerformanceAttributor.MonthlyReturn>
        {
            new(2026, 1, 2.0m, 1.5m),
        };

        var beta = PerformanceAttributor.ComputeBeta(history);

        Assert.Equal(0m, beta);
    }

    [Fact]
    public void ComputeBeta_NegativeCorrelation_ReturnsNegativeBeta()
    {
        // Strategy moves opposite to benchmark
        var history = new List<PerformanceAttributor.MonthlyReturn>
        {
            new(2026, 1, -2.0m, 2.0m),
            new(2026, 2, 1.0m, -1.0m),
            new(2026, 3, -3.0m, 3.0m),
            new(2026, 4, 0.5m, -0.5m),
        };

        var beta = PerformanceAttributor.ComputeBeta(history);

        Assert.True(beta < 0);
        Assert.Equal(-1.0m, beta, 2);
    }

    // ──────────────────────────────────────────────────────────────────
    // Attribute tests — decomposition verification
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Attribute_DecomposesCorrectly()
    {
        // Strategy returned 5%, benchmark 2%, beta = 1.5
        // BetaContribution = 1.5 * 2 = 3%
        // RegimeContribution = 0.5%
        // Alpha = 5 - 3 - 0.5 = 1.5%
        var result = PerformanceAttributor.Attribute(
            year: 2026, month: 3,
            strategyReturn: 5.0m, benchmarkReturn: 2.0m,
            beta: 1.5m, regimeContribution: 0.5m,
            tradeCount: 10, regimeAlignedTrades: 8, regimeMismatchedTrades: 2);

        Assert.Equal(5.0m, result.TotalReturn);
        Assert.Equal(3.0m, result.BetaContribution);
        Assert.Equal(0.5m, result.RegimeContribution);
        Assert.Equal(1.5m, result.Alpha);
        Assert.Equal(0m, result.Residual);
    }

    [Fact]
    public void Attribute_NegativeAlpha_WhenStrategyUnderperforms()
    {
        // Strategy returned 1%, benchmark 3%, beta = 1.0
        // BetaContribution = 3%
        // Alpha = 1 - 3 - 0 = -2% (negative alpha = strategy subtracted value)
        var result = PerformanceAttributor.Attribute(
            year: 2026, month: 1, strategyReturn: 1.0m, benchmarkReturn: 3.0m,
            beta: 1.0m, regimeContribution: 0m,
            tradeCount: 5, regimeAlignedTrades: 5, regimeMismatchedTrades: 0);

        Assert.Equal(-2.0m, result.Alpha);
        Assert.Equal(3.0m, result.BetaContribution);
    }

    [Fact]
    public void Attribute_ZeroBeta_AllReturnsAreAlpha()
    {
        // Market-neutral: beta = 0
        var result = PerformanceAttributor.Attribute(
            year: 2026, month: 2, strategyReturn: 4.0m, benchmarkReturn: 2.0m,
            beta: 0m, regimeContribution: 0m,
            tradeCount: 8, regimeAlignedTrades: 8, regimeMismatchedTrades: 0);

        Assert.Equal(4.0m, result.Alpha);
        Assert.Equal(0m, result.BetaContribution);
    }

    // ──────────────────────────────────────────────────────────────────
    // RegimeContribution tests
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeRegimeContribution_NoTrades_ReturnsZero()
    {
        var result = PerformanceAttributor.ComputeRegimeContribution([]);
        Assert.Equal(0m, result);
    }

    [Fact]
    public void ComputeRegimeContribution_AllAligned_ReturnsZero()
    {
        var trades = new List<TradeReview>
        {
            MakeReview(2m, "Bull", "Bull"),
            MakeReview(-1m, "Bear", "Bear"),
        };

        var result = PerformanceAttributor.ComputeRegimeContribution(trades);
        Assert.Equal(0m, result);
    }

    [Fact]
    public void ComputeRegimeContribution_MismatchPerformsWorse_PositiveContribution()
    {
        // Aligned trades avg = +3%, mismatched avg = -2%
        // Contribution should be positive (being aligned helped)
        var trades = new List<TradeReview>
        {
            MakeReview(3m, "Bull", "Bull"),
            MakeReview(3m, "Bull", "Bull"),
            MakeReview(-2m, "Bull", "Bear"), // regime changed → mismatched
        };

        var result = PerformanceAttributor.ComputeRegimeContribution(trades);
        Assert.True(result > 0);
    }

    [Fact]
    public void ComputeRegimeContribution_MismatchPerformsBetter_NegativeContribution()
    {
        // Mismatched trades actually did better — negative regime contribution
        var trades = new List<TradeReview>
        {
            MakeReview(-1m, "Bull", "Bull"),
            MakeReview(-1m, "Bear", "Bear"),
            MakeReview(5m, "Bull", "Bear"), // regime changed but trade was profitable
        };

        var result = PerformanceAttributor.ComputeRegimeContribution(trades);
        Assert.True(result < 0);
    }

    // ──────────────────────────────────────────────────────────────────
    // RollingSummary tests
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void BuildRollingSummary_EmptyResults_ReturnsZeros()
    {
        var summary = PerformanceAttributor.BuildRollingSummary([]);
        Assert.Equal(0, summary.MonthsIncluded);
        Assert.Equal(0m, summary.CumulativeReturn);
    }

    [Fact]
    public void BuildRollingSummary_Accumulates12Months()
    {
        var results = Enumerable.Range(1, 12).Select(m =>
            new PerformanceAttributor.AttributionResult(
                Year: 2026, Month: m, TotalReturn: 1.0m, Alpha: 0.5m,
                BetaContribution: 0.3m, RegimeContribution: 0.1m, Residual: 0.1m,
                Beta: 0.8m, BenchmarkReturn: 0.4m,
                TradeCount: 5, RegimeAlignedTrades: 4, RegimeMismatchedTrades: 1))
            .ToList();

        var summary = PerformanceAttributor.BuildRollingSummary(results);

        Assert.Equal(12, summary.MonthsIncluded);
        Assert.Equal(12.0m, summary.CumulativeReturn);
        Assert.Equal(6.0m, summary.CumulativeAlpha);
        Assert.Equal(0.8m, summary.AverageBeta);
    }

    [Fact]
    public void BuildRollingSummary_TakesOnly12MostRecent()
    {
        var results = Enumerable.Range(1, 15).Select(m =>
            new PerformanceAttributor.AttributionResult(
                Year: m <= 12 ? 2025 : 2026, Month: m <= 12 ? m : m - 12,
                TotalReturn: 1.0m, Alpha: 0.5m,
                BetaContribution: 0.3m, RegimeContribution: 0.1m, Residual: 0.1m,
                Beta: 0.8m, BenchmarkReturn: 0.4m,
                TradeCount: 5, RegimeAlignedTrades: 4, RegimeMismatchedTrades: 1))
            .ToList();

        var summary = PerformanceAttributor.BuildRollingSummary(results);

        Assert.Equal(12, summary.MonthsIncluded);
    }

    // ──────────────────────────────────────────────────────────────────
    // BenchmarkSymbols mapping tests
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void BenchmarkSymbols_USMarket_MapsToSPY()
    {
        Assert.True(PerformanceAttributor.BenchmarkSymbols.TryGetValue("US_SP500", out var sym));
        Assert.Equal("SPY", sym);
    }

    [Fact]
    public void BenchmarkSymbols_IndiaMarket_MapsToNSEI()
    {
        Assert.True(PerformanceAttributor.BenchmarkSymbols.TryGetValue("IN_NIFTY50", out var sym));
        Assert.Equal("^NSEI", sym);
    }

    // ──────────────────────────────────────────────────────────────────
    // Handler tests
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_ComputesAndSavesAttribution()
    {
        var dbName = Guid.NewGuid().ToString();
        var intelligenceDb = TestIntelligenceDbContextFactory.Create(dbName);
        var marketDb = TestMarketDataDbContextFactory.Create();

        // Add trade reviews for March 2026
        intelligenceDb.TradeReviews.Add(MakeReviewWithDate(2m, "Bull", "Bull", "US_SP500",
            new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc)));
        intelligenceDb.TradeReviews.Add(MakeReviewWithDate(-1m, "Bull", "Bear", "US_SP500",
            new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc)));
        await intelligenceDb.SaveChangesAsync();

        // Add benchmark data
        var spy = new Stock { Symbol = "SPY", Name = "SPDR S&P 500", Exchange = "NYSE" };
        marketDb.Stocks.Add(spy);
        await marketDb.SaveChangesAsync();

        marketDb.PriceCandles.Add(new PriceCandle
        {
            StockId = spy.Id, Close = 500m,
            Timestamp = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            Interval = CandleInterval.Daily
        });
        marketDb.PriceCandles.Add(new PriceCandle
        {
            StockId = spy.Id, Close = 510m,
            Timestamp = new DateTime(2026, 3, 28, 0, 0, 0, DateTimeKind.Utc),
            Interval = CandleInterval.Daily
        });
        await marketDb.SaveChangesAsync();

        var result = await GetAttributionHandler.HandleAsync(
            new GetAttributionQuery("US_SP500", 2026, 3),
            intelligenceDb, marketDb, NullLogger<GetAttributionHandler>.Instance);

        Assert.Equal("US_SP500", result.MarketCode);
        Assert.Equal(2026, result.Year);
        Assert.Equal(3, result.Month);
        Assert.Equal(1.0m, result.TotalReturn); // 2 + (-1) = 1%
        Assert.Equal(2, result.TradeCount);
        Assert.True(result.BenchmarkReturn > 0); // SPY went from 500 to 510

        // Verify saved to DB
        var saved = await intelligenceDb.MonthlyAttributions
            .FirstOrDefaultAsync(a => a.Year == 2026 && a.Month == 3);
        Assert.NotNull(saved);
    }

    [Fact]
    public async Task HandleAsync_ReturnsCachedIfExists()
    {
        var intelligenceDb = TestIntelligenceDbContextFactory.Create();
        var marketDb = TestMarketDataDbContextFactory.Create();

        // Pre-populate cached attribution
        intelligenceDb.MonthlyAttributions.Add(new MonthlyAttribution
        {
            MarketCode = "US_SP500", Year = 2026, Month = 2,
            TotalReturn = 5.0m, Alpha = 3.0m, BetaContribution = 2.0m,
            RegimeContribution = 0m, Residual = 0m, Beta = 1.0m,
            BenchmarkReturn = 2.0m, TradeCount = 10,
            RegimeAlignedTrades = 8, RegimeMismatchedTrades = 2
        });
        await intelligenceDb.SaveChangesAsync();

        var result = await GetAttributionHandler.HandleAsync(
            new GetAttributionQuery("US_SP500", 2026, 2),
            intelligenceDb, marketDb, NullLogger<GetAttributionHandler>.Instance);

        Assert.Equal(5.0m, result.TotalReturn);
        Assert.Equal(3.0m, result.Alpha);
    }

    [Fact]
    public async Task HandleRollingAsync_ReturnsUpTo12Months()
    {
        var intelligenceDb = TestIntelligenceDbContextFactory.Create();

        for (int m = 1; m <= 6; m++)
        {
            intelligenceDb.MonthlyAttributions.Add(new MonthlyAttribution
            {
                MarketCode = "US_SP500", Year = 2026, Month = m,
                TotalReturn = 1.0m, Alpha = 0.5m, BetaContribution = 0.3m,
                RegimeContribution = 0.1m, Residual = 0.1m,
                Beta = 0.9m, BenchmarkReturn = 0.3m, TradeCount = 5,
                RegimeAlignedTrades = 4, RegimeMismatchedTrades = 1
            });
        }
        await intelligenceDb.SaveChangesAsync();

        var result = await GetAttributionHandler.HandleRollingAsync(
            new GetRollingAttributionQuery("US_SP500"), intelligenceDb);

        Assert.Equal(6, result.MonthsIncluded);
        Assert.Equal(6.0m, result.CumulativeReturn);
        Assert.Equal(3.0m, result.CumulativeAlpha);
    }

    [Fact]
    public async Task HandleAsync_NoBenchmarkData_BetaIsZero()
    {
        var intelligenceDb = TestIntelligenceDbContextFactory.Create();
        var marketDb = TestMarketDataDbContextFactory.Create();

        intelligenceDb.TradeReviews.Add(MakeReviewWithDate(3m, "Bull", "Bull", "US_SP500",
            new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc)));
        await intelligenceDb.SaveChangesAsync();

        // No benchmark data in marketDb

        var result = await GetAttributionHandler.HandleAsync(
            new GetAttributionQuery("US_SP500", 2026, 1),
            intelligenceDb, marketDb, NullLogger<GetAttributionHandler>.Instance);

        Assert.Equal(3.0m, result.TotalReturn);
        Assert.Equal(0m, result.BenchmarkReturn);
        Assert.Equal(0m, result.Beta);
        Assert.Equal(3.0m, result.Alpha); // All return is alpha when no benchmark
    }

    // ──────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────

    private static TradeReview MakeReview(decimal pnlPercent, string regimeEntry, string regimeExit)
    {
        return new TradeReview
        {
            TradeId = Guid.NewGuid(),
            Symbol = "AAPL",
            MarketCode = "US_SP500",
            StrategyName = "TestStrategy",
            EntryPrice = 100m,
            ExitPrice = 100m + pnlPercent,
            EntryDate = DateTime.UtcNow.AddDays(-5),
            ExitDate = DateTime.UtcNow,
            PnlPercent = pnlPercent,
            PnlAbsolute = pnlPercent * 10,
            DurationHours = 48,
            RegimeAtEntry = regimeEntry,
            RegimeAtExit = regimeExit,
            OutcomeClass = pnlPercent >= 0 ? OutcomeClass.GoodEntryGoodExit : OutcomeClass.BadEntry,
            Score = 5,
            Summary = "Test review"
        };
    }

    private static TradeReview MakeReviewWithDate(
        decimal pnlPercent, string regimeEntry, string regimeExit,
        string marketCode, DateTime exitDate)
    {
        var review = MakeReview(pnlPercent, regimeEntry, regimeExit);
        review.MarketCode = marketCode;
        review.ExitDate = exitDate;
        review.EntryDate = exitDate.AddDays(-3);
        review.ReviewedAt = exitDate;
        return review;
    }
}
