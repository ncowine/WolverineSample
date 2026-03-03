using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingAssistant.Contracts;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Intelligence;

public class CheckDecayHandler
{
    public static async Task<CheckDecayResultDto> HandleAsync(
        CheckDecayCommand command,
        IClaudeClient claude,
        IntelligenceDbContext intelligenceDb,
        ILogger<CheckDecayHandler> logger)
    {
        logger.LogInformation("Checking strategy decay for {StrategyId} in {Market}",
            command.StrategyId, command.MarketCode);

        // Load trade reviews for this strategy
        var trades = await intelligenceDb.TradeReviews
            .Where(r => r.MarketCode == command.MarketCode)
            .OrderBy(r => r.ExitDate)
            .Select(r => new StrategyDecayChecker.TradeData(r.ExitDate, r.PnlPercent))
            .ToListAsync();

        if (trades.Count < StrategyDecayChecker.MinTradesForMetric)
        {
            return new CheckDecayResultDto(
                AlertTriggered: false, AlertId: null, AlertType: null,
                TriggerReason: $"Insufficient trades ({trades.Count}) for decay analysis",
                ClaudeAnalysis: null);
        }

        // Run decay check
        var result = StrategyDecayChecker.CheckForDecay(trades);

        if (!result.AlertTriggered || result.AlertType is null)
        {
            return new CheckDecayResultDto(
                AlertTriggered: false, AlertId: null, AlertType: null,
                TriggerReason: null, ClaudeAnalysis: null);
        }

        // Check for existing unresolved alert of same type for this strategy
        var existingAlert = await intelligenceDb.StrategyDecayAlerts
            .AnyAsync(a => a.StrategyId == command.StrategyId
                && a.AlertType == result.AlertType.Value
                && !a.IsResolved);

        if (existingAlert)
        {
            return new CheckDecayResultDto(
                AlertTriggered: true, AlertId: null,
                AlertType: result.AlertType.Value.ToString(),
                TriggerReason: result.TriggerReason,
                ClaudeAnalysis: null);
        }

        // Get strategy name from assignment or use market code
        var assignment = await intelligenceDb.StrategyAssignments
            .FirstOrDefaultAsync(a => a.StrategyId == command.StrategyId);
        var strategyName = assignment?.StrategyName ?? $"Strategy-{command.StrategyId.ToString("N")[..8]}";

        // Call Claude for analysis
        string? claudeAnalysis = null;
        if (!claude.IsRateLimited)
        {
            claudeAnalysis = await GetClaudeAnalysis(claude, strategyName, result, logger);
        }

        // Save alert
        var alert = new StrategyDecayAlert
        {
            StrategyId = command.StrategyId,
            StrategyName = strategyName,
            MarketCode = command.MarketCode,
            AlertType = result.AlertType.Value,
            Rolling30DaySharpe = result.Rolling30.Sharpe,
            Rolling60DaySharpe = result.Rolling60.Sharpe,
            Rolling90DaySharpe = result.Rolling90.Sharpe,
            Rolling30DayWinRate = result.Rolling30.WinRate,
            Rolling60DayWinRate = result.Rolling60.WinRate,
            Rolling90DayWinRate = result.Rolling90.WinRate,
            Rolling30DayAvgPnl = result.Rolling30.AvgPnl,
            Rolling60DayAvgPnl = result.Rolling60.AvgPnl,
            Rolling90DayAvgPnl = result.Rolling90.AvgPnl,
            HistoricalSharpe = result.HistoricalSharpe,
            TriggerReason = result.TriggerReason!,
            ClaudeAnalysis = claudeAnalysis
        };

        intelligenceDb.StrategyDecayAlerts.Add(alert);
        await intelligenceDb.SaveChangesAsync();

        logger.LogWarning(
            "Strategy decay alert: {AlertType} for '{Strategy}' ({Market}): {Reason}",
            result.AlertType, strategyName, command.MarketCode, result.TriggerReason);

        return new CheckDecayResultDto(
            AlertTriggered: true,
            AlertId: alert.Id,
            AlertType: result.AlertType.Value.ToString(),
            TriggerReason: result.TriggerReason,
            ClaudeAnalysis: claudeAnalysis);
    }

    private static async Task<string?> GetClaudeAnalysis(
        IClaudeClient claude,
        string strategyName,
        StrategyDecayChecker.DecayCheckResult result,
        ILogger logger)
    {
        try
        {
            var request = new ClaudeRequest(
                SystemPrompt: """
                    You are an expert quantitative analyst. Analyze why a trading strategy
                    appears to be losing its edge. Provide a concise (2-3 sentences) probable
                    cause based on the rolling metrics. Be specific and actionable.
                    Respond with plain text, not JSON.
                    """,
                UserPrompt: $"""
                    Strategy "{strategyName}" is showing signs of decay:

                    Trigger: {result.TriggerReason}

                    Rolling metrics:
                    - 30-day: Sharpe={result.Rolling30.Sharpe:F4}, WinRate={result.Rolling30.WinRate:F1}%, AvgPnl={result.Rolling30.AvgPnl:F2}%, Trades={result.Rolling30.TradeCount}
                    - 60-day: Sharpe={result.Rolling60.Sharpe:F4}, WinRate={result.Rolling60.WinRate:F1}%, AvgPnl={result.Rolling60.AvgPnl:F2}%, Trades={result.Rolling60.TradeCount}
                    - 90-day: Sharpe={result.Rolling90.Sharpe:F4}, WinRate={result.Rolling90.WinRate:F1}%, AvgPnl={result.Rolling90.AvgPnl:F2}%, Trades={result.Rolling90.TradeCount}
                    - Historical Sharpe: {result.HistoricalSharpe:F4}

                    What is the most likely cause of this decay, and what should be done?
                    """,
                Temperature: 0.3m,
                MaxTokens: 512);

            var response = await claude.CompleteAsync(request);
            return response.Success ? response.Content.Trim() : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get Claude analysis for strategy decay");
            return null;
        }
    }
}
