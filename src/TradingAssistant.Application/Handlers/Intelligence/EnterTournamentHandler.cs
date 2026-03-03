using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.Domain.Trading;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Intelligence;

public class EnterTournamentHandler
{
    public static async Task<EnterTournamentResultDto> HandleAsync(
        EnterTournamentCommand command,
        BacktestDbContext backtestDb,
        TradingDbContext tradingDb,
        IntelligenceDbContext intelligenceDb,
        ILogger<EnterTournamentHandler> logger)
    {
        // 1. Validate tournament exists and is active
        var tournament = await intelligenceDb.TournamentRuns
            .FirstOrDefaultAsync(t => t.Id == command.TournamentRunId);

        if (tournament is null)
        {
            return Fail("", $"Tournament '{command.TournamentRunId}' not found.");
        }

        if (tournament.Status != TournamentRunStatus.Active)
        {
            return Fail("", $"Tournament is {tournament.Status}, not accepting entries.");
        }

        // 2. Check capacity
        var currentEntryCount = await intelligenceDb.TournamentEntries
            .CountAsync(e => e.TournamentRunId == command.TournamentRunId);

        if (currentEntryCount >= tournament.MaxEntries)
        {
            return Fail("", $"Tournament is full ({tournament.MaxEntries} entries max).");
        }

        // 3. Validate strategy exists
        var strategy = await backtestDb.Strategies
            .FirstOrDefaultAsync(s => s.Id == command.StrategyId);

        if (strategy is null)
        {
            return Fail("", $"Strategy '{command.StrategyId}' not found.");
        }

        // 4. Check duplicate entry
        var alreadyEntered = await intelligenceDb.TournamentEntries
            .AnyAsync(e => e.TournamentRunId == command.TournamentRunId
                        && e.StrategyId == command.StrategyId);

        if (alreadyEntered)
        {
            return Fail(strategy.Name,
                $"Strategy '{strategy.Name}' is already entered in this tournament.");
        }

        // 5. Create isolated paper account in TradingDbContext
        var balance = command.PaperAccountBalance > 0 ? command.PaperAccountBalance : 100_000m;

        var account = new Account
        {
            UserId = Guid.Empty, // Tournament accounts are system-owned
            Name = $"Tournament: {strategy.Name}",
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

        tradingDb.Accounts.Add(account);
        tradingDb.Portfolios.Add(portfolio);
        await tradingDb.SaveChangesAsync();

        // 6. Create tournament entry in IntelligenceDbContext
        var entry = new TournamentEntry
        {
            TournamentRunId = command.TournamentRunId,
            StrategyId = command.StrategyId,
            PaperAccountId = account.Id,
            MarketCode = tournament.MarketCode,
            StartDate = DateTime.UtcNow,
            Status = TournamentStatus.Active,
            AllocationPercent = 25m
        };

        intelligenceDb.TournamentEntries.Add(entry);

        // 7. Update entry count on tournament run
        tournament.EntryCount = currentEntryCount + 1;
        await intelligenceDb.SaveChangesAsync();

        logger.LogInformation(
            "Strategy '{Strategy}' entered tournament {Tournament} with paper account {Account}",
            strategy.Name, tournament.Id, account.Id);

        return new EnterTournamentResultDto(
            Success: true,
            EntryId: entry.Id,
            PaperAccountId: account.Id,
            StrategyName: strategy.Name);
    }

    private static EnterTournamentResultDto Fail(string strategyName, string error)
    {
        return new EnterTournamentResultDto(
            Success: false, EntryId: null, PaperAccountId: null,
            StrategyName: strategyName, Error: error);
    }
}
