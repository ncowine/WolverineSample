using Microsoft.Extensions.Logging.Abstractions;
using TradingAssistant.Application.Handlers.Trading;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.Events;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Domain.Trading;
using TradingAssistant.Infrastructure.Persistence;
using TradingAssistant.Tests.Helpers;

namespace TradingAssistant.Tests.Handlers.Trading;

public class PaperOrderExecutionTests
{
    private readonly FakeCurrentUser _user = new();
    private readonly FakeStockPriceCache _priceCache = new();

    [Fact]
    public async Task Paper_order_uses_market_price_from_cache()
    {
        using var db = TestDbContextFactory.Create();
        var account = CreatePaperAccount(db);
        _priceCache.SetPrice("AAPL", 175.50m);

        var command = new PlaceOrderCommand(account.Id, "AAPL", "Buy", "Market", 10, null);
        var (orderPlaced, _) = await PlaceOrderHandler.HandleAsync(command, db, _user, _priceCache);

        Assert.Equal(175.50m, orderPlaced.Price);
    }

    [Fact]
    public async Task Paper_order_ignores_command_price_and_uses_market_price()
    {
        using var db = TestDbContextFactory.Create();
        var account = CreatePaperAccount(db);
        _priceCache.SetPrice("AAPL", 175.50m);

        var command = new PlaceOrderCommand(account.Id, "AAPL", "Buy", "Limit", 10, 999.99m);
        var (orderPlaced, _) = await PlaceOrderHandler.HandleAsync(command, db, _user, _priceCache);

        Assert.Equal(175.50m, orderPlaced.Price);
    }

    [Fact]
    public async Task Paper_fill_has_zero_fee()
    {
        using var db = TestDbContextFactory.Create();
        var (account, order) = CreatePaperAccountWithOrder(db, 175.50m);

        var orderPlaced = new OrderPlaced(order.Id, account.Id, "AAPL", "Buy", 10, 175.50m);
        var logger = NullLogger<FillOrderHandler>.Instance;

        var result = await FillOrderHandler.HandleAsync(orderPlaced, db, logger);

        Assert.Equal(0m, result.Fee);
        var execution = db.TradeExecutions.Single();
        Assert.Equal(0m, execution.Fee);
    }

    [Fact]
    public async Task Live_fill_retains_standard_fee()
    {
        using var db = TestDbContextFactory.Create();
        var (account, order) = CreateLiveAccountWithOrder(db, 175.50m);

        var orderPlaced = new OrderPlaced(order.Id, account.Id, "AAPL", "Buy", 10, 175.50m);
        var logger = NullLogger<FillOrderHandler>.Instance;

        var result = await FillOrderHandler.HandleAsync(orderPlaced, db, logger);

        // 0.1% of 175.50 * 10 = 0.1755 → rounds to 1.76
        var expectedFee = Math.Round(175.50m * 10 * 0.001m, 2);
        Assert.Equal(expectedFee, result.Fee);
    }

    [Fact]
    public async Task Paper_fill_creates_position_and_updates_portfolio()
    {
        using var db = TestDbContextFactory.Create();
        var (account, order) = CreatePaperAccountWithOrder(db, 175.50m);

        var portfolio = new Portfolio
        {
            AccountId = account.Id,
            TotalValue = 100_000m,
            CashBalance = 100_000m - (175.50m * 10),
            InvestedValue = 0m,
            TotalPnL = 0m
        };
        db.Portfolios.Add(portfolio);
        await db.SaveChangesAsync();

        var orderPlaced = new OrderPlaced(order.Id, account.Id, "AAPL", "Buy", 10, 175.50m);
        var logger = NullLogger<FillOrderHandler>.Instance;

        await FillOrderHandler.HandleAsync(orderPlaced, db, logger);

        var position = db.Positions.Single();
        Assert.Equal("AAPL", position.Symbol);
        Assert.Equal(10m, position.Quantity);
        Assert.Equal(175.50m, position.AverageEntryPrice);
        Assert.Equal(PositionStatus.Open, position.Status);

        var updatedPortfolio = db.Portfolios.Single();
        Assert.Equal(account.Balance, updatedPortfolio.CashBalance);
    }

    [Fact]
    public async Task Paper_order_throws_when_no_market_price_available()
    {
        using var db = TestDbContextFactory.Create();
        var account = CreatePaperAccount(db);
        // No price set in cache for "UNKNOWN"

        var command = new PlaceOrderCommand(account.Id, "UNKNOWN", "Buy", "Market", 10, null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => PlaceOrderHandler.HandleAsync(command, db, _user, _priceCache));
    }

    private Account CreatePaperAccount(TradingDbContext db)
    {
        var account = new Account
        {
            UserId = _user.UserId,
            Name = "Paper Account",
            Balance = 100_000m,
            Currency = "USD",
            AccountType = AccountType.Paper
        };
        db.Accounts.Add(account);
        db.SaveChanges();
        return account;
    }

    private (Account account, Order order) CreatePaperAccountWithOrder(TradingDbContext db, decimal price)
    {
        var account = new Account
        {
            UserId = _user.UserId,
            Name = "Paper Account",
            Balance = 100_000m - (price * 10),
            Currency = "USD",
            AccountType = AccountType.Paper
        };
        db.Accounts.Add(account);

        var order = new Order
        {
            AccountId = account.Id,
            Symbol = "AAPL",
            Side = OrderSide.Buy,
            Type = OrderType.Market,
            Quantity = 10,
            Price = price,
            Status = OrderStatus.Pending
        };
        db.Orders.Add(order);
        db.SaveChanges();
        return (account, order);
    }

    private (Account account, Order order) CreateLiveAccountWithOrder(TradingDbContext db, decimal price)
    {
        var account = new Account
        {
            UserId = _user.UserId,
            Name = "Live Account",
            Balance = 100_000m - (price * 10),
            Currency = "USD",
            AccountType = AccountType.Live
        };
        db.Accounts.Add(account);

        var order = new Order
        {
            AccountId = account.Id,
            Symbol = "AAPL",
            Side = OrderSide.Buy,
            Type = OrderType.Market,
            Quantity = 10,
            Price = price,
            Status = OrderStatus.Pending
        };
        db.Orders.Add(order);
        db.SaveChanges();
        return (account, order);
    }
}
