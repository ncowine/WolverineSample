using System.Text.Json;
using Microsoft.ML.Data;

namespace TradingAssistant.Infrastructure.ML;

/// <summary>
/// ML.NET-compatible typed feature vector for trade prediction models.
/// All numeric features use float for ML.NET compatibility.
/// Property names match FeatureExtractor dictionary keys via [ColumnName].
/// </summary>
public class FeatureVector
{
    // ── Trend indicators (6) ─────────────────────────────────
    [ColumnName("smaShort")]
    public float SmaShort { get; set; }

    [ColumnName("smaMedium")]
    public float SmaMedium { get; set; }

    [ColumnName("smaLong")]
    public float SmaLong { get; set; }

    [ColumnName("emaShort")]
    public float EmaShort { get; set; }

    [ColumnName("emaMedium")]
    public float EmaMedium { get; set; }

    [ColumnName("emaLong")]
    public float EmaLong { get; set; }

    // ── Momentum indicators (6) ──────────────────────────────
    [ColumnName("rsi")]
    public float Rsi { get; set; }

    [ColumnName("macdLine")]
    public float MacdLine { get; set; }

    [ColumnName("macdSignal")]
    public float MacdSignal { get; set; }

    [ColumnName("macdHistogram")]
    public float MacdHistogram { get; set; }

    [ColumnName("stochasticK")]
    public float StochasticK { get; set; }

    [ColumnName("stochasticD")]
    public float StochasticD { get; set; }

    // ── Volatility indicators (6) ────────────────────────────
    [ColumnName("atr")]
    public float Atr { get; set; }

    [ColumnName("bollingerUpper")]
    public float BollingerUpper { get; set; }

    [ColumnName("bollingerMiddle")]
    public float BollingerMiddle { get; set; }

    [ColumnName("bollingerLower")]
    public float BollingerLower { get; set; }

    [ColumnName("bollingerBandwidth")]
    public float BollingerBandwidth { get; set; }

    [ColumnName("bollingerPercentB")]
    public float BollingerPercentB { get; set; }

    // ── Volume indicators (3) ────────────────────────────────
    [ColumnName("obv")]
    public float Obv { get; set; }

    [ColumnName("volumeMa")]
    public float VolumeMa { get; set; }

    [ColumnName("relativeVolume")]
    public float RelativeVolume { get; set; }

    // ── Price context (5) ────────────────────────────────────
    [ColumnName("closePrice")]
    public float ClosePrice { get; set; }

    [ColumnName("highPrice")]
    public float HighPrice { get; set; }

    [ColumnName("lowPrice")]
    public float LowPrice { get; set; }

    [ColumnName("openPrice")]
    public float OpenPrice { get; set; }

    [ColumnName("dailyRange")]
    public float DailyRange { get; set; }

    // ── Derived price features (4) ───────────────────────────
    [ColumnName("priceToSmaShort")]
    public float PriceToSmaShort { get; set; }

    [ColumnName("priceToSmaLong")]
    public float PriceToSmaLong { get; set; }

    [ColumnName("priceToEmaShort")]
    public float PriceToEmaShort { get; set; }

    [ColumnName("atrPercent")]
    public float AtrPercent { get; set; }

    // ── Regime context (5) ───────────────────────────────────
    [ColumnName("regimeLabel")]
    public string RegimeLabel { get; set; } = string.Empty;

    [ColumnName("regimeConfidence")]
    public float RegimeConfidence { get; set; }

    [ColumnName("daysSinceRegimeChange")]
    public float DaysSinceRegimeChange { get; set; }

    [ColumnName("vixLevel")]
    public float VixLevel { get; set; }

    [ColumnName("breadthScore")]
    public float BreadthScore { get; set; }

    // ── Market context (4) ───────────────────────────────────
    [ColumnName("marketCode")]
    public string MarketCode { get; set; } = string.Empty;

