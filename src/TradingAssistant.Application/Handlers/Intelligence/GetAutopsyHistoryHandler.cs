using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Intelligence;

public class GetAutopsyHistoryHandler
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task<IReadOnlyList<StrategyAutopsyDto>> HandleAsync(
        GetAutopsyHistoryQuery query,
        IntelligenceDbContext db)
    {
        var autopsies = await db.StrategyAutopsies
            .Where(a => a.StrategyId == query.StrategyId)
            .OrderByDescending(a => a.PeriodStart)
            .ToListAsync();

        return autopsies.Select(MapToDto).ToList();
    }

    internal static StrategyAutopsyDto MapToDto(StrategyAutopsy a)
    {
        var rootCauses = DeserializeList(a.RootCausesJson);
        var recommendations = DeserializeList(a.RecommendationsJson);

        return new StrategyAutopsyDto(
            Id: a.Id,
            StrategyId: a.StrategyId,
            StrategyName: a.StrategyName,
            MarketCode: a.MarketCode,
            PeriodStart: a.PeriodStart,
            PeriodEnd: a.PeriodEnd,
            MonthlyReturnPercent: a.MonthlyReturnPercent,
            PrimaryLossReason: a.PrimaryLossReason.ToString(),
            RootCauses: rootCauses,
            MarketConditionImpact: a.MarketConditionImpact,
            Recommendations: recommendations,
            ShouldRetire: a.ShouldRetire,
            Confidence: a.Confidence,
            Summary: a.Summary,
            AnalyzedAt: a.AnalyzedAt);
    }

    private static IReadOnlyList<string> DeserializeList(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonOpts) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
