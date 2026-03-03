using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingAssistant.Application.Indicators;
using TradingAssistant.Contracts.Events;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Intelligence;

/// <summary>
/// Captures full indicator state at trade entry for ML feature store.
/// Triggered by OrderFilled event via Wolverine cascade.
/// </summary>
public class CaptureFeatureSnapshotHandler
{
    public const string DefaultMarketCode = "US_SP500";

    public static async Task HandleAsync(
        OrderFilled @event,
        MarketDataDbContext marketDb,
        TradingDbContext tradingDb,
        IntelligenceDbContext intelDb,
        ILogger<CaptureFeatureSnapshotHandler> logger)
    {
        try
        {
            var indicators = await GetLatestIndicators(@event.Symbol, marketDb);
            var regime = await GetCurrentRegime(DefaultMarketCode, intelDb);
            var tradeContext = await BuildTradeContext(
                @event, indicators != null, tradingDb, regime);

            var effectiveIndicators = indicators ?? new IndicatorValues();

            var features = FeatureExtractor.ExtractFeatures(effectiveIndicators, tradeContext);
            var compressed = FeatureExtractor.CompressFeatures(features);
            var hash = FeatureExtractor.ComputeHash(features);

            var snapshot = new FeatureSnapshot
            {
                TradeId = @event.OrderId,
                Symbol = @event.Symbol,
                MarketCode = DefaultMarketCode,
                CapturedAt = DateTime.UtcNow,
                FeatureVersion = FeatureExtractor.CurrentVersion,
                FeatureCount = features.Count,
                FeaturesJson = compressed,
                FeaturesHash = hash,
                TradeOutcome = TradeOutcome.Pending
            };

            intelDb.FeatureSnapshots.Add(snapshot);
            await intelDb.SaveChangesAsync();

            logger.LogInformation(
                "Feature snapshot captured for {Symbol} trade {OrderId}: {Count} features, v{Version}",
                @event.Symbol, @event.OrderId, features.Count, FeatureExtractor.CurrentVersion);
        }
        catch (Exception ex)
        {
            // Don't fail the trade cascade; log and continue
            logger.LogWarning(ex,
                "Failed to capture feature snapshot for {Symbol} trade {OrderId}",
                @event.Symbol, @event.OrderId);
        }
    }

    internal static async Task<IndicatorValues?> GetLatestIndicators(
        string symbol, MarketDataDbContext marketDb)
    {
        var stock = await marketDb.Stocks
            .FirstOrDefaultAsync(s => s.Symbol == symbol);

        if (stock is null) return null;

        var indicators = await marketDb.TechnicalIndicators
            .Where(i => i.StockId == stock.Id)
            .OrderByDescending(i => i.Timestamp)
            .Take(20) // Get latest values for each indicator type
            .ToListAsync();

        if (indicators.Count == 0) return null;

        var values = new IndicatorValues { IsWarmedUp = true };

        foreach (var ind in indicators)
        {
            switch (ind.IndicatorType)
            {
                case IndicatorType.RSI:
                    values.Rsi = ind.Value;
                    break;
                case IndicatorType.SMA:
                    if (values.SmaShort == 0) values.SmaShort = ind.Value;
                    else if (values.SmaMedium == 0) values.SmaMedium = ind.Value;
                    else if (values.SmaLong == 0) values.SmaLong = ind.Value;
                    break;
                case IndicatorType.EMA:
                    if (values.EmaShort == 0) values.EmaShort = ind.Value;
                    else if (values.EmaMedium == 0) values.EmaMedium = ind.Value;
                    else if (values.EmaLong == 0) values.EmaLong = ind.Value;
                    break;
                case IndicatorType.MACD:
                    values.MacdLine = ind.Value;
                    break;
                case IndicatorType.BollingerBands:
                    values.BollingerMiddle = ind.Value;
                    break;
                case IndicatorType.ATR:
                    values.Atr = ind.Value;
                    break;
                case IndicatorType.Stochastic:
                    values.StochasticK = ind.Value;
                    break;
                case IndicatorType.OBV:
                    values.Obv = ind.Value;
                    break;
            }
        }

        return values;
    }

