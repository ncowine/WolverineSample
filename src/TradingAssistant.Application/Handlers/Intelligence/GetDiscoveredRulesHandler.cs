using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application.Intelligence.Prompts;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Intelligence;

public class GetDiscoveredRulesHandler
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task<IReadOnlyList<DiscoverRulesResultDto>> HandleAsync(
        GetDiscoveredRulesQuery query,
        IntelligenceDbContext db)
    {
        var discoveries = await db.RuleDiscoveries
            .Where(d => d.StrategyId == query.StrategyId)
            .OrderByDescending(d => d.AnalyzedAt)
            .ToListAsync();

        return discoveries.Select(d =>
        {
            var rules = DeserializeRules(d.DiscoveredRulesJson);
            var patterns = DeserializeList(d.PatternsJson);

            return new DiscoverRulesResultDto(
                Success: true,
                DiscoveryId: d.Id,
                StrategyName: d.StrategyName,
                TradeCount: d.TradeCount,
                DiscoveredRules: rules,
                Patterns: patterns,
                Summary: d.Summary);
        }).ToList();
    }

    private static IReadOnlyList<DiscoveredRuleDto> DeserializeRules(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
            return [];

        try
        {
            var rules = JsonSerializer.Deserialize<List<DiscoveredRule>>(json, JsonOpts);
            return rules?.Select(r => new DiscoveredRuleDto(
                r.Rule, r.Confidence, r.SupportingTradeCount, r.Description)).ToList()
                ?? [];
        }
        catch
        {
            return [];
        }
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
