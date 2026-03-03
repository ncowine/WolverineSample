using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.Infrastructure.Persistence;
using TradingAssistant.SharedKernel;

namespace TradingAssistant.Application.Handlers.Intelligence;

public class GetTradeReviewsHandler
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task<PagedResponse<TradeReviewDto>> HandleAsync(
        GetTradeReviewsQuery query, IntelligenceDbContext db)
    {
        var q = db.TradeReviews.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Symbol))
            q = q.Where(r => r.Symbol == query.Symbol);

        if (!string.IsNullOrWhiteSpace(query.MarketCode))
            q = q.Where(r => r.MarketCode == query.MarketCode);

        if (!string.IsNullOrWhiteSpace(query.OutcomeClass)
            && Enum.TryParse<OutcomeClass>(query.OutcomeClass, ignoreCase: true, out var oc))
            q = q.Where(r => r.OutcomeClass == oc);

        var totalCount = await q.CountAsync();
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var items = await q
            .OrderByDescending(r => r.ReviewedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var dtos = items.Select(r => MapToDto(r, JsonOpts)).ToList();

        return new PagedResponse<TradeReviewDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    internal static TradeReviewDto MapToDto(Domain.Intelligence.TradeReview r, JsonSerializerOptions opts)
    {
        return new TradeReviewDto(
            Id: r.Id,
            TradeId: r.TradeId,
            Symbol: r.Symbol,
            MarketCode: r.MarketCode,
            StrategyName: r.StrategyName,
            EntryPrice: r.EntryPrice,
            ExitPrice: r.ExitPrice,
            EntryDate: r.EntryDate,
            ExitDate: r.ExitDate,
            PnlPercent: r.PnlPercent,
            PnlAbsolute: r.PnlAbsolute,
            DurationHours: r.DurationHours,
            RegimeAtEntry: r.RegimeAtEntry,
            RegimeAtExit: r.RegimeAtExit,
            Grade: r.Grade,
            MlConfidence: r.MlConfidence,
            OutcomeClass: r.OutcomeClass.ToString(),
            MistakeType: r.MistakeType?.ToString(),
            Score: r.Score,
            Strengths: DeserializeList(r.StrengthsJson, opts),
            Weaknesses: DeserializeList(r.WeaknessesJson, opts),
            LessonsLearned: DeserializeList(r.LessonsLearnedJson, opts),
            Summary: r.Summary,
            ReviewedAt: r.ReviewedAt);
    }

    private static IReadOnlyList<string> DeserializeList(string json, JsonSerializerOptions opts)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, opts) ?? [];
        }
        catch
        {
            return [];
        }
    }
}

public class GetTradeReviewByTradeIdHandler
{
    public static async Task<TradeReviewDto?> HandleAsync(
        GetTradeReviewByTradeIdQuery query, IntelligenceDbContext db)
    {
        var review = await db.TradeReviews
            .FirstOrDefaultAsync(r => r.TradeId == query.TradeId);

        if (review is null)
            return null;

        return GetTradeReviewsHandler.MapToDto(review, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
}
