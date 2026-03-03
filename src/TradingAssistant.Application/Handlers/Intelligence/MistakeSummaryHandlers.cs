using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingAssistant.Contracts;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Intelligence;

public class GetMistakeSummaryHandler
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task<MistakeSummaryDto> HandleAsync(
        GetMistakeSummaryQuery query,
        IntelligenceDbContext db)
    {
        var reviews = await db.TradeReviews
            .Where(r => r.MarketCode == query.MarketCode)
            .OrderBy(r => r.ReviewedAt)
            .ToListAsync();

        var summary = MistakePatternAnalyzer.Analyze(query.MarketCode, reviews);

        // Get last pattern report info
        var lastReport = await db.MistakePatternReports
            .Where(r => r.MarketCode == query.MarketCode)
            .OrderByDescending(r => r.AnalyzedAt)
            .FirstOrDefaultAsync();

        var tradesSinceLastReport = lastReport is not null
            ? reviews.Count(r => r.ReviewedAt > lastReport.AnalyzedAt)
            : reviews.Count;

        return new MistakeSummaryDto(
            MarketCode: summary.MarketCode,
            TotalTrades: summary.TotalTrades,
            LosingTrades: summary.LosingTrades,
            MostCommonMistake: summary.MostCommonMistake,
            MistakeBreakdown: summary.MistakeBreakdown,
            RegimeBreakdown: summary.RegimeBreakdown.ToDictionary(
                kv => kv.Key,
                kv => (IReadOnlyDictionary<string, int>)kv.Value),
            Recommendations: summary.Recommendations,
            LastReportDate: lastReport?.AnalyzedAt,
            TradesSinceLastReport: tradesSinceLastReport);
    }
}

public class GeneratePatternReportHandler
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task<MistakePatternReportDto> HandleAsync(
        GeneratePatternReportCommand command,
        IClaudeClient claude,
        IntelligenceDbContext db,
        ILogger<GeneratePatternReportHandler> logger)
    {
        logger.LogInformation("Generating mistake pattern report for {Market}", command.MarketCode);

        var reviews = await db.TradeReviews
            .Where(r => r.MarketCode == command.MarketCode)
            .OrderBy(r => r.ReviewedAt)
            .ToListAsync();

        var summary = MistakePatternAnalyzer.Analyze(command.MarketCode, reviews);

        // Get Claude analysis if available
        string? claudeAnalysis = null;
        if (!claude.IsRateLimited && summary.LosingTrades > 0)
        {
            claudeAnalysis = await GetClaudeAnalysis(claude, summary, logger);
        }

        // Save the report
        var report = new MistakePatternReport
        {
            MarketCode = command.MarketCode,
            TradeCount = summary.TotalTrades,
            LosingTradeCount = summary.LosingTrades,
            MostCommonMistake = summary.MostCommonMistake ?? "None",
            MistakeBreakdownJson = JsonSerializer.Serialize(summary.MistakeBreakdown, JsonOpts),
            RegimeBreakdownJson = JsonSerializer.Serialize(summary.RegimeBreakdown, JsonOpts),
            RecommendationsJson = JsonSerializer.Serialize(summary.Recommendations, JsonOpts),
            ClaudeAnalysis = claudeAnalysis
        };

        db.MistakePatternReports.Add(report);
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Pattern report generated for {Market}: {Total} trades, {Losing} losing, top mistake: {Top}",
            command.MarketCode, summary.TotalTrades, summary.LosingTrades, summary.MostCommonMistake);

        return MapToDto(report);
    }

    internal static MistakePatternReportDto MapToDto(MistakePatternReport report)
    {
        var opts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var breakdown = TryDeserialize<Dictionary<string, int>>(report.MistakeBreakdownJson, opts)
            ?? new Dictionary<string, int>();
        var regimeBreakdown = TryDeserialize<Dictionary<string, Dictionary<string, int>>>(
            report.RegimeBreakdownJson, opts) ?? new Dictionary<string, Dictionary<string, int>>();
        var recommendations = TryDeserialize<List<string>>(report.RecommendationsJson, opts) ?? [];

        return new MistakePatternReportDto(
            Id: report.Id,
            MarketCode: report.MarketCode,
            TradeCount: report.TradeCount,
            LosingTradeCount: report.LosingTradeCount,
            MostCommonMistake: report.MostCommonMistake,
            MistakeBreakdown: breakdown,
            RegimeBreakdown: regimeBreakdown.ToDictionary(
                kv => kv.Key,
                kv => (IReadOnlyDictionary<string, int>)kv.Value),
            Recommendations: recommendations,
            ClaudeAnalysis: report.ClaudeAnalysis,
            AnalyzedAt: report.AnalyzedAt);
    }

    private static async Task<string?> GetClaudeAnalysis(
        IClaudeClient claude,
        MistakePatternAnalyzer.MistakeSummary summary,
        ILogger logger)
    {
        try
        {
            var breakdownText = string.Join("\n",
                summary.MistakeBreakdown.Select(kv => $"  - {kv.Key}: {kv.Value} trades"));
            var regimeText = string.Join("\n",
                summary.RegimeBreakdown.Select(kv =>
                    $"  {kv.Key}: {string.Join(", ", kv.Value.Select(m => $"{m.Key}={m.Value}"))}"));

            var request = new ClaudeRequest(
                SystemPrompt: """
                    You are an expert trading coach analyzing patterns in a trader's mistakes.
                    Provide a concise (3-5 sentences) analysis of the mistake patterns and
                    actionable advice to improve trading performance.
                    Respond with plain text, not JSON.
                    """,
                UserPrompt: $"""
                    Market: {summary.MarketCode}
                    Total trades: {summary.TotalTrades}, Losing trades: {summary.LosingTrades}

                    Mistake breakdown:
                    {breakdownText}

                    Regime breakdown:
                    {regimeText}

                    Most common mistake: {summary.MostCommonMistake}

                    What patterns do you see and what specific actions should be taken?
                    """,
                Temperature: 0.3m,
                MaxTokens: 512);

            var response = await claude.CompleteAsync(request);
            return response.Success ? response.Content.Trim() : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get Claude analysis for mistake patterns");
            return null;
        }
    }

    private static T? TryDeserialize<T>(string json, JsonSerializerOptions opts) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, opts);
        }
        catch
        {
            return null;
        }
    }
}
