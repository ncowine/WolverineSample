using TradingAssistant.Application.Services;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Domain.Trading;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Trading;

public class CreatePaperAccountHandler
{
    public static async Task<AccountDto> HandleAsync(
        CreatePaperAccountCommand command,
        TradingDbContext db,
        ICurrentUser currentUser)
    {
        var balance = command.StartingBalance ?? 100_000m;
        var name = command.Name?.Trim() ?? "Paper Trading Account";

        var account = new Account
        {
            UserId = currentUser.UserId,
            Name = name,
            Balance = balance,
            Currency = "USD",
            AccountType = AccountType.Paper
        };

        var portfolio = new Portfolio
        {
            AccountId = account.Id,
            TotalValue = balance,
            CashBalance = balance,
            InvestedValue = 0m,
            TotalPnL = 0m
        };

        db.Accounts.Add(account);
        db.Portfolios.Add(portfolio);

        await db.SaveChangesAsync();

        return new AccountDto(
            account.Id,
            account.Name,
            account.Balance,
            account.Currency,
            account.AccountType.ToString());
    }
}
