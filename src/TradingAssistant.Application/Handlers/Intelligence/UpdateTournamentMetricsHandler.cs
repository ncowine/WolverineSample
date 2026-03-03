using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Intelligence;

public class UpdateTournamentMetricsHandler
{
    public const int MinPromotionDays = 30;
    public const decimal AnnualizationFactor = 252m; // Trading days per year

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task HandleAsync(
        UpdateTournamentMetricsCommand command,
        TradingDbContext tradingDb,
        IntelligenceDbContext intelligenceDb,
        ILogger<UpdateTournamentMetricsHandler> logger)
    {
        var entry = await intelligenceDb.TournamentEntries
            .FirstOrDefaultAsync(e => e.Id == command.TournamentEntryId);

        if (entry is null)
        {
            logger.LogWarning("Tournament entry {EntryId} not found", command.TournamentEntryId);
            return;
        }

        // 1. Get portfolio value from TradingDbContext
        var portfolio = await tradingDb.Portfolios
            .FirstOrDefaultAsync(p => p.AccountId == entry.PaperAccountId);

        if (portfolio is null)
        {
            logger.LogWarning("Portfolio for account {AccountId} not found", entry.PaperAccountId);
            return;
        }

        var account = await tradingDb.Accounts.FindAsync(entry.PaperAccountId);
        var initialBalance = account?.Balance ?? 100_000m;
        var currentValue = portfolio.TotalValue;

        // 2. Compute days active
        entry.DaysActive = (int)(DateTime.UtcNow - entry.StartDate).TotalDays;

        // 3. Compute total return
        entry.TotalReturn = initialBalance > 0
            ? ((currentValue - initialBalance) / initialBalance) * 100m
            : 0m;

        // 4. Count trades and win rate from filled orders
        var filledOrders = await tradingDb.Orders
            .Where(o => o.AccountId == entry.PaperAccountId && o.Status == OrderStatus.Filled)
            .Include(o => o.TradeExecutions)
            .ToListAsync();

        entry.TotalTrades = filledOrders.Count;

        // Simple win rate: sell orders with positive P&L
        var sellOrders = filledOrders.Where(o => o.Side == OrderSide.Sell).ToList();
        if (sellOrders.Count > 0)
        {
            var winningTrades = sellOrders.Count(o =>
                o.TradeExecutions.Sum(t => t.Price * t.Quantity - t.Fee) > 0);
            entry.WinRate = (decimal)winningTrades / sellOrders.Count;
        }

        // 5. Append today's equity point to curve
        var equityCurve = DeserializeEquityCurve(entry.EquityCurveJson);
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        // Replace today's entry if already exists, otherwise append
        var existingIdx = equityCurve.FindIndex(p => p.Date == today);
        if (existingIdx >= 0)
            equityCurve[existingIdx] = new EquityPoint(today, currentValue);
        else
            equityCurve.Add(new EquityPoint(today, currentValue));

        entry.EquityCurveJson = JsonSerializer.Serialize(equityCurve, JsonOpts);

        // 6. Compute Sharpe ratio from daily returns
        entry.SharpeRatio = ComputeSharpe(equityCurve);

        // 7. Compute max drawdown from equity curve
        entry.MaxDrawdown = ComputeMaxDrawdown(equityCurve);

        await intelligenceDb.SaveChangesAsync();

        logger.LogInformation(
            "Updated metrics for entry {EntryId}: Return={Return:F2}%, Sharpe={Sharpe:F2}, DD={DD:F2}%",
            entry.Id, entry.TotalReturn, entry.SharpeRatio, entry.MaxDrawdown);
    }

    internal static decimal ComputeSharpe(List<EquityPoint> curve)
    {
        if (curve.Count < 2)
            return 0m;

        var dailyReturns = new List<decimal>();
        for (int i = 1; i < curve.Count; i++)
        {
            if (curve[i - 1].Value != 0)
            {
                var ret = (curve[i].Value - curve[i - 1].Value) / curve[i - 1].Value;
                dailyReturns.Add(ret);
            }
        }

        if (dailyReturns.Count < 2)
            return 0m;

        var mean = dailyReturns.Average();
        var variance = dailyReturns.Sum(r => (r - mean) * (r - mean)) / (dailyReturns.Count - 1);
        var stdDev = (decimal)Math.Sqrt((double)variance);

        if (stdDev == 0)
            return 0m;

        // Annualized Sharpe (assuming risk-free rate = 0)
        return (mean / stdDev) * (decimal)Math.Sqrt((double)AnnualizationFactor);
    }

    internal static decimal ComputeMaxDrawdown(List<EquityPoint> curve)
    {
        if (curve.Count < 2)
            return 0m;

        var peak = curve[0].Value;
        var maxDd = 0m;

        foreach (var point in curve)
        {
            if (point.Value > peak)
                peak = point.Value;

            if (peak > 0)
            {
                var dd = (peak - point.Value) / peak * 100m;
                if (dd > maxDd)
                    maxDd = dd;
            }
        }

        return maxDd;
    }

    internal static List<EquityPoint> DeserializeEquityCurve(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<EquityPoint>>(json, JsonOpts) ?? [];
        }
        catch
        {
            return [];
        }
    }

    internal record EquityPoint(string Date, decimal Value);
}
