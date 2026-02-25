using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingAssistant.Contracts.Events;
using TradingAssistant.Domain.Backtesting;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Backtesting;

public class ExecuteBacktestHandler
{
    public static async Task<BacktestExecuted> HandleAsync(
        HistoricalDataLoaded @event,
        BacktestDbContext db,
        ILogger<ExecuteBacktestHandler> logger)
    {
        logger.LogInformation("[BacktestDb] Executing backtest {RunId} for {Symbol}",
            @event.BacktestRunId, @event.Symbol);

        // Deserialize price data
        var priceData = JsonSerializer.Deserialize<List<CandleData>>(@event.PriceDataJson)
            ?? new List<CandleData>();

        // Load strategy rules
        var strategy = await db.Strategies
            .Include(s => s.Rules)
            .FirstOrDefaultAsync(s => s.Id == @event.StrategyId);

        // Simple backtest simulation
        var trades = new List<SimulatedTrade>();
        decimal balance = 100_000m;
        decimal peakBalance = balance;
        decimal maxDrawdown = 0;
        bool inPosition = false;
        decimal entryPrice = 0;

        for (int i = 1; i < priceData.Count; i++)
        {
            var current = priceData[i];
            var previous = priceData[i - 1];

            // Simple moving average crossover simulation
            if (strategy?.Rules.Any() == true)
            {
                foreach (var rule in strategy.Rules)
                {
                    var priceChange = (current.Close - previous.Close) / previous.Close * 100;

                    if (rule.SignalType == SignalType.Buy && !inPosition)
                    {
                        // Buy signal: price change exceeds threshold
                        if (rule.Condition == "GreaterThan" && priceChange > rule.Threshold)
                        {
                            inPosition = true;
                            entryPrice = current.Close;
                        }
                    }
                    else if (rule.SignalType == SignalType.Sell && inPosition)
                    {
                        // Sell signal: price change below threshold
                        if (rule.Condition == "LessThan" && priceChange < -rule.Threshold)
                        {
                            inPosition = false;
                            var pnl = (current.Close - entryPrice) / entryPrice * balance;
                            balance += pnl;
                            trades.Add(new SimulatedTrade(entryPrice, current.Close, pnl > 0));

                            peakBalance = Math.Max(peakBalance, balance);
                            var drawdown = (peakBalance - balance) / peakBalance * 100;
                            maxDrawdown = Math.Max(maxDrawdown, drawdown);
                        }
                    }
                }
            }
            else
            {
                // Default strategy: simple mean reversion
                var priceChange = (current.Close - previous.Close) / previous.Close * 100;

                if (!inPosition && priceChange < -1.5m)
                {
                    inPosition = true;
                    entryPrice = current.Close;
                }
                else if (inPosition && priceChange > 1.0m)
                {
                    inPosition = false;
                    var pnl = (current.Close - entryPrice) / entryPrice * balance;
                    balance += pnl;
                    trades.Add(new SimulatedTrade(entryPrice, current.Close, pnl > 0));

                    peakBalance = Math.Max(peakBalance, balance);
                    var drawdown = (peakBalance - balance) / peakBalance * 100;
                    maxDrawdown = Math.Max(maxDrawdown, drawdown);
                }
            }
        }

        var totalTrades = trades.Count;
        var winRate = totalTrades > 0 ? (decimal)trades.Count(t => t.IsWin) / totalTrades * 100 : 0;
        var totalReturn = (balance - 100_000m) / 100_000m * 100;
        var sharpeRatio = totalTrades > 0
            ? Math.Round(totalReturn / (maxDrawdown == 0 ? 1 : maxDrawdown), 2)
            : 0;

        // Save result
        var run = await db.BacktestRuns.FindAsync(@event.BacktestRunId);
        if (run != null)
        {
            run.Status = BacktestRunStatus.Completed;
        }

        var result = new BacktestResult
        {
            BacktestRunId = @event.BacktestRunId,
            TotalTrades = totalTrades,
            WinRate = Math.Round(winRate, 2),
            TotalReturn = Math.Round(totalReturn, 2),
            MaxDrawdown = Math.Round(maxDrawdown, 2),
            SharpeRatio = sharpeRatio,
            ResultData = JsonSerializer.Serialize(trades)
        };

        db.BacktestResults.Add(result);
        await db.SaveChangesAsync();

        logger.LogInformation(
            "[BacktestDb] Backtest {RunId} completed: {Trades} trades, {WinRate}% win rate, {Return}% return",
            @event.BacktestRunId, totalTrades, winRate, totalReturn);

        return new BacktestExecuted(
            @event.BacktestRunId, @event.StrategyId,
            totalTrades, Math.Round(winRate, 2),
            Math.Round(totalReturn, 2), Math.Round(maxDrawdown, 2), sharpeRatio);
    }

    private record CandleData(decimal Open, decimal High, decimal Low, decimal Close, long Volume, DateTime Timestamp);
    private record SimulatedTrade(decimal EntryPrice, decimal ExitPrice, bool IsWin);
}
