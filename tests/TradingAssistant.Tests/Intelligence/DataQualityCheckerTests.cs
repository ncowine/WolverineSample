using TradingAssistant.Application.Intelligence;

namespace TradingAssistant.Tests.Intelligence;

public class DataQualityCheckerTests
{
    private static readonly DateTime RefDate = new(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc); // Monday

    private static CandleData Candle(DateTime date, decimal close = 100m, long volume = 1000)
        => new(date, close * 0.99m, close * 1.01m, close * 0.98m, close, volume);

    private static CandleData Candle(int year, int month, int day, decimal close = 100m, long volume = 1000)
        => Candle(new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc), close, volume);

    // ── Check (full report) ──

    [Fact]
    public void Check_CleanData_NotFlagged()
    {
        var candles = new[]
        {
            Candle(2026, 2, 27, 100m), // Friday
            Candle(2026, 3, 2, 101m),  // Monday (weekend gap = 3 days, within threshold)
        };

        var report = DataQualityChecker.Check("AAPL", candles, RefDate);

        Assert.False(report.IsFlagged);
        Assert.Empty(report.Issues);
        Assert.Equal("AAPL", report.Symbol);
    }

    [Fact]
    public void Check_EmptyCandles_FlagsStaleFeed()
    {
        var report = DataQualityChecker.Check("AAPL", Array.Empty<CandleData>(), RefDate);

        Assert.True(report.IsFlagged);
        Assert.Single(report.Issues);
        Assert.Equal(DataQualityIssueType.StaleFeed, report.Issues[0].Type);
        Assert.Contains("No candle data", report.Issues[0].Detail);
    }

    [Fact]
    public void Check_MultipleIssueTypes_AllReported()
    {
        var candles = new[]
        {
            Candle(2026, 2, 10, 100m),       // Tuesday
            Candle(2026, 2, 20, 150m, 0),     // Friday - 10 day gap + 50% price gap + zero volume
        };

        var report = DataQualityChecker.Check("TSLA", candles, RefDate);

        Assert.True(report.IsFlagged);
        Assert.True(report.MissingDayCount > 0);
        Assert.True(report.PriceGapCount > 0);
        Assert.True(report.ZeroVolumeCount > 0);
        Assert.True(report.HasStaleFeed);
    }

    [Fact]
    public void Check_ReportCountersAreAccurate()
    {
        var candles = new[]
        {
            Candle(2026, 2, 23, 100m),     // Monday
            Candle(2026, 2, 24, 100m, 0),  // Tuesday - zero volume
            Candle(2026, 2, 25, 100m, 0),  // Wednesday - zero volume
            Candle(2026, 2, 26, 100m),     // Thursday
            Candle(2026, 2, 27, 100m),     // Friday
            Candle(2026, 3, 2, 100m),      // Monday - normal weekend
        };

        var report = DataQualityChecker.Check("MSFT", candles, RefDate);

        Assert.Equal(2, report.ZeroVolumeCount);
        Assert.Equal(0, report.MissingDayCount);
        Assert.Equal(0, report.PriceGapCount);
        Assert.False(report.HasStaleFeed);
    }

    // ── Missing Trading Days ──

    [Fact]
    public void MissingDays_WeekendGap_NotFlagged()
    {
        // Friday to Monday = 3 calendar days, within default threshold
        var candles = new[]
        {
            Candle(2026, 2, 27), // Friday
            Candle(2026, 3, 2),  // Monday
        };

        var issues = DataQualityChecker.CheckMissingTradingDays(candles);

        Assert.Empty(issues);
    }

    [Fact]
    public void MissingDays_ConsecutiveWeekdays_NotFlagged()
    {
        var candles = new[]
        {
            Candle(2026, 2, 23), // Monday
            Candle(2026, 2, 24), // Tuesday
            Candle(2026, 2, 25), // Wednesday
        };

        var issues = DataQualityChecker.CheckMissingTradingDays(candles);

        Assert.Empty(issues);
    }

    [Fact]
    public void MissingDays_FourCalendarDayGap_Flagged()
    {
        // Monday to Friday = 4 calendar days gap (Tuesday-Thursday missing)
        var candles = new[]
        {
            Candle(2026, 2, 23), // Monday
            Candle(2026, 2, 27), // Friday
        };

        var issues = DataQualityChecker.CheckMissingTradingDays(candles);

        Assert.Single(issues);
        Assert.Equal(DataQualityIssueType.MissingTradingDay, issues[0].Type);
        Assert.Contains("4 calendar days", issues[0].Detail);
    }

    [Fact]
    public void MissingDays_LongHolidayGap_Flagged()
    {
        // 10 calendar day gap
        var candles = new[]
        {
            Candle(2026, 2, 10), // Tuesday
            Candle(2026, 2, 20), // Friday
        };

        var issues = DataQualityChecker.CheckMissingTradingDays(candles);

        Assert.Single(issues);
        Assert.Contains("10 calendar days", issues[0].Detail);
    }

    [Fact]
    public void MissingDays_CustomThreshold_Respected()
    {
        // 3 day gap, custom threshold of 2
        var candles = new[]
        {
            Candle(2026, 2, 27), // Friday
            Candle(2026, 3, 2),  // Monday
        };

        var issues = DataQualityChecker.CheckMissingTradingDays(candles, maxCalendarDayGap: 2);

        Assert.Single(issues); // 3 day gap exceeds threshold of 2
    }

    [Fact]
    public void MissingDays_SingleCandle_NothingToCompare()
    {
        var candles = new[] { Candle(2026, 3, 2) };

        var issues = DataQualityChecker.CheckMissingTradingDays(candles);

        Assert.Empty(issues);
    }

    [Fact]
    public void MissingDays_MultipleGaps_AllDetected()
    {
        var candles = new[]
        {
            Candle(2026, 2, 2),  // Monday
            Candle(2026, 2, 10), // Tuesday next week (8 day gap)
            Candle(2026, 2, 11), // Wednesday (1 day, fine)
            Candle(2026, 2, 20), // Friday (9 day gap)
        };

        var issues = DataQualityChecker.CheckMissingTradingDays(candles);

        Assert.Equal(2, issues.Count);
    }

    // ── Price Gaps ──

    [Fact]
    public void PriceGaps_NormalMove_NotFlagged()
    {
        var candles = new[]
        {
            Candle(2026, 2, 23, 100m),
            Candle(2026, 2, 24, 105m), // 5% move
        };

        var issues = DataQualityChecker.CheckPriceGaps(candles);

        Assert.Empty(issues);
    }

    [Fact]
    public void PriceGaps_ExactlyAtThreshold_NotFlagged()
    {
        var candles = new[]
        {
            Candle(2026, 2, 23, 100m),
            Candle(2026, 2, 24, 120m), // exactly 20%
        };

        var issues = DataQualityChecker.CheckPriceGaps(candles);

        Assert.Empty(issues);
    }

    [Fact]
    public void PriceGaps_OverThreshold_Flagged()
    {
        var candles = new[]
        {
            Candle(2026, 2, 23, 100m),
            Candle(2026, 2, 24, 121m), // 21% gap
        };

        var issues = DataQualityChecker.CheckPriceGaps(candles);

        Assert.Single(issues);
        Assert.Equal(DataQualityIssueType.PriceGap, issues[0].Type);
        Assert.Contains("21.0%", issues[0].Detail);
    }

    [Fact]
    public void PriceGaps_LargeDropDetected()
    {
        var candles = new[]
        {
            Candle(2026, 2, 23, 100m),
            Candle(2026, 2, 24, 70m), // -30% drop
        };

        var issues = DataQualityChecker.CheckPriceGaps(candles);

        Assert.Single(issues);
        Assert.Contains("30.0%", issues[0].Detail);
    }

    [Fact]
    public void PriceGaps_ZeroPreviousClose_Skipped()
    {
        var candles = new[]
        {
            new CandleData(new DateTime(2026, 2, 23), 0m, 0m, 0m, 0m, 1000),
            Candle(2026, 2, 24, 100m),
        };

        var issues = DataQualityChecker.CheckPriceGaps(candles);

        Assert.Empty(issues);
    }

    [Fact]
    public void PriceGaps_CustomThreshold_Respected()
    {
        var candles = new[]
        {
            Candle(2026, 2, 23, 100m),
            Candle(2026, 2, 24, 111m), // 11% gap
        };

        var issues = DataQualityChecker.CheckPriceGaps(candles, priceGapThresholdPercent: 10m);

        Assert.Single(issues); // 11% > 10% threshold
    }

    [Fact]
    public void PriceGaps_MultipleLargeGaps_AllDetected()
    {
        var candles = new[]
        {
            Candle(2026, 2, 23, 100m),
            Candle(2026, 2, 24, 130m), // +30%
            Candle(2026, 2, 25, 90m),  // -30.8%
        };

        var issues = DataQualityChecker.CheckPriceGaps(candles);

        Assert.Equal(2, issues.Count);
    }

    // ── Zero Volume ──

    [Fact]
    public void ZeroVolume_NormalVolume_NotFlagged()
    {
        var candles = new[]
        {
            Candle(2026, 2, 23, 100m, 50000),
            Candle(2026, 2, 24, 101m, 45000),
        };

        var issues = DataQualityChecker.CheckZeroVolumeDays(candles);

        Assert.Empty(issues);
    }

    [Fact]
    public void ZeroVolume_ZeroDay_Flagged()
    {
        var candles = new[]
        {
            Candle(2026, 2, 23, 100m, 50000),
            Candle(2026, 2, 24, 101m, 0), // zero volume
        };

        var issues = DataQualityChecker.CheckZeroVolumeDays(candles);

        Assert.Single(issues);
        Assert.Equal(DataQualityIssueType.ZeroVolume, issues[0].Type);
        Assert.Contains("Zero volume", issues[0].Detail);
    }

    [Fact]
    public void ZeroVolume_MultipleZeroDays_AllFlagged()
    {
        var candles = new[]
        {
            Candle(2026, 2, 23, 100m, 0),
            Candle(2026, 2, 24, 101m, 0),
            Candle(2026, 2, 25, 102m, 5000),
        };

        var issues = DataQualityChecker.CheckZeroVolumeDays(candles);

        Assert.Equal(2, issues.Count);
    }

    [Fact]
    public void ZeroVolume_EmptyCandles_NoIssues()
    {
        var issues = DataQualityChecker.CheckZeroVolumeDays(Array.Empty<CandleData>());

        Assert.Empty(issues);
    }

    // ── Stale Feed ──

    [Fact]
    public void StaleFeed_CurrentData_NotFlagged()
    {
        // Last candle is today (Monday)
        var candles = new[] { Candle(2026, 3, 2, 100m) };

        var issues = DataQualityChecker.CheckStaleFeed(candles, RefDate);

        Assert.Empty(issues);
    }

    [Fact]
    public void StaleFeed_FridayToMonday_NotFlagged()
    {
        // Friday → Monday ref = 1 trading day gap
        var candles = new[] { Candle(2026, 2, 27, 100m) }; // Friday

        var issues = DataQualityChecker.CheckStaleFeed(candles, RefDate); // Monday

        Assert.Empty(issues); // 1 trading day gap <= 2 threshold
    }

    [Fact]
    public void StaleFeed_ThursdayToMonday_NotFlagged()
    {
        // Thursday → Monday ref = 2 trading days (Fri + Mon)
        var candles = new[] { Candle(2026, 2, 26, 100m) }; // Thursday

        var issues = DataQualityChecker.CheckStaleFeed(candles, RefDate); // Monday

        Assert.Empty(issues); // 2 trading days = threshold
    }

    [Fact]
    public void StaleFeed_WednesdayToMonday_Flagged()
    {
        // Wednesday → Monday ref = 3 trading days (Thu, Fri, Mon)
        var candles = new[] { Candle(2026, 2, 25, 100m) }; // Wednesday

        var issues = DataQualityChecker.CheckStaleFeed(candles, RefDate); // Monday

        Assert.Single(issues);
        Assert.Equal(DataQualityIssueType.StaleFeed, issues[0].Type);
        Assert.Contains("3 trading days", issues[0].Detail);
    }

    [Fact]
    public void StaleFeed_VeryStale_Flagged()
    {
        // 2 weeks old
        var candles = new[] { Candle(2026, 2, 16, 100m) }; // Monday 2 weeks ago

        var issues = DataQualityChecker.CheckStaleFeed(candles, RefDate);

        Assert.Single(issues);
        Assert.Equal(DataQualityIssueType.StaleFeed, issues[0].Type);
    }

    [Fact]
    public void StaleFeed_EmptyCandles_Flagged()
    {
        var issues = DataQualityChecker.CheckStaleFeed(Array.Empty<CandleData>(), RefDate);

        Assert.Single(issues);
        Assert.Equal(DataQualityIssueType.StaleFeed, issues[0].Type);
    }

    [Fact]
    public void StaleFeed_CustomThreshold_Respected()
    {
        // Thursday → Monday = 2 trading days
        var candles = new[] { Candle(2026, 2, 26, 100m) }; // Thursday

        var issues = DataQualityChecker.CheckStaleFeed(candles, RefDate, staleFeedDays: 1);

        Assert.Single(issues); // 2 > 1 threshold
    }

    [Fact]
    public void StaleFeed_ReferenceDateBeforeLastCandle_NotFlagged()
    {
        var candles = new[] { Candle(2026, 3, 5, 100m) };

        var issues = DataQualityChecker.CheckStaleFeed(candles, RefDate); // ref is before candle

        Assert.Empty(issues);
    }

    // ── CountTradingDays ──

    [Fact]
    public void CountTradingDays_SameDay_Zero()
    {
        var date = new DateTime(2026, 3, 2);
        Assert.Equal(0, DataQualityChecker.CountTradingDays(date, date));
    }

    [Fact]
    public void CountTradingDays_EndBeforeStart_Zero()
    {
        Assert.Equal(0, DataQualityChecker.CountTradingDays(
            new DateTime(2026, 3, 5), new DateTime(2026, 3, 2)));
    }

    [Fact]
    public void CountTradingDays_MondayToSaturday_FourTradingDays()
    {
        // Exclusive of start (Mon), inclusive of end (Sat=weekend, not counted)
        // Trading days: Tue, Wed, Thu, Fri = 4
        Assert.Equal(4, DataQualityChecker.CountTradingDays(
            new DateTime(2026, 2, 23), // Monday
            new DateTime(2026, 2, 28))); // Saturday
    }

    [Fact]
    public void CountTradingDays_FridayToMonday_OneTradingDay()
    {
        Assert.Equal(1, DataQualityChecker.CountTradingDays(
            new DateTime(2026, 2, 27), // Friday
            new DateTime(2026, 3, 2)));  // Monday
    }

    [Fact]
    public void CountTradingDays_WeekendOnly_Zero()
    {
        // Saturday to Sunday = 0 trading days
        Assert.Equal(0, DataQualityChecker.CountTradingDays(
            new DateTime(2026, 2, 28), // Saturday
            new DateTime(2026, 3, 1)));  // Sunday
    }

    [Fact]
    public void CountTradingDays_TwoFullWeeks_NineTradingDays()
    {
        // Exclusive of start (Mon 2/16), inclusive of end (Fri 2/27)
        // Trading days: Tue-Fri (4) + Mon-Fri (5) = 9
        Assert.Equal(9, DataQualityChecker.CountTradingDays(
            new DateTime(2026, 2, 16), // Monday
            new DateTime(2026, 2, 27))); // Friday
    }

    // ── Integration: Check with custom params ──

    [Fact]
    public void Check_CustomThresholds_AllApplied()
    {
        var candles = new[]
        {
            Candle(2026, 2, 25, 100m),      // Wednesday
            Candle(2026, 2, 26, 108m, 0),   // Thursday - 8% gap + zero vol
        };

        // Set price gap threshold to 5%, stale feed to 1
        var report = DataQualityChecker.Check(
            "TEST", candles, RefDate,
            priceGapThresholdPercent: 5m,
            staleFeedDays: 1);

        Assert.True(report.IsFlagged);
        Assert.True(report.PriceGapCount > 0);   // 8% > 5%
        Assert.True(report.ZeroVolumeCount > 0);  // zero volume day
        Assert.True(report.HasStaleFeed);          // 2 trading days > 1 threshold
    }

    [Theory]
    [InlineData(10, true)]   // 20% gap > 10% threshold → flagged
    [InlineData(19, true)]   // 20% gap > 19% threshold → flagged
    [InlineData(20, false)]  // 20% gap = 20% threshold → not flagged (strict >)
    [InlineData(25, false)]  // 20% gap < 25% threshold → not flagged
    public void PriceGaps_VariousThresholds(decimal threshold, bool shouldFlag)
    {
        var candles = new[]
        {
            Candle(2026, 2, 23, 100m),
            Candle(2026, 2, 24, 120m), // 20% gap
        };

        var issues = DataQualityChecker.CheckPriceGaps(candles, threshold);

        Assert.Equal(shouldFlag, issues.Count > 0);
    }

    [Theory]
    [InlineData(1, true)]  // 2 trading days > 1
    [InlineData(2, false)] // 2 trading days = 2 (not exceeded)
    [InlineData(3, false)] // 2 trading days < 3
    public void StaleFeed_VariousThresholds(int threshold, bool shouldFlag)
    {
        // Thursday data → Monday reference = 2 trading days
        var candles = new[] { Candle(2026, 2, 26, 100m) };

        var issues = DataQualityChecker.CheckStaleFeed(candles, RefDate, threshold);

        Assert.Equal(shouldFlag, issues.Count > 0);
    }

    [Fact]
    public void Check_UsesLastCandleForStaleFeedCheck()
    {
        // Multiple candles, last one is fresh
        var candles = new[]
        {
            Candle(2026, 2, 10, 100m), // old
            Candle(2026, 2, 20, 100m), // less old
            Candle(2026, 3, 2, 100m),  // today
        };

        var report = DataQualityChecker.Check("SYM", candles, RefDate);

        Assert.False(report.HasStaleFeed);
    }
}
