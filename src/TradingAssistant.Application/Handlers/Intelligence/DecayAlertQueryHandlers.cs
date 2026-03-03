using Microsoft.EntityFrameworkCore;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Intelligence;

public class GetDecayAlertsHandler
{
    public static async Task<IReadOnlyList<StrategyDecayAlertDto>> HandleAsync(
        GetDecayAlertsQuery query, IntelligenceDbContext db)
    {
        var q = db.StrategyDecayAlerts.AsQueryable();

        if (!query.IncludeResolved)
            q = q.Where(a => !a.IsResolved);

        if (!string.IsNullOrWhiteSpace(query.MarketCode))
            q = q.Where(a => a.MarketCode == query.MarketCode);

        var alerts = await q
            .OrderByDescending(a => a.AlertedAt)
            .ToListAsync();

        return alerts.Select(a => new StrategyDecayAlertDto(
            Id: a.Id,
            StrategyId: a.StrategyId,
            StrategyName: a.StrategyName,
            MarketCode: a.MarketCode,
            AlertType: a.AlertType.ToString(),
            Rolling30DaySharpe: a.Rolling30DaySharpe,
            Rolling60DaySharpe: a.Rolling60DaySharpe,
            Rolling90DaySharpe: a.Rolling90DaySharpe,
            Rolling30DayWinRate: a.Rolling30DayWinRate,
            Rolling60DayWinRate: a.Rolling60DayWinRate,
            Rolling90DayWinRate: a.Rolling90DayWinRate,
            Rolling30DayAvgPnl: a.Rolling30DayAvgPnl,
            Rolling60DayAvgPnl: a.Rolling60DayAvgPnl,
            Rolling90DayAvgPnl: a.Rolling90DayAvgPnl,
            HistoricalSharpe: a.HistoricalSharpe,
            TriggerReason: a.TriggerReason,
            ClaudeAnalysis: a.ClaudeAnalysis,
            IsResolved: a.IsResolved,
            ResolvedAt: a.ResolvedAt,
            ResolutionNote: a.ResolutionNote,
            AlertedAt: a.AlertedAt
        )).ToList();
    }
}

public class ResolveDecayAlertHandler
{
    public static async Task<StrategyDecayAlertDto?> HandleAsync(
        ResolveDecayAlertCommand command, IntelligenceDbContext db)
    {
        var alert = await db.StrategyDecayAlerts.FindAsync(command.AlertId);
        if (alert is null)
            return null;

        alert.IsResolved = true;
        alert.ResolvedAt = DateTime.UtcNow;
        alert.ResolutionNote = command.Note;
        alert.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return new StrategyDecayAlertDto(
            Id: alert.Id,
            StrategyId: alert.StrategyId,
            StrategyName: alert.StrategyName,
            MarketCode: alert.MarketCode,
            AlertType: alert.AlertType.ToString(),
            Rolling30DaySharpe: alert.Rolling30DaySharpe,
            Rolling60DaySharpe: alert.Rolling60DaySharpe,
            Rolling90DaySharpe: alert.Rolling90DaySharpe,
            Rolling30DayWinRate: alert.Rolling30DayWinRate,
            Rolling60DayWinRate: alert.Rolling60DayWinRate,
            Rolling90DayWinRate: alert.Rolling90DayWinRate,
            Rolling30DayAvgPnl: alert.Rolling30DayAvgPnl,
            Rolling60DayAvgPnl: alert.Rolling60DayAvgPnl,
            Rolling90DayAvgPnl: alert.Rolling90DayAvgPnl,
            HistoricalSharpe: alert.HistoricalSharpe,
            TriggerReason: alert.TriggerReason,
            ClaudeAnalysis: alert.ClaudeAnalysis,
            IsResolved: alert.IsResolved,
            ResolvedAt: alert.ResolvedAt,
            ResolutionNote: alert.ResolutionNote,
            AlertedAt: alert.AlertedAt);
    }
}
