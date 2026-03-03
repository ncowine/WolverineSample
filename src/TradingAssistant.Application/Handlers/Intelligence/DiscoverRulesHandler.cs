using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingAssistant.Application.Intelligence.Prompts;
using TradingAssistant.Contracts;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Intelligence;

public class DiscoverRulesHandler
{
    public const int MinimumTradeCount = 50;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task<DiscoverRulesResultDto> HandleAsync(
        DiscoverRulesCommand command,
        IClaudeClient claude,
        BacktestDbContext backtestDb,
        IntelligenceDbContext intelligenceDb,
        ILogger<DiscoverRulesHandler> logger)
    {
        logger.LogInformation("Discovering rules for Strategy {StrategyId} in {Market}",
            command.StrategyId, command.MarketCode);

        // 1. Load strategy
        var strategy = await backtestDb.Strategies
            .FirstOrDefaultAsync(s => s.Id == command.StrategyId);

        if (strategy is null)
        {
            return Fail("", $"Strategy '{command.StrategyId}' not found.");
        }

        // 2. Gather all backtest trade logs for this strategy
        var results = await backtestDb.BacktestRuns
            .Where(r => r.StrategyId == command.StrategyId && r.Result != null)
            .OrderByDescending(r => r.EndDate)
            .Select(r => r.Result!)
            .ToListAsync();

        // 3. Extract trades from TradeLogJson across all results
        var trades = new List<TradeSummary>();
        foreach (var result in results)
        {
            var extracted = ExtractTrades(result.TradeLogJson);
            trades.AddRange(extracted);

            // Stop once we have enough trades
            if (trades.Count >= 200)
                break;
        }

        // 4. Validate minimum trade count
        if (trades.Count < MinimumTradeCount)
        {
            return Fail(strategy.Name,
                $"Insufficient trades for rule discovery. Found {trades.Count}, need at least {MinimumTradeCount}.");
        }

        // 5. Check Claude rate limit
        if (claude.IsRateLimited)
        {
            return Fail(strategy.Name, "Claude API daily rate limit reached. Try again tomorrow.");
        }

        // 6. Call Claude for pattern discovery
        var input = new RuleDiscoveryInput(command.MarketCode, trades);
        var request = new ClaudeRequest(
            RuleDiscoveryPrompt.BuildSystemPrompt(),
            RuleDiscoveryPrompt.BuildUserPrompt(input),
            Temperature: 0.3m,
            MaxTokens: 4096);

        var response = await claude.CompleteAsync(request);
        if (!response.Success)
        {
            logger.LogWarning("Claude rule discovery call failed: {Error}", response.Error);
            return Fail(strategy.Name, $"Claude API error: {response.Error}");
        }

        // 7. Parse Claude's response
        var output = RuleDiscoveryPrompt.ParseResponse(response.Content);
        if (output is null)
        {
            logger.LogWarning("Failed to parse Claude rule discovery response");
            return Fail(strategy.Name, "Failed to parse Claude's response.");
        }

        // 8. Save rule discovery to IntelligenceDbContext
        var winCount = trades.Count(t => t.WonTrade);
        var discovery = new RuleDiscovery
        {
            StrategyId = command.StrategyId,
            StrategyName = strategy.Name,
            MarketCode = command.MarketCode,
            TradeCount = trades.Count,
            WinningTrades = winCount,
            LosingTrades = trades.Count - winCount,
            DiscoveredRulesJson = JsonSerializer.Serialize(output.DiscoveredRules, JsonOpts),
            PatternsJson = JsonSerializer.Serialize(output.Patterns, JsonOpts),
            Summary = output.Summary,
            IsApproved = false
        };

        intelligenceDb.RuleDiscoveries.Add(discovery);
        await intelligenceDb.SaveChangesAsync();

        logger.LogInformation(
            "Rule discovery completed for '{Strategy}': {RuleCount} rules from {TradeCount} trades",
            strategy.Name, output.DiscoveredRules.Count, trades.Count);

        // 9. Map to DTOs
        var ruleDtos = output.DiscoveredRules
            .Select(r => new DiscoveredRuleDto(r.Rule, r.Confidence, r.SupportingTradeCount, r.Description))
            .ToList();

        return new DiscoverRulesResultDto(
            Success: true,
            DiscoveryId: discovery.Id,
            StrategyName: strategy.Name,
            TradeCount: trades.Count,
            DiscoveredRules: ruleDtos,
            Patterns: output.Patterns,
            Summary: output.Summary);
    }

    internal static List<TradeSummary> ExtractTrades(string? tradeLogJson)
    {
        if (string.IsNullOrWhiteSpace(tradeLogJson))
            return [];

        try
        {
            using var doc = JsonDocument.Parse(tradeLogJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return [];

            var trades = new List<TradeSummary>();

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                var symbol = GetString(element, "symbol", "?");
                var side = GetString(element, "side", "Long");
                var pnl = GetDecimal(element, "pnlPercent");
                var rsi = GetDecimal(element, "entryRsi");
                var macdH = GetDecimal(element, "entryMacdHistogram");
                var smaSlope = GetDecimal(element, "entrySmaSlope");
                var atr = GetDecimal(element, "entryAtr");
                var volume = GetDecimal(element, "entryVolume");
                var won = pnl > 0;

                trades.Add(new TradeSummary(
                    symbol, side, pnl, rsi, macdH, smaSlope, atr, volume, won));
            }

            return trades;
        }
        catch
        {
            return [];
        }
    }

    private static string GetString(JsonElement element, string property, string defaultValue)
    {
        return element.TryGetProperty(property, out var val) ? val.GetString() ?? defaultValue : defaultValue;
    }

    private static decimal GetDecimal(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var val) && val.TryGetDecimal(out var d) ? d : 0m;
    }

    private static DiscoverRulesResultDto Fail(string strategyName, string error)
    {
        return new DiscoverRulesResultDto(
            Success: false, DiscoveryId: null, StrategyName: strategyName,
            TradeCount: 0, DiscoveredRules: [], Patterns: [], Summary: string.Empty,
            Error: error);
    }
}
