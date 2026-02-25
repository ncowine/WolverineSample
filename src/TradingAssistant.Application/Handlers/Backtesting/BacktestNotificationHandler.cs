using Microsoft.Extensions.Logging;
using TradingAssistant.Contracts.Events;

namespace TradingAssistant.Application.Handlers.Backtesting;

public class BacktestNotificationHandler
{
    public static void Handle(
        BacktestExecuted @event,
        ILogger<BacktestNotificationHandler> logger)
    {
        logger.LogInformation(
            "🔔 [NOTIFICATION] Backtest {RunId} completed: {Trades} trades, {WinRate}% win rate, {Return}% total return",
            @event.BacktestRunId, @event.TotalTrades, @event.WinRate, @event.TotalReturn);
    }
}
