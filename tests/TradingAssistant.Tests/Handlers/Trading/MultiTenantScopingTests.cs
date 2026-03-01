using TradingAssistant.Application.Exceptions;
using TradingAssistant.Application.Handlers.Trading;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Domain.Identity;
using TradingAssistant.Domain.Trading;
using TradingAssistant.Tests.Helpers;

namespace TradingAssistant.Tests.Handlers.Trading;

public class MultiTenantScopingTests
{
    private readonly FakeCurrentUser _owner = new();
    private readonly FakeCurrentUser _intruder = new();
    private readonly FakeStockPriceCache _priceCache = new();

    private (Account account, User owner) SeedAccountWithOwner(Infrastructure.Persistence.TradingDbContext db)
    {
        var user = new User
        {
            Email = "owner@test.com",
            PasswordHash = "hashed",
            Role = "User"
        };
        db.Users.Add(user);

        var account = new Account
        {
            UserId = user.Id,
            Name = "Test Account",
            Balance = 50_000m,
            Currency = "USD"
        };
        db.Accounts.Add(account);
        db.SaveChanges();

        _owner.UserId = user.Id;
        return (account, user);
    }

    [Fact]
    public async Task GetPortfolioHandler_ThrowsForbidden_ForWrongUser()
    {
        using var db = TestDbContextFactory.Create();
        var (account, _) = SeedAccountWithOwner(db);

        var query = new GetPortfolioQuery(account.Id);

        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => GetPortfolioHandler.HandleAsync(query, null!, db, _intruder));
    }

    [Fact]
    public async Task GetOrderHistoryHandler_ThrowsForbidden_ForWrongUser()
    {
        using var db = TestDbContextFactory.Create();
        var (account, _) = SeedAccountWithOwner(db);

        var query = new GetOrderHistoryQuery(account.Id);

        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => GetOrderHistoryHandler.HandleAsync(query, db, _intruder));
    }

    [Fact]
    public async Task GetPositionsHandler_ThrowsForbidden_ForWrongUser()
    {
        using var db = TestDbContextFactory.Create();
        var (account, _) = SeedAccountWithOwner(db);

        var query = new GetPositionsQuery(account.Id);

        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => GetPositionsHandler.HandleAsync(query, db, _intruder));
    }

    [Fact]
    public async Task PlaceOrderHandler_ThrowsForbidden_ForWrongUser()
    {
        using var db = TestDbContextFactory.Create();
        var (account, _) = SeedAccountWithOwner(db);

        var command = new PlaceOrderCommand(account.Id, "AAPL", "Buy", "Market", 10, 150m);

        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => PlaceOrderHandler.HandleAsync(command, db, _intruder, _priceCache));
    }

    [Fact]
    public async Task CancelOrderHandler_ThrowsForbidden_ForWrongUser()
    {
        using var db = TestDbContextFactory.Create();
        var (account, _) = SeedAccountWithOwner(db);

        var order = new Order
        {
            AccountId = account.Id,
            Symbol = "AAPL",
            Side = OrderSide.Buy,
            Type = OrderType.Market,
            Quantity = 10,
            Price = 150m,
            Status = OrderStatus.Pending
        };
        db.Orders.Add(order);
        db.SaveChanges();

        var command = new CancelOrderCommand(order.Id);

        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => CancelOrderHandler.HandleAsync(command, db, _intruder));
    }

    [Fact]
    public async Task ClosePositionHandler_ThrowsForbidden_ForWrongUser()
    {
        using var db = TestDbContextFactory.Create();
        var (account, _) = SeedAccountWithOwner(db);

        var position = new Position
        {
            AccountId = account.Id,
            Symbol = "AAPL",
            Quantity = 10,
            AverageEntryPrice = 150m,
            CurrentPrice = 160m,
            Status = PositionStatus.Open,
            OpenedAt = DateTime.UtcNow
        };
        db.Positions.Add(position);
        db.SaveChanges();

        var command = new ClosePositionCommand(position.Id);

        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => ClosePositionHandler.HandleAsync(command, db, _intruder));
    }
}
