using TradingAssistant.Application.Handlers.Trading;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Domain.MarketData;
using TradingAssistant.Domain.Trading;
using TradingAssistant.Tests.Helpers;

namespace TradingAssistant.Tests.Handlers.Trading;

public class CreateDcaPlanHandlerTests
{
    private readonly FakeCurrentUser _user = new();

    [Fact]
    public async Task Creates_dca_plan_with_correct_fields()
    {
        using var tradingDb = TestDbContextFactory.Create();
        using var marketDb = TestMarketDataDbContextFactory.Create();
        SeedAccount(tradingDb);
        SeedStock(marketDb, "AAPL");

        var account = tradingDb.Accounts.Single();
        var command = new CreateDcaPlanCommand(account.Id, "AAPL", 500m, "Weekly");

        var result = await CreateDcaPlanHandler.HandleAsync(command, tradingDb, marketDb, _user);

        Assert.Equal(account.Id, result.AccountId);
        Assert.Equal("AAPL", result.Symbol);
        Assert.Equal(500m, result.Amount);
        Assert.Equal("Weekly", result.Frequency);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task Creates_plan_with_is_active_true()
    {
        using var tradingDb = TestDbContextFactory.Create();
        using var marketDb = TestMarketDataDbContextFactory.Create();
        SeedAccount(tradingDb);
        SeedStock(marketDb, "AAPL");

        var account = tradingDb.Accounts.Single();
        var command = new CreateDcaPlanCommand(account.Id, "AAPL", 100m, "Daily");

        await CreateDcaPlanHandler.HandleAsync(command, tradingDb, marketDb, _user);

        var plan = tradingDb.DcaPlans.Single();
        Assert.True(plan.IsActive);
        Assert.Equal(DcaFrequency.Daily, plan.Frequency);
    }

    [Fact]
    public async Task Normalizes_symbol_to_uppercase()
    {
        using var tradingDb = TestDbContextFactory.Create();
        using var marketDb = TestMarketDataDbContextFactory.Create();
        SeedAccount(tradingDb);
        SeedStock(marketDb, "AAPL");

        var account = tradingDb.Accounts.Single();
        var command = new CreateDcaPlanCommand(account.Id, "aapl", 500m, "Monthly");

        var result = await CreateDcaPlanHandler.HandleAsync(command, tradingDb, marketDb, _user);

        Assert.Equal("AAPL", result.Symbol);
    }

    [Fact]
    public async Task Throws_when_symbol_not_found()
    {
        using var tradingDb = TestDbContextFactory.Create();
        using var marketDb = TestMarketDataDbContextFactory.Create();
        SeedAccount(tradingDb);

        var account = tradingDb.Accounts.Single();
        var command = new CreateDcaPlanCommand(account.Id, "FAKE", 500m, "Daily");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateDcaPlanHandler.HandleAsync(command, tradingDb, marketDb, _user));
    }

    [Fact]
    public async Task Calculates_next_execution_in_the_future()
    {
        using var tradingDb = TestDbContextFactory.Create();
        using var marketDb = TestMarketDataDbContextFactory.Create();
        SeedAccount(tradingDb);
        SeedStock(marketDb, "AAPL");

        var account = tradingDb.Accounts.Single();
        var command = new CreateDcaPlanCommand(account.Id, "AAPL", 500m, "Daily");

        var result = await CreateDcaPlanHandler.HandleAsync(command, tradingDb, marketDb, _user);

        Assert.True(result.NextExecutionDate > DateTime.UtcNow.Date);
    }

    [Theory]
    [InlineData(DcaFrequency.Daily, 1)]
    [InlineData(DcaFrequency.Biweekly, 14)]
    public void CalculateNextExecution_returns_correct_offset(DcaFrequency frequency, int expectedDays)
    {
        var result = CreateDcaPlanHandler.CalculateNextExecution(frequency);
        var expected = DateTime.UtcNow.Date.AddDays(expectedDays);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void CalculateNextExecution_monthly_returns_next_month()
    {
        var result = CreateDcaPlanHandler.CalculateNextExecution(DcaFrequency.Monthly);
        var expected = DateTime.UtcNow.Date.AddMonths(1);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void CalculateNextExecution_weekly_returns_next_monday()
    {
        var result = CreateDcaPlanHandler.CalculateNextExecution(DcaFrequency.Weekly);

        Assert.Equal(DayOfWeek.Monday, result.DayOfWeek);
        Assert.True(result > DateTime.UtcNow.Date);
    }

    private void SeedAccount(Infrastructure.Persistence.TradingDbContext db)
    {
        db.Accounts.Add(new Account
        {
            UserId = _user.UserId,
            Name = "Test Account",
            Balance = 100_000m,
            Currency = "USD",
            AccountType = AccountType.Live
        });
        db.SaveChanges();
    }

    private static void SeedStock(Infrastructure.Persistence.MarketDataDbContext db, string symbol)
    {
        db.Stocks.Add(new Stock
        {
            Symbol = symbol,
            Name = $"{symbol} Inc.",
            Exchange = "NASDAQ",
            Sector = "Technology"
        });
        db.SaveChanges();
    }
}
