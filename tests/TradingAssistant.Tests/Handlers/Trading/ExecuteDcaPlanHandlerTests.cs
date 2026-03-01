using Microsoft.Extensions.Logging;
using NSubstitute;
using TradingAssistant.Application.Handlers.Trading;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Domain.Trading;
using TradingAssistant.Infrastructure.Persistence;
using TradingAssistant.Tests.Helpers;

namespace TradingAssistant.Tests.Handlers.Trading;

public class ExecuteDcaPlanHandlerTests
{
    private readonly FakeStockPriceCache _priceCache = new();
    private readonly ILogger _logger = Substitute.For<ILogger>();

    [Fact]
    public async Task Executes_successfully_creates_order_and_execution()
    {
        using var db = TestDbContextFactory.Create();
        var (account, plan) = SeedPlan(db, balance: 10_000m, amount: 500m, symbol: "AAPL");
        _priceCache.SetPrice("AAPL", 150m);

        var result = await ExecuteDcaPlanHandler.ExecuteAsync(plan, db, _priceCache, _logger);

        Assert.Equal(DcaExecutionStatus.Success, result.Status);
        Assert.NotNull(result.OrderId);
        Assert.Equal(150m, result.ExecutedPrice);
        Assert.Equal(3m, result.Quantity); // floor(500/150) = 3
        Assert.Null(result.ErrorReason);
    }

    [Fact]
    public async Task Calculates_correct_quantity_via_floor()
    {
        using var db = TestDbContextFactory.Create();
        var (account, plan) = SeedPlan(db, balance: 10_000m, amount: 1000m, symbol: "TSLA");
        _priceCache.SetPrice("TSLA", 175.50m);

        var result = await ExecuteDcaPlanHandler.ExecuteAsync(plan, db, _priceCache, _logger);

        // floor(1000 / 175.50) = floor(5.698...) = 5
        Assert.Equal(5m, result.Quantity);
        Assert.Equal(DcaExecutionStatus.Success, result.Status);
    }

    [Fact]
    public async Task Deducts_balance_and_creates_position()
    {
        using var db = TestDbContextFactory.Create();
        var (account, plan) = SeedPlan(db, balance: 10_000m, amount: 500m, symbol: "AAPL");
        _priceCache.SetPrice("AAPL", 150m);

        await ExecuteDcaPlanHandler.ExecuteAsync(plan, db, _priceCache, _logger);

        var refreshedAccount = db.Accounts.Single();
        // 3 shares * 150 = 450 cost, live fee = 150*3*0.001 = 0.45
        Assert.Equal(10_000m - 450m - 0.45m, refreshedAccount.Balance);

        var position = db.Positions.Single();
        Assert.Equal("AAPL", position.Symbol);
        Assert.Equal(3m, position.Quantity);
        Assert.Equal(150m, position.AverageEntryPrice);
    }

    [Fact]
    public async Task Pauses_plan_on_insufficient_funds()
    {
        using var db = TestDbContextFactory.Create();
        var (account, plan) = SeedPlan(db, balance: 100m, amount: 500m, symbol: "AAPL");
        _priceCache.SetPrice("AAPL", 150m);

        var result = await ExecuteDcaPlanHandler.ExecuteAsync(plan, db, _priceCache, _logger);

        Assert.Equal(DcaExecutionStatus.InsufficientFunds, result.Status);
        Assert.False(plan.IsActive);
        Assert.NotNull(result.ErrorReason);
    }

    [Fact]
    public async Task Returns_insufficient_funds_when_amount_too_small_for_one_share()
    {
        using var db = TestDbContextFactory.Create();
        var (account, plan) = SeedPlan(db, balance: 10_000m, amount: 10m, symbol: "AAPL");
        _priceCache.SetPrice("AAPL", 150m);

        var result = await ExecuteDcaPlanHandler.ExecuteAsync(plan, db, _priceCache, _logger);

        // floor(10/150) = 0 → InsufficientFunds
        Assert.Equal(DcaExecutionStatus.InsufficientFunds, result.Status);
        Assert.True(plan.IsActive); // Plan stays active — just the amount is too small, not a balance issue
    }

