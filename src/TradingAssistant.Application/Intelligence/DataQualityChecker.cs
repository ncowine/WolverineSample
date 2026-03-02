namespace TradingAssistant.Application.Intelligence;

/// <summary>
/// Lightweight candle data input for quality checks.
/// Decoupled from the EF PriceCandle entity.
/// </summary>
public record CandleData(
    DateTime Date,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume);

/// <summary>
/// Types of data quality anomalies detected.
/// </summary>
public enum DataQualityIssueType
{
    MissingTradingDay,
    PriceGap,
    ZeroVolume,
    StaleFeed
}

/// <summary>
/// A single data quality issue detected for a symbol.
/// </summary>
public record DataQualityIssue(
    DataQualityIssueType Type,
    DateTime Date,
    string Detail);

/// <summary>
/// Aggregated quality report for a single symbol.
/// </summary>
public record DataQualityReport(
    string Symbol,
    IReadOnlyList<DataQualityIssue> Issues)
{
    /// <summary>
    /// True if the symbol has any quality issues and should be flagged/skipped.
    /// </summary>
    public bool IsFlagged => Issues.Count > 0;

    public int MissingDayCount => Issues.Count(i => i.Type == DataQualityIssueType.MissingTradingDay);
    public int PriceGapCount => Issues.Count(i => i.Type == DataQualityIssueType.PriceGap);
    public int ZeroVolumeCount => Issues.Count(i => i.Type == DataQualityIssueType.ZeroVolume);
    public bool HasStaleFeed => Issues.Any(i => i.Type == DataQualityIssueType.StaleFeed);
}

/// <summary>
/// Pure static data quality checker.
///
/// Detects anomalies in OHLCV candle data:
/// - Missing trading days (gaps > 3 calendar days, allowing for weekends)
/// - Price gaps exceeding a configurable threshold (default 20%)
/// - Zero-volume trading days
/// - Stale feeds (most recent candle older than threshold trading days)
/// </summary>
public static class DataQualityChecker
{
    public const decimal DefaultPriceGapThresholdPercent = 20m;
    public const int DefaultStaleFeedDays = 2;
    public const int DefaultMaxCalendarDayGap = 3;

    /// <summary>
    /// Run all quality checks on a symbol's candle data.
    /// Candles should be sorted by date ascending.
    /// </summary>
    /// <param name="symbol">Ticker symbol being checked.</param>
    /// <param name="candles">OHLCV candle data sorted by date ascending.</param>
    /// <param name="referenceDate">Current date to check stale feed against.</param>
    /// <param name="priceGapThresholdPercent">Max allowed close-to-close gap (default 20%).</param>
    /// <param name="staleFeedDays">Max trading days since last candle before flagging stale (default 2).</param>
    /// <param name="maxCalendarDayGap">Max calendar days between candles before flagging missing day (default 3).</param>
    public static DataQualityReport Check(
        string symbol,
        IReadOnlyList<CandleData> candles,
        DateTime referenceDate,
        decimal priceGapThresholdPercent = DefaultPriceGapThresholdPercent,
        int staleFeedDays = DefaultStaleFeedDays,
        int maxCalendarDayGap = DefaultMaxCalendarDayGap)
    {
        if (candles.Count == 0)
        {
            return new DataQualityReport(symbol, new[]
            {
                new DataQualityIssue(
                    DataQualityIssueType.StaleFeed,
                    referenceDate,
                    $"No candle data available for {symbol}")
            });
        }

        var issues = new List<DataQualityIssue>();

        issues.AddRange(CheckMissingTradingDays(candles, maxCalendarDayGap));
        issues.AddRange(CheckPriceGaps(candles, priceGapThresholdPercent));
        issues.AddRange(CheckZeroVolumeDays(candles));
        issues.AddRange(CheckStaleFeed(candles, referenceDate, staleFeedDays));

        return new DataQualityReport(symbol, issues);
    }

