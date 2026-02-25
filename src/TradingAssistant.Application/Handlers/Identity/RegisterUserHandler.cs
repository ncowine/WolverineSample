using Microsoft.EntityFrameworkCore;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Domain.Identity;
using TradingAssistant.Domain.Trading;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Identity;

public class RegisterUserHandler
{
    public static async Task<AuthResponseDto> HandleAsync(
        RegisterUserCommand command,
        TradingDbContext db)
    {
        var emailLower = command.Email.Trim().ToLowerInvariant();

        var exists = await db.Users.AnyAsync(u => u.Email == emailLower);
        if (exists)
            throw new InvalidOperationException("A user with this email already exists.");

        var user = new User
        {
            Email = emailLower,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(command.Password),
            Role = "User"
        };

        var account = new Account
        {
            UserId = user.Id,
            Name = $"{emailLower}'s Trading Account",
            Balance = 100_000m,
            Currency = "USD"
        };

        var portfolio = new Portfolio
        {
            AccountId = account.Id,
            TotalValue = 100_000m,
            CashBalance = 100_000m,
            InvestedValue = 0m,
            TotalPnL = 0m
        };

        db.Users.Add(user);
        db.Accounts.Add(account);
        db.Portfolios.Add(portfolio);

        await db.SaveChangesAsync();

        return new AuthResponseDto(user.Id, user.Email, user.Role, account.Id);
    }
}
