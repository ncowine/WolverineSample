using Microsoft.EntityFrameworkCore;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Intelligence;

public class GetActiveStrategiesHandler
{
    public static async Task<IReadOnlyList<ActiveStrategyDto>> HandleAsync(
        GetActiveStrategiesQuery query,
        IntelligenceDbContext db)
    {
        var promoted = await db.TournamentEntries
            .Where(e => e.MarketCode == query.MarketCode
                     && e.Status == TournamentStatus.Promoted)
            .OrderByDescending(e => e.SharpeRatio)
            .ToListAsync();

        return promoted.Select(e => new ActiveStrategyDto(
            EntryId: e.Id,
            StrategyId: e.StrategyId,
            StrategyName: e.StrategyName,
            MarketCode: e.MarketCode,
            AllocationPercent: e.AllocationPercent,
            TotalReturn: e.TotalReturn,
            SharpeRatio: e.SharpeRatio,
            PromotedAt: e.PromotedAt ?? e.CreatedAt
        )).ToList();
    }
}
