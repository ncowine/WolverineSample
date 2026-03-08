using TradingAssistant.Application.Backtesting;
using TradingAssistant.Application.Indicators;
using TradingAssistant.Application.Intelligence.Prompts;
using TradingAssistant.Contracts;
using TradingAssistant.Contracts.Backtesting;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Domain.MarketData;

namespace TradingAssistant.Application.Intelligence;

/// <summary>
/// Orchestrates AI strategy generation: Claude call → parse → backtest → validate.
/// Pure static methods with no DB dependency — the handler provides data.
/// </summary>
public static class StrategyGenerator
{
    /// <summary>
    /// Call Claude to generate a StrategyDefinition from natural language.
    /// </summary>
    public static async Task<(StrategyDefinition? Definition, string Rationale, string? Error)> GenerateAsync(
        IClaudeClient claude,
        StrategyDefinitionInput input,
        CancellationToken ct = default)
    {
        if (claude.IsRateLimited)
            return (null, string.Empty, "Daily Claude API rate limit reached. Try again tomorrow.");

        var request = new ClaudeRequest(
            SystemPrompt: StrategyDefinitionPrompt.BuildSystemPrompt(),
            UserPrompt: StrategyDefinitionPrompt.BuildUserPrompt(input),
            Temperature: 0.7m,
            MaxTokens: 4096);

        var response = await claude.CompleteAsync(request, ct);
        if (!response.Success)
            return (null, string.Empty, $"Claude API error: {response.Error}");

        var definition = StrategyDefinitionPrompt.ParseResponse(response.Content);
        if (definition is null)
            return (null, response.Content, "Failed to parse Claude response as StrategyDefinition JSON.");

        // Extract a rationale from the response or use the description
        var rationale = $"AI-generated strategy for {input.MarketCode} ({input.RegimeType} regime). " +
                        $"User request: {input.Description}";

        return (definition, rationale, null);
    }

    /// <summary>
    /// Run backtest on historical candles and compute performance metrics.
    /// </summary>
    public static (BacktestEngineResult Result, PerformanceMetrics Metrics) Backtest(
        StrategyDefinition definition,
        List<PriceCandle> dailyCandles,
        string symbol)
    {
        // Compute indicators via IndicatorOrchestrator
        var candlesByTimeframe = new Dictionary<CandleInterval, List<PriceCandle>>
        {
            [CandleInterval.Daily] = dailyCandles
        };

        var multiTf = IndicatorOrchestrator.Compute(candlesByTimeframe);
        var bars = multiTf.AlignedDaily;

        if (bars.Length < 2)
        {
            var emptyResult = new BacktestEngineResult
            {
                Symbol = symbol,
                StartDate = dailyCandles.FirstOrDefault()?.Timestamp ?? DateTime.UtcNow,
                EndDate = dailyCandles.LastOrDefault()?.Timestamp ?? DateTime.UtcNow,
                InitialCapital = 100_000m,
                FinalEquity = 100_000m
            };
            return (emptyResult, new PerformanceMetrics());
        }

        var engine = new PortfolioBacktestEngine(definition, maxPositions: 1, initialCapital: 100_000m);
        var result = engine.Run(new Dictionary<string, CandleWithIndicators[]> { [symbol] = bars });
        var metrics = PerformanceCalculator.Calculate(result);

        return (result, metrics);
    }

    /// <summary>
    /// Validate strategy against minimum Sharpe threshold.
    /// </summary>
    public static (bool Accepted, string? RejectionReason) Validate(
        PerformanceMetrics metrics,
        decimal minSharpe)
    {
        if (metrics.TotalTrades == 0)
            return (false, "Strategy produced zero trades in the backtest period.");

        if (metrics.SharpeRatio < minSharpe)
            return (false, $"Sharpe ratio {metrics.SharpeRatio:F2} is below minimum threshold {minSharpe:F2}.");

        return (true, null);
    }

    /// <summary>
    /// Convert PerformanceMetrics to the summary DTO.
    /// </summary>
    public static BacktestSummaryDto ToSummary(PerformanceMetrics metrics) =>
        new(
            TotalTrades: metrics.TotalTrades,
            WinRate: Math.Round(metrics.WinRate, 2),
            TotalReturn: Math.Round(metrics.TotalReturn, 2),
            MaxDrawdown: Math.Round(metrics.MaxDrawdownPercent, 2),
            SharpeRatio: Math.Round(metrics.SharpeRatio, 2));
}
