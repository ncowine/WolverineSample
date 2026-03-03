using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Intelligence;

public class GetTournamentEntryDetailHandler
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task<TournamentEntryDetailDto?> HandleAsync(
        GetTournamentEntryDetailQuery query,
        IntelligenceDbContext db)
    {
        var entry = await db.TournamentEntries
            .FirstOrDefaultAsync(e => e.Id == query.EntryId);

        if (entry is null)
            return null;

        var equityCurve = DeserializeEquityCurve(entry.EquityCurveJson);

        return new TournamentEntryDetailDto(
            EntryId: entry.Id,
            TournamentRunId: entry.TournamentRunId,
            StrategyId: entry.StrategyId,
            StrategyName: entry.StrategyName,
            PaperAccountId: entry.PaperAccountId,
            MarketCode: entry.MarketCode,
            StartDate: entry.StartDate,
            DaysActive: entry.DaysActive,
            TotalTrades: entry.TotalTrades,
            WinRate: entry.WinRate,
            SharpeRatio: entry.SharpeRatio,
            MaxDrawdown: entry.MaxDrawdown,
            TotalReturn: entry.TotalReturn,
            AllocationPercent: entry.AllocationPercent,
            Status: entry.Status.ToString(),
            EligibleForPromotion: entry.DaysActive >= UpdateTournamentMetricsHandler.MinPromotionDays,
            EquityCurve: equityCurve);
    }

    private static IReadOnlyList<EquityPointDto> DeserializeEquityCurve(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<EquityPointDto>>(json, JsonOpts) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
