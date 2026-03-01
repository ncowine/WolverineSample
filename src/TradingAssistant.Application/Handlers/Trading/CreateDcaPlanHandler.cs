using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application.Exceptions;
using TradingAssistant.Application.Services;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Domain.Trading;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Trading;

public class CreateDcaPlanHandler
{
    public static async Task<DcaPlanDto> HandleAsync(
        CreateDcaPlanCommand command,
        TradingDbContext tradingDb,
        MarketDataDbContext marketDb,
        ICurrentUser currentUser)
    {
        var account = await tradingDb.Accounts.FindAsync(command.AccountId)
            ?? throw new InvalidOperationException($"Account '{command.AccountId}' not found.");

        if (account.UserId != currentUser.UserId)
            throw new ForbiddenAccessException("You do not have access to this account.");

        var symbolUpper = command.Symbol.Trim().ToUpperInvariant();

        var stockExists = await marketDb.Stocks.AnyAsync(s => s.Symbol == symbolUpper);
        if (!stockExists)
            throw new InvalidOperationException($"Stock symbol '{symbolUpper}' not found.");

        if (!Enum.TryParse<DcaFrequency>(command.Frequency, true, out var frequency))
            throw new InvalidOperationException($"Invalid frequency: {command.Frequency}");

        var nextExecution = CalculateNextExecution(frequency);

        var plan = new DcaPlan
        {
            AccountId = command.AccountId,
            Symbol = symbolUpper,
            Amount = command.Amount,
            Frequency = frequency,
            NextExecutionDate = nextExecution,
            IsActive = true
        };

        tradingDb.DcaPlans.Add(plan);
        await tradingDb.SaveChangesAsync();

        return new DcaPlanDto(
            plan.Id,
            plan.AccountId,
            plan.Symbol,
            plan.Amount,
            plan.Frequency.ToString(),
            plan.NextExecutionDate,
            plan.IsActive,
            plan.CreatedAt);
    }

    internal static DateTime CalculateNextExecution(DcaFrequency frequency)
    {
        var today = DateTime.UtcNow.Date;

        return frequency switch
        {
            DcaFrequency.Daily => today.AddDays(1),
            DcaFrequency.Weekly => NextWeekday(today, DayOfWeek.Monday),
            DcaFrequency.Biweekly => today.AddDays(14),
            DcaFrequency.Monthly => today.AddMonths(1),
            _ => today.AddDays(1)
        };
    }

    private static DateTime NextWeekday(DateTime from, DayOfWeek target)
    {
        var daysUntil = ((int)target - (int)from.DayOfWeek + 7) % 7;
        if (daysUntil == 0) daysUntil = 7;
        return from.AddDays(daysUntil);
    }
}
