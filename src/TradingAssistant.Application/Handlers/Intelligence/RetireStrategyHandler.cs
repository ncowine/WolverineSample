using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Intelligence;

public class RetireStrategyHandler
{
    public const decimal SharpeRetirementThreshold = 0.3m;
    public const int RollingSharpeWindowDays = 60;
    public const int ConsecutiveLosingMonths = 3;

    public static async Task<RetireStrategyResultDto> HandleAsync(
        RetireStrategyCommand command,
        IntelligenceDbContext db,
        ILogger<RetireStrategyHandler> logger)
    {
        var entry = await db.TournamentEntries
            .FirstOrDefaultAsync(e => e.Id == command.EntryId);

        if (entry is null)
        {
            return Fail(Guid.Empty, "", "Tournament entry not found.");
        }

        if (entry.Status == TournamentStatus.Retired)
        {
            return Fail(entry.Id, entry.StrategyName, "Strategy is already retired.");
        }

        // Determine retirement reason
        string retirementReason;

        if (command.Force || !string.IsNullOrWhiteSpace(command.Reason))
        {
            retirementReason = command.Reason ?? "Manual retirement";
        }
        else
        {
            // Evaluate automatic retirement criteria
            var reasons = EvaluateRetirementCriteria(
                entry.SharpeRatio, entry.DaysActive, entry.EquityCurveJson);

            if (reasons.Count == 0)
            {
                return Fail(entry.Id, entry.StrategyName,
                    "Strategy does not meet retirement criteria. Use Force=true for manual retirement.");
            }

            retirementReason = string.Join("; ", reasons);
        }

        // Retire
        entry.Status = TournamentStatus.Retired;
        entry.RetiredAt = DateTime.UtcNow;
        entry.RetirementReason = retirementReason;

        await db.SaveChangesAsync();

        logger.LogInformation(
            "Strategy '{Strategy}' retired from {Market}: {Reason}",
            entry.StrategyName, entry.MarketCode, retirementReason);

        return new RetireStrategyResultDto(
            Success: true,
            EntryId: entry.Id,
            StrategyName: entry.StrategyName,
            RetirementReason: retirementReason);
    }

    internal static List<string> EvaluateRetirementCriteria(
        decimal sharpeRatio, int daysActive, string equityCurveJson)
    {
        var reasons = new List<string>();

        // Criterion 1: Sharpe < 0.3 over rolling 60 days
        if (daysActive >= RollingSharpeWindowDays && sharpeRatio < SharpeRetirementThreshold)
        {
            reasons.Add($"Sharpe ratio {sharpeRatio:F2} below {SharpeRetirementThreshold} threshold over {daysActive} days");
        }

        // Criterion 2: 3 consecutive losing months
        var losingMonths = CountConsecutiveLosingMonths(equityCurveJson);
        if (losingMonths >= ConsecutiveLosingMonths)
        {
            reasons.Add($"{losingMonths} consecutive losing months");
        }

        return reasons;
    }

    internal static int CountConsecutiveLosingMonths(string equityCurveJson)
    {
        if (string.IsNullOrWhiteSpace(equityCurveJson) || equityCurveJson == "[]")
            return 0;

        try
        {
            var opts = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var curve = JsonSerializer.Deserialize<List<EquityPoint>>(equityCurveJson, opts);
            if (curve is null || curve.Count < 2)
                return 0;

            // Group by month and compute monthly returns
            var monthlyReturns = new List<decimal>();
            var grouped = curve
                .GroupBy(p => p.Date[..7]) // "2026-03"
                .OrderBy(g => g.Key)
                .ToList();

            for (int i = 1; i < grouped.Count; i++)
            {
                var prevEnd = grouped[i - 1].Last().Value;
                var curEnd = grouped[i].Last().Value;
                if (prevEnd > 0)
                    monthlyReturns.Add((curEnd - prevEnd) / prevEnd);
            }

            // Count max consecutive losing months from the end
            var consecutive = 0;
            for (int i = monthlyReturns.Count - 1; i >= 0; i--)
            {
                if (monthlyReturns[i] < 0)
                    consecutive++;
                else
                    break;
            }

            return consecutive;
        }
        catch
        {
            return 0;
        }
    }

    private static RetireStrategyResultDto Fail(Guid entryId, string name, string error)
    {
        return new RetireStrategyResultDto(
            Success: false, EntryId: entryId, StrategyName: name,
            RetirementReason: string.Empty, Error: error);
    }

    internal record EquityPoint(string Date, decimal Value);
}
