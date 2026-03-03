using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TradingAssistant.Application.Indicators;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Domain.Intelligence.Enums;

namespace TradingAssistant.Application.Handlers.Intelligence;

/// <summary>
/// Extracts 40+ features from indicator state, market regime, and trade context
/// for ML model training. Features stored as compressed JSON with SHA256 hash.
/// </summary>
public static class FeatureExtractor
{
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Extract a full feature dictionary from available market/trade context.
    /// </summary>
    public static Dictionary<string, object> ExtractFeatures(
        IndicatorValues indicators,
        FeatureContext context)
    {
        var features = new Dictionary<string, object>();

        // ── Trend indicators (6) ─────────────────────────────────
        features["smaShort"] = indicators.SmaShort;
        features["smaMedium"] = indicators.SmaMedium;
        features["smaLong"] = indicators.SmaLong;
        features["emaShort"] = indicators.EmaShort;
        features["emaMedium"] = indicators.EmaMedium;
        features["emaLong"] = indicators.EmaLong;

        // ── Momentum indicators (6) ──────────────────────────────
        features["rsi"] = indicators.Rsi;
        features["macdLine"] = indicators.MacdLine;
        features["macdSignal"] = indicators.MacdSignal;
        features["macdHistogram"] = indicators.MacdHistogram;
        features["stochasticK"] = indicators.StochasticK;
        features["stochasticD"] = indicators.StochasticD;

        // ── Volatility indicators (6) ────────────────────────────
        features["atr"] = indicators.Atr;
        features["bollingerUpper"] = indicators.BollingerUpper;
        features["bollingerMiddle"] = indicators.BollingerMiddle;
        features["bollingerLower"] = indicators.BollingerLower;
        features["bollingerBandwidth"] = indicators.BollingerBandwidth;
        features["bollingerPercentB"] = indicators.BollingerPercentB;

        // ── Volume indicators (3) ────────────────────────────────
        features["obv"] = indicators.Obv;
        features["volumeMa"] = indicators.VolumeMa;
        features["relativeVolume"] = indicators.RelativeVolume;

        // ── Price context (5) ────────────────────────────────────
        features["closePrice"] = context.ClosePrice;
        features["highPrice"] = context.HighPrice;
        features["lowPrice"] = context.LowPrice;
        features["openPrice"] = context.OpenPrice;
        features["dailyRange"] = context.HighPrice - context.LowPrice;

        // ── Derived price features (4) ───────────────────────────
        features["priceToSmaShort"] = indicators.SmaShort > 0
            ? context.ClosePrice / indicators.SmaShort : 0m;
        features["priceToSmaLong"] = indicators.SmaLong > 0
            ? context.ClosePrice / indicators.SmaLong : 0m;
        features["priceToEmaShort"] = indicators.EmaShort > 0
            ? context.ClosePrice / indicators.EmaShort : 0m;
        features["atrPercent"] = context.ClosePrice > 0
            ? indicators.Atr / context.ClosePrice * 100m : 0m;

        // ── Regime context (5) ───────────────────────────────────
        features["regimeLabel"] = context.RegimeLabel;
        features["regimeConfidence"] = context.RegimeConfidence;
        features["daysSinceRegimeChange"] = context.DaysSinceRegimeChange;
        features["vixLevel"] = context.VixLevel;
        features["breadthScore"] = context.BreadthScore;

        // ── Market context (4) ───────────────────────────────────
        features["marketCode"] = context.MarketCode;
        features["symbol"] = context.Symbol;
        features["dayOfWeek"] = context.DayOfWeek;
        features["orderSide"] = context.OrderSide;

        // ── Trade context (6) ────────────────────────────────────
        features["winStreak"] = context.WinStreak;
        features["lossStreak"] = context.LossStreak;
        features["recentWinRate"] = context.RecentWinRate;
        features["portfolioHeat"] = context.PortfolioHeat;
        features["openPositionCount"] = context.OpenPositionCount;
        features["tradePrice"] = context.TradePrice;

        return features;
    }

    /// <summary>
    /// Compress feature dictionary to GZip+Base64 string.
    /// </summary>
    public static string CompressFeatures(Dictionary<string, object> features)
    {
        var json = JsonSerializer.Serialize(features, JsonOpts);
        var bytes = Encoding.UTF8.GetBytes(json);

        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
        {
            gzip.Write(bytes, 0, bytes.Length);
        }

        return Convert.ToBase64String(output.ToArray());
    }

    /// <summary>
    /// Decompress Base64+GZip string back to feature dictionary.
    /// </summary>
    public static Dictionary<string, object>? DecompressFeatures(string compressedBase64)
    {
        if (string.IsNullOrWhiteSpace(compressedBase64))
            return null;

        var compressed = Convert.FromBase64String(compressedBase64);

        using var input = new MemoryStream(compressed);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();

        gzip.CopyTo(output);
        var json = Encoding.UTF8.GetString(output.ToArray());

        return JsonSerializer.Deserialize<Dictionary<string, object>>(json, JsonOpts);
    }

    /// <summary>
    /// Compute SHA256 hex hash of the uncompressed feature JSON.
    /// </summary>
    public static string ComputeHash(Dictionary<string, object> features)
    {
        var json = JsonSerializer.Serialize(features, JsonOpts);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Context data gathered from multiple sources for feature extraction.
    /// </summary>
    public record FeatureContext(
        decimal ClosePrice,
        decimal HighPrice,
        decimal LowPrice,
        decimal OpenPrice,
        string RegimeLabel,
        decimal RegimeConfidence,
        int DaysSinceRegimeChange,
        decimal VixLevel,
        decimal BreadthScore,
        string MarketCode,
        string Symbol,
        int DayOfWeek,
        string OrderSide,
        int WinStreak,
        int LossStreak,
        decimal RecentWinRate,
        decimal PortfolioHeat,
        int OpenPositionCount,
        decimal TradePrice);
}
