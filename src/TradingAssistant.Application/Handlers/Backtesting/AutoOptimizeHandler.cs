using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingAssistant.Application.Backtesting;
using TradingAssistant.Contracts.Backtesting;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Infrastructure.Persistence;
using Wolverine;

namespace TradingAssistant.Application.Handlers.Backtesting;

public class AutoOptimizeHandler
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public static async Task<AutoOptimizeResultDto> HandleAsync(
        AutoOptimizeCommand command,
        BacktestDbContext db,
        IMessageBus bus,
        ILogger<AutoOptimizeHandler> logger)
    {
        var runId = command.BacktestRunId;
        AutoOptimizeProgressStore.ActiveRunId = runId;

        try
        {
        // 1. Load backtest run with result and strategy
        AutoOptimizeProgressStore.Update(runId, "Loading backtest data", 0, 0, 0, 0);
        var run = await db.BacktestRuns
            .Include(r => r.Result)
            .Include(r => r.Strategy)
            .FirstOrDefaultAsync(r => r.Id == command.BacktestRunId)
            ?? throw new InvalidOperationException($"Backtest run '{command.BacktestRunId}' not found.");

        if (run.Result is null)
            throw new InvalidOperationException("Backtest has no result yet.");

        if (!run.Strategy.UsesV2Engine || string.IsNullOrEmpty(run.Strategy.RulesJson))
            throw new InvalidOperationException("Auto-optimize requires a V2 strategy with RulesJson.");

        // 2. Deserialize trade log and strategy definition
        AutoOptimizeProgressStore.Update(runId, "Analyzing trade log", 0, 0, 0, 0);
        var trades = DeserializeTradeLog(run.Result.TradeLogJson);
        var definition = JsonSerializer.Deserialize<StrategyDefinition>(run.Strategy.RulesJson, JsonOpts)
            ?? throw new InvalidOperationException("Failed to deserialize strategy definition.");

        // 3. Compute trade stats
        var stats = ComputeTradeStats(trades, run.StartDate, run.EndDate);

        logger.LogInformation(
            "[AutoOptimize] Run {RunId}: {Trades} trades, WR={WinRate:F1}%, AvgHold={AvgHold:F1}d, MaxDD={MaxDD:F1}%",
            command.BacktestRunId, stats.TotalTrades, stats.WinRate * 100, stats.AvgHoldingDays, stats.MaxDrawdown * 100);

        // 4. Diagnose weaknesses and generate parameter ranges
        AutoOptimizeProgressStore.Update(runId, "Diagnosing weaknesses", 0, 0, 0, 0);
        var (diagnoses, ranges) = DiagnoseAndGenerateRanges(stats, definition);

        if (ranges.Count == 0)
            throw new InvalidOperationException("No optimization opportunities found — strategy looks healthy.");

        logger.LogInformation("[AutoOptimize] Diagnoses: [{Diag}], Ranges: {Count}",
            string.Join(", ", diagnoses), ranges.Count);

        // 5. Resolve symbol — portfolio runs use "PORTFOLIO" placeholder, pick most-traded symbol instead
        var symbol = run.Symbol;
        if (string.Equals(symbol, "PORTFOLIO", StringComparison.OrdinalIgnoreCase) && trades.Count > 0)
        {
            symbol = trades
                .GroupBy(t => t.Symbol)
                .OrderByDescending(g => g.Count())
                .First()
                .Key;
            logger.LogInformation("[AutoOptimize] Portfolio backtest — using most-traded symbol '{Symbol}' for optimization", symbol);
        }

        // 6. Build and delegate to RunOptimizationCommand
        AutoOptimizeProgressStore.Update(runId, "Fetching market data", 0, 0, 0, 0);
        var optCommand = new RunOptimizationCommand(
            StrategyId: run.StrategyId,
            Symbol: symbol,
            StartDate: run.StartDate,
            EndDate: run.EndDate,
            ParameterRanges: ranges,
            UniverseId: run.UniverseId,
            InitialCapital: run.InitialCapital,
            MaxPositions: run.MaxPositions);

        var optimization = await bus.InvokeAsync<OptimizationResultDto>(optCommand);

        // 7. Return before/after comparison
        return new AutoOptimizeResultDto(
            Diagnoses: diagnoses,
            GeneratedRanges: ranges,
            BeforeWinRate: Math.Round(run.Result.WinRate, 2),
            BeforeSharpe: Math.Round(run.Result.SharpeRatio, 4),
            BeforeMaxDrawdown: Math.Round(run.Result.MaxDrawdown, 2),
            BeforeCagr: Math.Round(run.Result.Cagr, 2),
            BeforeProfitFactor: Math.Round(run.Result.ProfitFactor, 2),
            Optimization: optimization);
        }
        finally
        {
            AutoOptimizeProgressStore.Remove(runId);
            AutoOptimizeProgressStore.ActiveRunId = null;
        }
    }

    private static List<TradeRecord> DeserializeTradeLog(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            return JsonSerializer.Deserialize<List<TradeRecord>>(json, JsonOpts) ?? new();
        }
        catch
        {
            return new();
        }
    }

    private record TradeStats(
        int TotalTrades,
        decimal WinRate,
        decimal AvgHoldingDays,
        int SlHits,
        int TpHits,
        int ExitSignalHits,
        decimal AvgLossPercent,
        decimal MaxDrawdown,
        int TestPeriodDays);

    private static TradeStats ComputeTradeStats(List<TradeRecord> trades, DateTime start, DateTime end)
    {
        if (trades.Count == 0)
        {
            return new TradeStats(0, 0, 0, 0, 0, 0, 0, 0, (int)(end - start).TotalDays);
        }

        var wins = trades.Count(t => t.PnL > 0);
        var winRate = (decimal)wins / trades.Count;
        var avgHold = (decimal)trades.Average(t => t.HoldingDays);

        var slHits = trades.Count(t =>
            t.ExitReason.Contains("Stop", StringComparison.OrdinalIgnoreCase));
        var tpHits = trades.Count(t =>
            t.ExitReason.Contains("TakeProfit", StringComparison.OrdinalIgnoreCase) ||
            t.ExitReason.Contains("TP", StringComparison.OrdinalIgnoreCase));
        var exitSignalHits = trades.Count(t =>
            t.ExitReason.Contains("Signal", StringComparison.OrdinalIgnoreCase) ||
            t.ExitReason.Contains("Exit", StringComparison.OrdinalIgnoreCase));

        var losses = trades.Where(t => t.PnLPercent < 0).ToList();
        var avgLoss = losses.Count > 0 ? Math.Abs(losses.Average(t => t.PnLPercent)) : 0;

        // Approximate max drawdown from trade PnL sequence
        decimal peak = 0, maxDd = 0, cumPnl = 0;
        foreach (var t in trades.OrderBy(t => t.ExitDate))
        {
            cumPnl += t.PnLPercent;
            if (cumPnl > peak) peak = cumPnl;
            var dd = peak - cumPnl;
            if (dd > maxDd) maxDd = dd;
        }

        return new TradeStats(
            trades.Count, winRate, avgHold,
            slHits, tpHits, exitSignalHits,
            avgLoss, maxDd / 100m,
            (int)(end - start).TotalDays);
    }

    private static (List<string> Diagnoses, List<ParameterRangeDto> Ranges) DiagnoseAndGenerateRanges(
        TradeStats stats, StrategyDefinition definition)
    {
        var diagnoses = new List<string>();
        var ranges = new Dictionary<string, ParameterRangeDto>(StringComparer.OrdinalIgnoreCase);

        // Rule: SL too wide — 0 SL hits OR avgLoss > 8%
        if (stats.SlHits == 0 || stats.AvgLossPercent > 8)
        {
            diagnoses.Add(stats.SlHits == 0
                ? "Stop-loss never triggered — likely too wide"
                : $"Average loss too high ({stats.AvgLossPercent:F1}%)");

            var currentMult = definition.StopLoss?.Multiplier ?? 2m;
            ranges["StopMultiplier"] = new ParameterRangeDto("StopMultiplier",
                Math.Max(0.5m, currentMult * 0.5m), currentMult * 1.5m, 0.25m);
            ranges["MaxStopLossPercent"] = new ParameterRangeDto("MaxStopLossPercent", 3m, 8m, 1m);
        }

        // Rule: Holding too long — avgHold > 15d
        if (stats.AvgHoldingDays > 15)
        {
            diagnoses.Add($"Average holding period too long ({stats.AvgHoldingDays:F0} days)");
            ranges["MaxHoldingDays"] = new ParameterRangeDto("MaxHoldingDays", 5m, 30m, 5m);
        }

        // Rule: Low win rate — < 30%
        if (stats.WinRate < 0.30m && stats.TotalTrades > 5)
        {
            diagnoses.Add($"Low win rate ({stats.WinRate * 100:F1}%)");
            // Tighten entry thresholds — add ranges for entry conditions
            foreach (var group in definition.EntryConditions)
            {
                foreach (var cond in group.Conditions)
                {
                    var paramName = $"Entry{cond.Indicator}Value";
                    if (!ranges.ContainsKey(paramName))
                    {
                        var current = cond.Value;
                        var margin = Math.Max(Math.Abs(current) * 0.3m, 5m);
                        ranges[paramName] = new ParameterRangeDto(paramName,
                            current - margin, current + margin,
                            Math.Max(1m, Math.Round(margin / 5m, 0)));
                    }
                }
            }
        }

        // Rule: High drawdown — maxDD > 20%
        if (stats.MaxDrawdown > 0.20m)
        {
            diagnoses.Add($"High max drawdown ({stats.MaxDrawdown * 100:F1}%)");
            ranges["RiskPercent"] = new ParameterRangeDto("RiskPercent", 0.5m, 3m, 0.5m);
            ranges["MaxPortfolioHeat"] = new ParameterRangeDto("MaxPortfolioHeat", 5m, 15m, 2.5m);
        }

        // Rule: Too few trades — trades < testDays / 30
        if (stats.TotalTrades < stats.TestPeriodDays / 30 && stats.TotalTrades > 0)
        {
            diagnoses.Add($"Too few trades ({stats.TotalTrades} in {stats.TestPeriodDays} days)");
            // Widen entry thresholds to catch more signals
            foreach (var group in definition.EntryConditions)
            {
                foreach (var cond in group.Conditions)
                {
                    var paramName = $"Entry{cond.Indicator}Value";
                    if (!ranges.ContainsKey(paramName))
                    {
                        var current = cond.Value;
                        var margin = Math.Max(Math.Abs(current) * 0.5m, 10m);
                        ranges[paramName] = new ParameterRangeDto(paramName,
                            current - margin, current + margin,
                            Math.Max(1m, Math.Round(margin / 5m, 0)));
                    }
                }
            }
        }

        // Rule: TP rarely hits — ≤1 TP hit, >30% exit signals
        if (stats.TpHits <= 1 && stats.ExitSignalHits > stats.TotalTrades * 0.3m)
        {
            diagnoses.Add("Take-profit rarely triggers — exits mostly from signals");
            ranges["TakeProfitMultiplier"] = new ParameterRangeDto("TakeProfitMultiplier", 1m, 4m, 0.5m);
        }

        // Rule: No trailing stop
        if (definition.StopLoss is { UseTrailingStop: false })
        {
            diagnoses.Add("No trailing stop — profits may erode on reversals");
            ranges["TrailingActivationR"] = new ParameterRangeDto("TrailingActivationR", 1m, 3m, 0.5m);
            ranges["TrailingAtrMultiplier"] = new ParameterRangeDto("TrailingAtrMultiplier", 1.5m, 3m, 0.5m);
        }

        return (diagnoses, ranges.Values.ToList());
    }
}