    /// <summary>
    /// Detect gaps between consecutive trading days exceeding the calendar day threshold.
    /// A gap of 3+ calendar days (accounting for weekends) suggests missing data.
    /// </summary>
    public static IReadOnlyList<DataQualityIssue> CheckMissingTradingDays(
        IReadOnlyList<CandleData> candles,
        int maxCalendarDayGap = DefaultMaxCalendarDayGap)
    {
        var issues = new List<DataQualityIssue>();

        for (var i = 1; i < candles.Count; i++)
        {
            var gap = (candles[i].Date - candles[i - 1].Date).Days;
            if (gap > maxCalendarDayGap)
            {
                issues.Add(new DataQualityIssue(
                    DataQualityIssueType.MissingTradingDay,
                    candles[i].Date,
                    $"Gap of {gap} calendar days between {candles[i - 1].Date:yyyy-MM-dd} and {candles[i].Date:yyyy-MM-dd}"));
            }
        }

        return issues;
    }

    /// <summary>
    /// Detect close-to-close price gaps exceeding the threshold percentage.
    /// Gap% = |close[i] - close[i-1]| / close[i-1] * 100
    /// </summary>
    public static IReadOnlyList<DataQualityIssue> CheckPriceGaps(
        IReadOnlyList<CandleData> candles,
        decimal priceGapThresholdPercent = DefaultPriceGapThresholdPercent)
    {
        var issues = new List<DataQualityIssue>();

        for (var i = 1; i < candles.Count; i++)
        {
            var prevClose = candles[i - 1].Close;
            if (prevClose == 0) continue;

            var gapPercent = Math.Abs(candles[i].Close - prevClose) / prevClose * 100m;
            if (gapPercent > priceGapThresholdPercent)
            {
                issues.Add(new DataQualityIssue(
                    DataQualityIssueType.PriceGap,
                    candles[i].Date,
                    $"Price gap {gapPercent:F1}% from {prevClose:F2} to {candles[i].Close:F2} exceeds {priceGapThresholdPercent}% threshold"));
            }
        }

        return issues;
    }

    /// <summary>
    /// Detect trading days with zero volume.
    /// </summary>
    public static IReadOnlyList<DataQualityIssue> CheckZeroVolumeDays(
        IReadOnlyList<CandleData> candles)
    {
        var issues = new List<DataQualityIssue>();

        foreach (var candle in candles)
        {
            if (candle.Volume == 0)
            {
                issues.Add(new DataQualityIssue(
                    DataQualityIssueType.ZeroVolume,
                    candle.Date,
                    $"Zero volume on {candle.Date:yyyy-MM-dd} (Close={candle.Close:F2})"));
            }
        }

        return issues;
    }

    /// <summary>
    /// Detect stale feeds where the most recent candle is older than the threshold.
    /// Uses business/trading days (excludes weekends) for the gap calculation.
    /// </summary>
    public static IReadOnlyList<DataQualityIssue> CheckStaleFeed(
        IReadOnlyList<CandleData> candles,
        DateTime referenceDate,
        int staleFeedDays = DefaultStaleFeedDays)
    {
        if (candles.Count == 0)
        {
            return new[]
            {
                new DataQualityIssue(
                    DataQualityIssueType.StaleFeed,
                    referenceDate,
                    "No candle data available")
            };
        }

        var lastCandleDate = candles[^1].Date.Date;
        var refDate = referenceDate.Date;

        var tradingDaysGap = CountTradingDays(lastCandleDate, refDate);

        if (tradingDaysGap > staleFeedDays)
        {
            return new[]
            {
                new DataQualityIssue(
                    DataQualityIssueType.StaleFeed,
                    referenceDate,
                    $"Last candle on {lastCandleDate:yyyy-MM-dd} is {tradingDaysGap} trading days behind reference date {refDate:yyyy-MM-dd} (threshold: {staleFeedDays})")
            };
        }

        return Array.Empty<DataQualityIssue>();
    }

    /// <summary>
    /// Count trading days (Mon-Fri) between two dates, exclusive of start, inclusive of end.
    /// Returns 0 if end &lt;= start.
    /// </summary>
    public static int CountTradingDays(DateTime startDate, DateTime endDate)
    {
        var start = startDate.Date;
        var end = endDate.Date;

        if (end <= start) return 0;

        var count = 0;
        var current = start.AddDays(1);
        while (current <= end)
        {
            if (current.DayOfWeek != DayOfWeek.Saturday &&
                current.DayOfWeek != DayOfWeek.Sunday)
            {
                count++;
            }
            current = current.AddDays(1);
        }

        return count;
    }
}
