using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application.Indicators;
using TradingAssistant.Application.Intelligence;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Events;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Intelligence;

public class ClassifyRegimeHandler
{
    public static async Task<(RegimeChanged?, MarketRegimeDto)> HandleAsync(
        ClassifyRegimeCommand command,
        MarketDataDbContext marketDb,
        IntelligenceDbContext intelligenceDb)
    {
        var classificationDate = command.Date?.Date ?? DateTime.UtcNow.Date;

        // 1. Load market profile for thresholds
        var profile = await intelligenceDb.MarketProfiles
            .FirstOrDefaultAsync(p => p.MarketCode == command.MarketCode);

        var thresholds = RegimeThresholds.FromConfigJson(profile?.ConfigJson);

        // 2. Get latest breadth snapshot for this market (on or before classification date)
        var breadth = await intelligenceDb.BreadthSnapshots
            .Where(b => b.MarketCode == command.MarketCode && b.SnapshotDate <= classificationDate)
            .OrderByDescending(b => b.SnapshotDate)
            .FirstOrDefaultAsync();

        // 3. Compute SMA slopes from price data
        //    We need a representative index — use the first stock universe that matches this market
        var smaSlope50 = 0m;
        var smaSlope200 = 0m;

        // Load all daily candles for stocks in the universe (last 250 trading days)
        var cutoffDate = classificationDate.AddDays(-370); // ~250 trading days with buffer
        var stockCandles = await marketDb.PriceCandles
            .Where(c => c.Interval == CandleInterval.Daily
                     && c.Timestamp >= cutoffDate
                     && c.Timestamp <= classificationDate)
            .GroupBy(c => c.StockId)
            .Select(g => new
            {
                StockId = g.Key,
                Closes = g.OrderBy(c => c.Timestamp).Select(c => c.Close).ToList()
            })
            .ToListAsync();

        if (stockCandles.Count > 0)
        {
            // Compute average SMA slope across all stocks as a market-level signal
            var slopes50 = new List<decimal>();
            var slopes200 = new List<decimal>();

            foreach (var stock in stockCandles)
            {
                var closes = stock.Closes.ToArray();

                if (closes.Length >= 70) // 50 SMA + 20 lookback
                {
                    var sma50 = SmaCalculator.Instance.Calculate(closes, 50);
                    slopes50.Add(RegimeClassifier.ComputeSmaSlope(sma50));
                }

                if (closes.Length >= 220) // 200 SMA + 20 lookback
                {
                    var sma200 = SmaCalculator.Instance.Calculate(closes, 200);
                    slopes200.Add(RegimeClassifier.ComputeSmaSlope(sma200));
                }
            }

            if (slopes50.Count > 0)
                smaSlope50 = slopes50.Average();
            if (slopes200.Count > 0)
                smaSlope200 = slopes200.Average();
        }

        // 4. Get VIX level (if available as a tracked symbol)
        var vixLevel = 0m;
        if (profile?.VixSymbol != null)
        {
            var vixStock = await marketDb.Stocks
                .FirstOrDefaultAsync(s => s.Symbol == profile.VixSymbol);

            if (vixStock != null)
            {
                var latestVix = await marketDb.PriceCandles
                    .Where(c => c.StockId == vixStock.Id
                             && c.Interval == CandleInterval.Daily
                             && c.Timestamp <= classificationDate)
                    .OrderByDescending(c => c.Timestamp)
                    .FirstOrDefaultAsync();

                if (latestVix != null)
                    vixLevel = latestVix.Close;
            }
        }

        // 5. Build inputs and classify
        var inputs = new RegimeInputs(
            SmaSlope50: smaSlope50,
            SmaSlope200: smaSlope200,
            VixLevel: vixLevel,
            PctAbove200Sma: breadth?.PctAbove200Sma ?? 0.50m,
            PctAbove50Sma: breadth?.PctAbove50Sma ?? 0.50m,
            AdvanceDeclineRatio: breadth?.AdvanceDeclineRatio ?? 1.0m);

        var (regime, confidence) = RegimeClassifier.Classify(inputs, thresholds);

        var breadthScore = breadth != null
            ? RegimeClassifier.ComputeBreadthScore(breadth)
            : 50m;

        // 6. Load previous regime (if any) to detect transitions
        var previousRegime = await intelligenceDb.MarketRegimes
            .Where(r => r.MarketCode == command.MarketCode)
            .OrderByDescending(r => r.ClassifiedAt)
            .FirstOrDefaultAsync();

        var isTransition = previousRegime != null && previousRegime.CurrentRegime != regime;
        var regimeStartDate = isTransition || previousRegime == null
            ? classificationDate
            : previousRegime.RegimeStartDate;

        var duration = (int)(classificationDate - regimeStartDate).TotalDays;

        // 7. Create new MarketRegime record
        var marketRegime = new MarketRegime
        {
            MarketCode = command.MarketCode,
            CurrentRegime = regime,
            RegimeStartDate = regimeStartDate,
            RegimeDuration = duration,
            SmaSlope50 = Math.Round(smaSlope50, 6),
            SmaSlope200 = Math.Round(smaSlope200, 6),
            VixLevel = Math.Round(vixLevel, 2),
            BreadthScore = breadthScore,
            PctAbove200Sma = breadth?.PctAbove200Sma ?? 0.50m,
            AdvanceDeclineRatio = breadth?.AdvanceDeclineRatio ?? 1.0m,
            ClassifiedAt = classificationDate,
            ConfidenceScore = confidence
        };

        intelligenceDb.MarketRegimes.Add(marketRegime);

        // 8. Log transition if regime changed
        RegimeChanged? regimeChangedEvent = null;

        if (isTransition)
        {
            var transition = new RegimeTransition
            {
                MarketCode = command.MarketCode,
                FromRegime = previousRegime!.CurrentRegime,
                ToRegime = regime,
                TransitionDate = classificationDate,
                SmaSlope50 = Math.Round(smaSlope50, 6),
                SmaSlope200 = Math.Round(smaSlope200, 6),
                VixLevel = Math.Round(vixLevel, 2),
                BreadthScore = breadthScore,
                PctAbove200Sma = breadth?.PctAbove200Sma ?? 0.50m
            };

            intelligenceDb.RegimeTransitions.Add(transition);

            regimeChangedEvent = new RegimeChanged(
                command.MarketCode,
                previousRegime.CurrentRegime.ToString(),
                regime.ToString(),
                classificationDate,
                confidence);
        }

        await intelligenceDb.SaveChangesAsync();

        // 9. Return cascading event (null if no transition) + DTO
        var dto = new MarketRegimeDto(
            marketRegime.Id,
            marketRegime.MarketCode,
            marketRegime.CurrentRegime.ToString(),
            marketRegime.RegimeStartDate,
            marketRegime.RegimeDuration,
            marketRegime.SmaSlope50,
            marketRegime.SmaSlope200,
            marketRegime.VixLevel,
            marketRegime.BreadthScore,
            marketRegime.PctAbove200Sma,
            marketRegime.AdvanceDeclineRatio,
            marketRegime.ConfidenceScore,
            marketRegime.ClassifiedAt);

        return (regimeChangedEvent, dto);
    }
}
