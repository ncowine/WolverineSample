using TradingAssistant.Application.Handlers.Trading;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Tests.Helpers;

namespace TradingAssistant.Tests.Handlers.Trading;

public class CreatePaperAccountHandlerTests
{
    private readonly FakeCurrentUser _user = new();

    [Fact]
    public async Task Creates_paper_account_with_default_balance()
    {
        using var db = TestDbContextFactory.Create();
        var command = new CreatePaperAccountCommand();

        var result = await CreatePaperAccountHandler.HandleAsync(command, db, _user);

        Assert.Equal(100_000m, result.Balance);
        Assert.Equal("Paper Trading Account", result.Name);
        Assert.Equal("Paper", result.AccountType);
    }

    [Fact]
    public async Task Creates_paper_account_with_custom_balance()
    {
        using var db = TestDbContextFactory.Create();
        var command = new CreatePaperAccountCommand(StartingBalance: 50_000m);

        var result = await CreatePaperAccountHandler.HandleAsync(command, db, _user);

        Assert.Equal(50_000m, result.Balance);
    }

    [Fact]
    public async Task Creates_paper_account_with_custom_name()
    {
        using var db = TestDbContextFactory.Create();
        var command = new CreatePaperAccountCommand(Name: "My Strategy Test");

        var result = await CreatePaperAccountHandler.HandleAsync(command, db, _user);

        Assert.Equal("My Strategy Test", result.Name);
    }

    [Fact]
    public async Task Creates_portfolio_alongside_account()
    {
        using var db = TestDbContextFactory.Create();
        var command = new CreatePaperAccountCommand(StartingBalance: 75_000m);

        var result = await CreatePaperAccountHandler.HandleAsync(command, db, _user);

        var portfolio = db.Portfolios.Single();
        Assert.Equal(result.Id, portfolio.AccountId);
        Assert.Equal(75_000m, portfolio.CashBalance);
        Assert.Equal(75_000m, portfolio.TotalValue);
        Assert.Equal(0m, portfolio.InvestedValue);
    }

    [Fact]
    public async Task Account_entity_has_paper_type()
    {
        using var db = TestDbContextFactory.Create();
        var command = new CreatePaperAccountCommand();

        await CreatePaperAccountHandler.HandleAsync(command, db, _user);

        var account = db.Accounts.Single();
        Assert.Equal(AccountType.Paper, account.AccountType);
        Assert.Equal(_user.UserId, account.UserId);
    }
}
