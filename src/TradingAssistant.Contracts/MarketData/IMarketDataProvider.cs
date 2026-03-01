namespace TradingAssistant.Contracts.MarketData;

/// <summary>
/// Abstraction for fetching historical market data from external providers.
/// </summary>
public interface IMarketDataProvider
{
    /// <summary>
    /// Fetches daily OHLCV candles for a symbol over the given date range.
    /// Returns adjusted close prices that account for splits and dividends.
    /// </summary>
    Task<IReadOnlyList<MarketCandle>> GetDailyCandlesAsync(
        string symbol,
        DateTime from,
        DateTime to,
        CancellationToken ct = default);
}

/// <summary>
/// A single OHLCV candle returned from a market data provider.
/// Uses adjusted close to account for stock splits and dividends.
/// </summary>
public record MarketCandle(
    DateTime Date,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal AdjustedClose,
    long Volume);