    [ColumnName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [ColumnName("dayOfWeek")]
    public float DayOfWeek { get; set; }

    [ColumnName("orderSide")]
    public string OrderSide { get; set; } = string.Empty;

    // ── Trade context (6) ────────────────────────────────────
    [ColumnName("winStreak")]
    public float WinStreak { get; set; }

    [ColumnName("lossStreak")]
    public float LossStreak { get; set; }

    [ColumnName("recentWinRate")]
    public float RecentWinRate { get; set; }

    [ColumnName("portfolioHeat")]
    public float PortfolioHeat { get; set; }

    [ColumnName("openPositionCount")]
    public float OpenPositionCount { get; set; }

    [ColumnName("tradePrice")]
    public float TradePrice { get; set; }

    // ── Label (for training) ─────────────────────────────────
    [ColumnName("label")]
    public bool Label { get; set; }

    /// <summary>
    /// Total number of feature columns (excludes Label).
    /// </summary>
    public const int FeatureCount = 45;

    /// <summary>
    /// Returns all feature column names matching FeatureExtractor dictionary keys.
    /// Kept in sync with [ColumnName] attributes on properties above.
    /// </summary>
    public static string[] GetFeatureColumnNames() => FeatureColumnNames;

    private static readonly string[] FeatureColumnNames =
    [
        // Trend (6)
        "smaShort", "smaMedium", "smaLong", "emaShort", "emaMedium", "emaLong",
        // Momentum (6)
        "rsi", "macdLine", "macdSignal", "macdHistogram", "stochasticK", "stochasticD",
        // Volatility (6)
        "atr", "bollingerUpper", "bollingerMiddle", "bollingerLower",
        "bollingerBandwidth", "bollingerPercentB",
        // Volume (3)
        "obv", "volumeMa", "relativeVolume",
        // Price context (5)
        "closePrice", "highPrice", "lowPrice", "openPrice", "dailyRange",
        // Derived price (4)
        "priceToSmaShort", "priceToSmaLong", "priceToEmaShort", "atrPercent",
        // Regime context (5)
        "regimeLabel", "regimeConfidence", "daysSinceRegimeChange", "vixLevel", "breadthScore",
        // Market context (4)
        "marketCode", "symbol", "dayOfWeek", "orderSide",
        // Trade context (6)
        "winStreak", "lossStreak", "recentWinRate", "portfolioHeat",
        "openPositionCount", "tradePrice"
    ];

    /// <summary>
    /// Convert a feature dictionary (from FeatureExtractor) into a typed FeatureVector.
    /// Missing keys default to 0/empty. Label set from tradeOutcome parameter.
    /// </summary>
    public static FeatureVector FromDictionary(
        Dictionary<string, object> features, bool label = false)
    {
        return new FeatureVector
        {
            // Trend
            SmaShort = GetFloat(features, "smaShort"),
            SmaMedium = GetFloat(features, "smaMedium"),
            SmaLong = GetFloat(features, "smaLong"),
            EmaShort = GetFloat(features, "emaShort"),
            EmaMedium = GetFloat(features, "emaMedium"),
            EmaLong = GetFloat(features, "emaLong"),
            // Momentum
            Rsi = GetFloat(features, "rsi"),
            MacdLine = GetFloat(features, "macdLine"),
            MacdSignal = GetFloat(features, "macdSignal"),
            MacdHistogram = GetFloat(features, "macdHistogram"),
            StochasticK = GetFloat(features, "stochasticK"),
            StochasticD = GetFloat(features, "stochasticD"),
            // Volatility
            Atr = GetFloat(features, "atr"),
            BollingerUpper = GetFloat(features, "bollingerUpper"),
            BollingerMiddle = GetFloat(features, "bollingerMiddle"),
            BollingerLower = GetFloat(features, "bollingerLower"),
            BollingerBandwidth = GetFloat(features, "bollingerBandwidth"),
            BollingerPercentB = GetFloat(features, "bollingerPercentB"),
            // Volume
            Obv = GetFloat(features, "obv"),
            VolumeMa = GetFloat(features, "volumeMa"),
            RelativeVolume = GetFloat(features, "relativeVolume"),
            // Price context
            ClosePrice = GetFloat(features, "closePrice"),
            HighPrice = GetFloat(features, "highPrice"),
            LowPrice = GetFloat(features, "lowPrice"),
            OpenPrice = GetFloat(features, "openPrice"),
            DailyRange = GetFloat(features, "dailyRange"),
            // Derived price
            PriceToSmaShort = GetFloat(features, "priceToSmaShort"),
            PriceToSmaLong = GetFloat(features, "priceToSmaLong"),
            PriceToEmaShort = GetFloat(features, "priceToEmaShort"),
            AtrPercent = GetFloat(features, "atrPercent"),
            // Regime context
            RegimeLabel = GetString(features, "regimeLabel"),
            RegimeConfidence = GetFloat(features, "regimeConfidence"),
            DaysSinceRegimeChange = GetFloat(features, "daysSinceRegimeChange"),
            VixLevel = GetFloat(features, "vixLevel"),
            BreadthScore = GetFloat(features, "breadthScore"),
            // Market context
            MarketCode = GetString(features, "marketCode"),
            Symbol = GetString(features, "symbol"),
            DayOfWeek = GetFloat(features, "dayOfWeek"),
            OrderSide = GetString(features, "orderSide"),
            // Trade context
            WinStreak = GetFloat(features, "winStreak"),
            LossStreak = GetFloat(features, "lossStreak"),
            RecentWinRate = GetFloat(features, "recentWinRate"),
            PortfolioHeat = GetFloat(features, "portfolioHeat"),
            OpenPositionCount = GetFloat(features, "openPositionCount"),
            TradePrice = GetFloat(features, "tradePrice"),
            // Label
            Label = label
        };
    }

    /// <summary>
    /// Validates that all expected feature names exist in the dictionary keys.
    /// Returns list of missing feature names (empty if schema matches).
    /// </summary>
    public static List<string> ValidateSchema(IEnumerable<string> dictionaryKeys)
    {
        var expected = GetFeatureColumnNames();
        var keySet = new HashSet<string>(dictionaryKeys);
        return expected.Where(name => !keySet.Contains(name)).ToList();
    }

    private static float GetFloat(Dictionary<string, object> features, string key)
    {
        if (!features.TryGetValue(key, out var value))
            return 0f;

        return value switch
        {
            float f => f,
            double d => (float)d,
            decimal m => (float)m,
            int i => i,
            long l => l,
            JsonElement je => je.ValueKind switch
            {
                JsonValueKind.Number => je.TryGetDecimal(out var dec) ? (float)dec : 0f,
                _ => 0f
            },
            _ => float.TryParse(value.ToString(), out var parsed) ? parsed : 0f
        };
    }

    private static string GetString(Dictionary<string, object> features, string key)
    {
        if (!features.TryGetValue(key, out var value))
            return string.Empty;

        return value switch
        {
            string s => s,
            JsonElement je => je.GetString() ?? string.Empty,
            _ => value.ToString() ?? string.Empty
        };
    }
}