    [Fact]
    public async Task Deactivates_plan_when_stock_not_found()
    {
        using var db = TestDbContextFactory.Create();
        var (account, plan) = SeedPlan(db, balance: 10_000m, amount: 500m, symbol: "FAKE");
        // No price set for "FAKE"

        var result = await ExecuteDcaPlanHandler.ExecuteAsync(plan, db, _priceCache, _logger);

        Assert.Equal(DcaExecutionStatus.StockNotFound, result.Status);
        Assert.False(plan.IsActive);
        Assert.Contains("FAKE", result.ErrorReason!);
    }

    [Fact]
    public async Task Paper_account_gets_zero_fee()
    {
        using var db = TestDbContextFactory.Create();
        var (account, plan) = SeedPlan(db, balance: 10_000m, amount: 500m, symbol: "AAPL",
            accountType: AccountType.Paper);
        _priceCache.SetPrice("AAPL", 100m);

        await ExecuteDcaPlanHandler.ExecuteAsync(plan, db, _priceCache, _logger);

        var tradeExecution = db.TradeExecutions.Single();
        Assert.Equal(0m, tradeExecution.Fee);

        // 5 shares * 100 = 500, no fee
        var refreshedAccount = db.Accounts.Single();
        Assert.Equal(10_000m - 500m, refreshedAccount.Balance);
    }

    [Fact]
    public async Task Advances_next_execution_date_on_success()
    {
        using var db = TestDbContextFactory.Create();
        var (account, plan) = SeedPlan(db, balance: 10_000m, amount: 500m, symbol: "AAPL");
        _priceCache.SetPrice("AAPL", 100m);

        var originalNext = plan.NextExecutionDate;

        await ExecuteDcaPlanHandler.ExecuteAsync(plan, db, _priceCache, _logger);

        Assert.True(plan.NextExecutionDate > originalNext);
    }

    [Fact]
    public async Task Averages_into_existing_position()
    {
        using var db = TestDbContextFactory.Create();
        var (account, plan) = SeedPlan(db, balance: 20_000m, amount: 500m, symbol: "AAPL");

        // Seed existing position: 5 shares at $100
        db.Positions.Add(new Position
        {
            AccountId = account.Id,
            Symbol = "AAPL",
            Quantity = 5m,
            AverageEntryPrice = 100m,
            CurrentPrice = 100m,
            Status = PositionStatus.Open,
            OpenedAt = DateTime.UtcNow
        });
        db.SaveChanges();

        _priceCache.SetPrice("AAPL", 150m);

        await ExecuteDcaPlanHandler.ExecuteAsync(plan, db, _priceCache, _logger);

        var position = db.Positions.Single(p => p.Symbol == "AAPL" && p.Status == PositionStatus.Open);
        // Existing: 5 shares at 100 = 500, New: 3 shares at 150 = 450
        // Total: 8 shares, avg = (500 + 450) / 8 = 118.75
        Assert.Equal(8m, position.Quantity);
        Assert.Equal(118.75m, position.AverageEntryPrice);
    }

    private static (Account account, DcaPlan plan) SeedPlan(
        TradingDbContext db,
        decimal balance,
        decimal amount,
        string symbol,
        AccountType accountType = AccountType.Live,
        DcaFrequency frequency = DcaFrequency.Daily)
    {
        var account = new Account
        {
            UserId = Guid.NewGuid(),
            Name = "Test Account",
            Balance = balance,
            Currency = "USD",
            AccountType = accountType
        };
        db.Accounts.Add(account);

        var plan = new DcaPlan
        {
            AccountId = account.Id,
            Symbol = symbol,
            Amount = amount,
            Frequency = frequency,
            NextExecutionDate = DateTime.UtcNow.AddDays(-1), // Due for execution
            IsActive = true
        };
        db.DcaPlans.Add(plan);
        db.SaveChanges();

        return (account, plan);
    }
}
