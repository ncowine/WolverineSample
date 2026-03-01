using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application.Screening;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Domain.MarketData;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Screening;

public class RunScreenerHandler
{
    /// <summary>
    /// Persist a screener run result to the database and return a DTO.
    /// Also purges results older than 30 days.
    /// </summary>
    public static async Task<ScreenerResultsDto> HandleAsync(
        ScreenerRunResult scanResult,
        MarketDataDbContext db)
    {
        var signals = scanResult.Results.Select(r => new ScreenerSignalDto(
            r.Symbol, r.Grade.ToString(), r.Score, r.Direction.ToString(),
            r.EntryPrice, r.StopPrice, r.TargetPrice, r.RiskRewardRatio,
            r.HistoricalWinRate, r.SignalDate,
            r.Breakdown.Select(b => new ScreenerBreakdownEntryDto(
                b.Factor, b.RawScore, b.Weight, b.WeightedScore, b.Reason)).ToList()
        )).ToList();

        var warnings = scanResult.Warnings;

        var entity = new ScreenerRun
        {
            Id = Guid.NewGuid(),
            ScanDate = scanResult.ScanDate,
            StrategyName = scanResult.StrategyName,
            SymbolsScanned = scanResult.SymbolsScanned,
            SignalsFound = scanResult.SignalsFound,
            ResultsJson = JsonSerializer.Serialize(signals),
            WarningsJson = JsonSerializer.Serialize(warnings),
            ElapsedTime = scanResult.ElapsedTime,
            CreatedAt = DateTime.UtcNow
        };

        db.ScreenerRuns.Add(entity);

        // Purge old results (> 30 days)
        var cutoff = DateTime.UtcNow.AddDays(-30);
        var oldRuns = await db.ScreenerRuns
            .Where(r => r.ScanDate < cutoff)
            .ToListAsync();
        if (oldRuns.Count > 0)
            db.ScreenerRuns.RemoveRange(oldRuns);

        await db.SaveChangesAsync();

        return new ScreenerResultsDto(
            entity.Id, entity.ScanDate, entity.StrategyName,
            entity.SymbolsScanned, entity.SignalsFound,
            signals.Count, signals, warnings);
    }
}
