using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application.Intelligence;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Intelligence;

/// <summary>
/// Lightweight per-stock regime detection using only price candles.
/// Does not require breadth data, VIX, or market profiles — works for any symbol
/// with at least 200 daily candles.
///
/// Classification:
///   1. HighVolatility — ATR/Price ratio > 3% (normalized daily volatility)
///   2. Bull          — SMA50 > SMA200 AND SMA50 slope positive
///   3. Bear          — SMA50 &lt; SMA200 AND SMA50 slope negative (recommends MeanReversion)
///   4. Sideways      — default fallback
///
/// Maps regimes to PlaybookGenerator templates:
///   Bull          → Momentum
///   Bear/Sideways → MeanReversion
///   HighVol       → Breakout
/// </summary>
public class DetectStockRegimeHandler
{
    private const int MinBars = 50;
    private const decimal HighVolThreshold = 0.03m; // 3% daily ATR/Price ratio

    public static async Task<StockRegimeDto> HandleAsync(
        DetectStockRegimeQuery query,
        MarketDataDbContext db)
    {
        var symbol = query.Symbol.Trim().ToUpperInvariant();

        var stock = await db.Stocks
            .FirstOrDefaultAsync(s => s.Symbol == symbol && s.IsActive);

        if (stock is null)
        {
            return new StockRegimeDto(
                symbol, "Unknown", "Momentum", 0m, 0m, 0m, 0m,
                $"No market data for '{symbol}'. Seed data first, then try again.");
        }

        var candles = await db.PriceCandles
            .Where(c => c.StockId == stock.Id && c.Interval == CandleInterval.Daily)
            .OrderBy(c => c.Timestamp)
            .Select(c => c.Close)
            .ToArrayAsync();

        if (candles.Length < MinBars)
        {
            return new StockRegimeDto(
                symbol, "Unknown", "Momentum", 0m, 0m, 0m, 0m,
                $"Only {candles.Length} bars available (need {MinBars}+). Using Momentum as default.");
        }

        // Compute SMA 50
        var sma50 = ComputeSma(candles, 50);
        // Compute SMA 200 (if enough data, otherwise use SMA50 as proxy)
        var sma200 = candles.Length >= 200 ? ComputeSma(candles, 200) : sma50;

        // Compute slopes using the existing RegimeClassifier utility
        var slope50 = sma50.Length > 20 ? RegimeClassifier.ComputeSmaSlope(sma50) : 0m;
        var slope200 = sma200.Length > 20 ? RegimeClassifier.ComputeSmaSlope(sma200) : 0m;

        // Compute volatility ratio: ATR(14) / current price
        var atrRatio = ComputeAtrRatio(candles);

        // Classify
        string regime;
        string template;
        decimal confidence;
        string explanation;

        if (atrRatio > HighVolThreshold)
        {
            regime = "HighVolatility";
            template = "Breakout";
            confidence = Math.Min(1m, 0.6m + (atrRatio - HighVolThreshold) / HighVolThreshold * 0.4m);
            explanation = $"{symbol} shows high volatility (ATR ratio {atrRatio:P1}). Breakout strategies capture explosive moves.";
        }
        else if (slope50 > 0 && (sma50.Length < 1 || sma200.Length < 1 || sma50[^1] > sma200[^1]))
        {
            regime = "Bull";
            template = "Momentum";
            confidence = Math.Min(1m, 0.5m + Math.Abs(slope50) * 100m * 0.5m);
            explanation = $"{symbol} is in an uptrend (SMA50 above SMA200, positive slope). Momentum strategies ride the trend.";
        }
        else if (slope50 < 0 && sma50.Length > 0 && sma200.Length > 0 && sma50[^1] < sma200[^1])
        {
            regime = "Bear";
            template = "MeanReversion";
            confidence = Math.Min(1m, 0.5m + Math.Abs(slope50) * 100m * 0.5m);
            explanation = $"{symbol} is in a downtrend (SMA50 below SMA200). Mean reversion buys dips for recovery bounces.";
        }
        else
        {
            regime = "Sideways";
            template = "MeanReversion";
            confidence = 0.6m;
            explanation = $"{symbol} is range-bound with no strong trend. Mean reversion profits from oscillations.";
        }

        return new StockRegimeDto(
            symbol, regime, template,
            Math.Round(confidence, 2),
            Math.Round(slope50, 6),
            Math.Round(slope200, 6),
            Math.Round(atrRatio, 4),
            explanation);
    }

    private static decimal[] ComputeSma(decimal[] closes, int period)
    {
        if (closes.Length < period) return [];

        var result = new decimal[closes.Length - period + 1];
        var sum = 0m;
        for (var i = 0; i < period; i++)
            sum += closes[i];

        result[0] = sum / period;
        for (var i = period; i < closes.Length; i++)
        {
            sum += closes[i] - closes[i - period];
            result[i - period + 1] = sum / period;
        }

        return result;
    }

    private static decimal ComputeAtrRatio(decimal[] closes, int period = 14)
    {
        if (closes.Length < period + 1) return 0m;

        // Simplified ATR using close-to-close absolute changes
        var sum = 0m;
        var start = closes.Length - period - 1;
        for (var i = start + 1; i < closes.Length; i++)
        {
            var range = Math.Abs(closes[i] - closes[i - 1]);
            sum += range;
        }

        var atr = sum / period;
        var currentPrice = closes[^1];
        return currentPrice > 0 ? atr / currentPrice : 0m;
    }
}
