using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingAssistant.Application.Intelligence.Prompts;
using TradingAssistant.Contracts;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Intelligence;

public class RunAutopsyHandler
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task<StrategyAutopsyResultDto> HandleAsync(
        RunAutopsyCommand command,
        IClaudeClient claude,
        BacktestDbContext backtestDb,
        IntelligenceDbContext intelligenceDb,
        ILogger<RunAutopsyHandler> logger)
    {
        logger.LogInformation("Running autopsy for Strategy {StrategyId}, period {Year}-{Month:D2}",
            command.StrategyId, command.Year, command.Month);

        // 1. Load strategy
        var strategy = await backtestDb.Strategies
            .FirstOrDefaultAsync(s => s.Id == command.StrategyId);

        if (strategy is null)
        {
            return new StrategyAutopsyResultDto(
                Success: false, AutopsyId: null, StrategyName: string.Empty,
                PrimaryLossReason: string.Empty, RootCauses: [], MarketConditionImpact: string.Empty,
                Recommendations: [], ShouldRetire: false, Confidence: 0, Summary: string.Empty,
                Error: $"Strategy '{command.StrategyId}' not found.");
        }

        // 2. Find the most recent backtest result for this strategy
        var latestResult = await backtestDb.BacktestRuns
            .Where(r => r.StrategyId == command.StrategyId)
            .OrderByDescending(r => r.EndDate)
            .Select(r => r.Result)
            .FirstOrDefaultAsync();

        // 3. Extract monthly return for the requested month
        var periodStart = new DateTime(command.Year, command.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);
        var monthKey = $"{command.Year}-{command.Month:D2}";

        decimal monthlyReturn = 0m;
        decimal maxDrawdown = 0m;
        decimal winRate = 0m;
        decimal sharpeRatio = 0m;
        int tradeCount = 0;
        List<string> worstTrades = [];

        if (latestResult is not null)
        {
            // Try to extract the specific month's return from MonthlyReturnsJson
            if (!string.IsNullOrWhiteSpace(latestResult.MonthlyReturnsJson)
                && latestResult.MonthlyReturnsJson != "{}")
            {
                var monthlyReturns = JsonSerializer.Deserialize<Dictionary<string, decimal>>(
                    latestResult.MonthlyReturnsJson, JsonOpts);
                monthlyReturns?.TryGetValue(monthKey, out monthlyReturn);
            }

            // Use overall metrics as context
            maxDrawdown = latestResult.MaxDrawdown;
            winRate = latestResult.WinRate;
            sharpeRatio = latestResult.SharpeRatio;
            tradeCount = latestResult.TotalTrades;

            // Extract worst trades from TradeLogJson if available
            worstTrades = ExtractWorstTrades(latestResult.TradeLogJson);
        }

        // 4. Gather regime history during the period
        var regimesDuringPeriod = await GetRegimesDuringPeriod(intelligenceDb, periodStart, periodEnd);

        // Determine market code from strategy template or assignment
        var marketCode = strategy.TemplateMarketCode ?? "US_SP500";
        var assignment = await intelligenceDb.StrategyAssignments
            .FirstOrDefaultAsync(a => a.StrategyId == command.StrategyId);
        if (assignment is not null)
            marketCode = assignment.MarketCode;

        // 5. Check Claude rate limit
        if (claude.IsRateLimited)
        {
            return new StrategyAutopsyResultDto(
                Success: false, AutopsyId: null, StrategyName: strategy.Name,
                PrimaryLossReason: string.Empty, RootCauses: [], MarketConditionImpact: string.Empty,
                Recommendations: [], ShouldRetire: false, Confidence: 0, Summary: string.Empty,
                Error: "Claude API daily rate limit reached. Try again tomorrow.");
        }

        // 6. Call Claude for autopsy
        var input = new StrategyAutopsyInput(
            StrategyName: strategy.Name,
            PeriodStart: periodStart,
            PeriodEnd: periodEnd,
            TotalReturnPercent: monthlyReturn,
            MaxDrawdownPercent: maxDrawdown,
            WinRate: winRate,
            SharpeRatio: sharpeRatio,
            TradeCount: tradeCount,
            WorstTrades: worstTrades.Count > 0 ? worstTrades : null,
            RegimesDuringPeriod: regimesDuringPeriod);

        var request = new ClaudeRequest(
            StrategyAutopsyPrompt.BuildSystemPrompt(),
            StrategyAutopsyPrompt.BuildUserPrompt(input),
            Temperature: 0.3m,
            MaxTokens: 2048);

        var response = await claude.CompleteAsync(request);
        if (!response.Success)
        {
            logger.LogWarning("Claude autopsy call failed: {Error}", response.Error);
            return new StrategyAutopsyResultDto(
                Success: false, AutopsyId: null, StrategyName: strategy.Name,
                PrimaryLossReason: string.Empty, RootCauses: [], MarketConditionImpact: string.Empty,
                Recommendations: [], ShouldRetire: false, Confidence: 0, Summary: string.Empty,
                Error: $"Claude API error: {response.Error}");
        }

        // 7. Parse Claude's response
        var output = StrategyAutopsyPrompt.ParseResponse(response.Content);
        if (output is null)
        {
            logger.LogWarning("Failed to parse Claude autopsy response");
            return new StrategyAutopsyResultDto(
                Success: false, AutopsyId: null, StrategyName: strategy.Name,
                PrimaryLossReason: string.Empty, RootCauses: [], MarketConditionImpact: string.Empty,
                Recommendations: [], ShouldRetire: false, Confidence: 0, Summary: string.Empty,
                Error: "Failed to parse Claude's autopsy response.");
        }

        // 8. Classify primary loss reason
        var lossReason = ClassifyLossReason(output.PrimaryLossReason);

        // 9. Save autopsy to IntelligenceDbContext
        var autopsy = new StrategyAutopsy
        {
            StrategyId = command.StrategyId,
            StrategyName = strategy.Name,
            MarketCode = marketCode,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            MonthlyReturnPercent = monthlyReturn,
            MaxDrawdownPercent = maxDrawdown,
            WinRate = winRate,
            SharpeRatio = sharpeRatio,
            TradeCount = tradeCount,
            PrimaryLossReason = lossReason,
            RootCausesJson = JsonSerializer.Serialize(output.RootCauses, JsonOpts),
            MarketConditionImpact = output.MarketConditionImpact,
            RecommendationsJson = JsonSerializer.Serialize(output.Recommendations, JsonOpts),
            ShouldRetire = output.ShouldRetire,
            Confidence = output.Confidence,
            Summary = output.Summary
        };

        intelligenceDb.StrategyAutopsies.Add(autopsy);
        await intelligenceDb.SaveChangesAsync();

        logger.LogInformation(
            "Autopsy completed for '{Strategy}' ({Month}): LossReason={Reason}, ShouldRetire={Retire}",
            strategy.Name, monthKey, lossReason, output.ShouldRetire);

        return new StrategyAutopsyResultDto(
            Success: true,
            AutopsyId: autopsy.Id,
            StrategyName: strategy.Name,
            PrimaryLossReason: lossReason.ToString(),
            RootCauses: output.RootCauses,
            MarketConditionImpact: output.MarketConditionImpact,
            Recommendations: output.Recommendations,
            ShouldRetire: output.ShouldRetire,
            Confidence: output.Confidence,
            Summary: output.Summary);
    }

    internal static LossReason ClassifyLossReason(string claudeReason)
    {
        if (string.IsNullOrWhiteSpace(claudeReason))
            return LossReason.SignalDegradation;

        var normalized = claudeReason.Trim();

        if (Enum.TryParse<LossReason>(normalized, ignoreCase: true, out var parsed))
            return parsed;

        // Fuzzy matching for common Claude phrasing
        var lower = normalized.ToLowerInvariant();
        if (lower.Contains("regime") || lower.Contains("market condition") || lower.Contains("mismatch"))
            return LossReason.RegimeMismatch;
        if (lower.Contains("black swan") || lower.Contains("unexpected") || lower.Contains("crash"))
            return LossReason.BlackSwan;
        if (lower.Contains("position siz") || lower.Contains("over-sized") || lower.Contains("allocation"))
            return LossReason.PositionSizingError;
        if (lower.Contains("stop") || lower.Contains("stop-loss") || lower.Contains("stoploss"))
            return LossReason.StopLossFailure;
        if (lower.Contains("signal") || lower.Contains("degradation") || lower.Contains("indicator"))
            return LossReason.SignalDegradation;

        return LossReason.SignalDegradation;
    }

    private static async Task<string> GetRegimesDuringPeriod(
        IntelligenceDbContext db, DateTime start, DateTime end)
    {
        var regimes = await db.MarketRegimes
            .Where(r => r.ClassifiedAt >= start && r.ClassifiedAt <= end)
            .OrderBy(r => r.ClassifiedAt)
            .Select(r => new { r.CurrentRegime, r.ClassifiedAt })
            .ToListAsync();

        if (regimes.Count == 0)
            return "Unknown";

        var distinct = regimes
            .Select(r => r.CurrentRegime.ToString())
            .Distinct();

        return string.Join(" → ", distinct);
    }

    private static List<string> ExtractWorstTrades(string? tradeLogJson)
    {
        if (string.IsNullOrWhiteSpace(tradeLogJson))
            return [];

        try
        {
            using var doc = JsonDocument.Parse(tradeLogJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return [];

            var trades = new List<(string Symbol, decimal Pnl)>();

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                var symbol = element.TryGetProperty("symbol", out var s) ? s.GetString() ?? "?" : "?";
                var pnl = element.TryGetProperty("pnlPercent", out var p) ? p.GetDecimal() : 0m;
                if (pnl < 0)
                    trades.Add((symbol, pnl));
            }

            return trades
                .OrderBy(t => t.Pnl)
                .Take(5)
                .Select(t => $"{t.Symbol} {t.Pnl:F2}%")
                .ToList();
        }
        catch
        {
            return [];
        }
    }
}