    internal static async Task<(string Label, decimal Confidence, int DaysSinceChange,
        decimal VixLevel, decimal BreadthScore)> GetCurrentRegime(
        string marketCode, IntelligenceDbContext intelDb)
    {
        var regime = await intelDb.MarketRegimes
            .Where(r => r.MarketCode == marketCode)
            .OrderByDescending(r => r.ClassifiedAt)
            .FirstOrDefaultAsync();

        if (regime is null)
            return ("Unknown", 0m, 0, 0m, 0m);

        var daysSince = (int)(DateTime.UtcNow - regime.RegimeStartDate).TotalDays;
        return (regime.CurrentRegime.ToString(), regime.ConfidenceScore,
            daysSince, regime.VixLevel, regime.BreadthScore);
    }

    private static async Task<FeatureExtractor.FeatureContext> BuildTradeContext(
        OrderFilled @event,
        bool hasIndicators,
        TradingDbContext tradingDb,
        (string Label, decimal Confidence, int DaysSinceChange,
            decimal VixLevel, decimal BreadthScore) regime)
    {
        // Get recent closed positions for win/loss streak
        var recentPositions = await tradingDb.Positions
            .Where(p => p.AccountId == @event.AccountId && p.Status == PositionStatus.Closed)
            .OrderByDescending(p => p.ClosedAt)
            .Take(20)
            .ToListAsync();

        var (winStreak, lossStreak) = ComputeStreaks(recentPositions);
        var recentWinRate = ComputeRecentWinRate(recentPositions);

        // Portfolio heat: open position value / total account value
        var account = await tradingDb.Accounts.FindAsync(@event.AccountId);
        var openPositions = await tradingDb.Positions
            .Where(p => p.AccountId == @event.AccountId && p.Status == PositionStatus.Open)
            .ToListAsync();

        var totalPositionValue = openPositions.Sum(p => p.Quantity * p.CurrentPrice);
        var totalEquity = (account?.Balance ?? 0) + totalPositionValue;
        var portfolioHeat = totalEquity > 0 ? totalPositionValue / totalEquity : 0m;

        // Use trade price for OHLC approximation when no candle data available
        var price = @event.Price;

        return new FeatureExtractor.FeatureContext(
            ClosePrice: price,
            HighPrice: price,
            LowPrice: price,
            OpenPrice: price,
            RegimeLabel: regime.Label,
            RegimeConfidence: regime.Confidence,
            DaysSinceRegimeChange: regime.DaysSinceChange,
            VixLevel: regime.VixLevel,
            BreadthScore: regime.BreadthScore,
            MarketCode: CaptureFeatureSnapshotHandler.DefaultMarketCode,
            Symbol: @event.Symbol,
            DayOfWeek: (int)DateTime.UtcNow.DayOfWeek,
            OrderSide: @event.Quantity > 0 ? "Buy" : "Sell",
            WinStreak: winStreak,
            LossStreak: lossStreak,
            RecentWinRate: recentWinRate,
            PortfolioHeat: portfolioHeat,
            OpenPositionCount: openPositions.Count,
            TradePrice: price);
    }

    internal static (int WinStreak, int LossStreak) ComputeStreaks(
        List<Domain.Trading.Position> recentPositions)
    {
        int winStreak = 0, lossStreak = 0;

        foreach (var pos in recentPositions)
        {
            var pnl = (pos.CurrentPrice - pos.AverageEntryPrice) * pos.Quantity;
            if (pnl > 0)
            {
                if (lossStreak > 0) break;
                winStreak++;
            }
            else
            {
                if (winStreak > 0) break;
                lossStreak++;
            }
        }

        return (winStreak, lossStreak);
    }

    internal static decimal ComputeRecentWinRate(
        List<Domain.Trading.Position> recentPositions)
    {
        if (recentPositions.Count == 0) return 0m;

        var wins = recentPositions.Count(p =>
            (p.CurrentPrice - p.AverageEntryPrice) * p.Quantity > 0);

        return (decimal)wins / recentPositions.Count;
    }
}
