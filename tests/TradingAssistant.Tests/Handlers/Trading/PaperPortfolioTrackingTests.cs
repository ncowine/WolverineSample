using Microsoft.Extensions.Logging.Abstractions;
using TradingAssistant.Application.Handlers.Trading;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.Events;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Domain.Trading;
using TradingAssistant.Infrastructure.Persistence;
using TradingAssistant.Tests.Helpers;

namespace TradingAssistant.Tests.Handlers.Trading;

public class PaperPortfolioTrackingTests
{
    private readonly FakeCurrentUser _user = new();

    [Fact]
    public async Task Order_history_shows_paper_account_type()
    {
        using var db = TestDbContextFactory.Create();
        var account = SeedPaperAccountWithOrder(db);

        var query = new GetOrderHistoryQuery(account.Id);
        var result = await GetOrderHistoryHandler.HandleAsync(query, db, _user);

        Assert.Single(result.Items);
        Assert.Equal("Paper", result.Items[0].AccountType);
    }

    [Fact]
    public async Task Order_history_shows_live_account_type()
    {
        using var db = TestDbContextFactory.Create();
        var account = SeedLiveAccountWithOrder(db);

        var query = new GetOrderHistoryQuery(account.Id);
        var result = await GetOrderHistoryHandler.HandleAsync(query, db, _user);

        Assert.Single(result.Items);
        Assert.Equal("Live", result.Items[0].AccountType);
    }

    [Fact]
    public async Task Positions_show_paper_account_type()
    {
        using var db = TestDbContextFactory.Create();
        var account = SeedPaperAccountWithPosition(db);

        var query = new GetPositionsQuery(account.Id);
        var result = await GetPositionsHandler.HandleAsync(query, db, _user);

        Assert.Single(result);
        Assert.Equal("Paper", result[0].AccountType);
    }

    [Fact]
    public async Task Positions_show_live_account_type()
    {
        using var db = TestDbContextFactory.Create();
        var account = SeedLiveAccountWithPosition(db);

        var query = new GetPositionsQuery(account.Id);
        var result = await GetPositionsHandler.HandleAsync(query, db, _user);

        Assert.Single(result);
        Assert.Equal("Live", result[0].AccountType);
    }

    [Fact]
    public async Task Close_position_works_for_paper_account()
    {
        using var db = TestDbContextFactory.Create();
        var (account, position) = SeedPaperAccountWithOpenPosition(db);

        var command = new ClosePositionCommand(position.Id);
        var result = await ClosePositionHandler.HandleAsync(command, db, _user);

        Assert.Equal(position.Id, result.PositionId);
        Assert.Equal(PositionStatus.Closed, db.Positions.Single().Status);
    }

    private Account SeedPaperAccountWithOrder(TradingDbContext db)
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

        db.Orders.Add(new Order
        {
            AccountId = account.Id,
            Symbol = "AAPL",
            Side = OrderSide.Buy,
            Type = OrderType.Market,
            Quantity = 10,
            Price = 175m,
            Status = OrderStatus.Filled
        });
        db.SaveChanges();
        return account;
    }

    private Account SeedLiveAccountWithOrder(TradingDbContext db)
    {
        var account = new Account
        {
            UserId = _user.UserId,
            Name = "Live Account",
            Balance = 100_000m,
            Currency = "USD",
            AccountType = AccountType.Live
        };
        db.Accounts.Add(account);

        db.Orders.Add(new Order
        {
            AccountId = account.Id,
            Symbol = "AAPL",
            Side = OrderSide.Buy,
            Type = OrderType.Market,
            Quantity = 10,
            Price = 175m,
            Status = OrderStatus.Filled
        });
        db.SaveChanges();
        return account;
    }

    private Account SeedPaperAccountWithPosition(TradingDbContext db)
    {
        var account = new Account
        {
            UserId = _user.UserId,
            Name = "Paper Account",
            Balance = 98_000m,
            Currency = "USD",
            AccountType = AccountType.Paper
        };
        db.Accounts.Add(account);

        db.Positions.Add(new Position
        {
            AccountId = account.Id,
            Symbol = "AAPL",
            Quantity = 10,
            AverageEntryPrice = 175m,
            CurrentPrice = 180m,
            Status = PositionStatus.Open,
            OpenedAt = DateTime.UtcNow
        });
        db.SaveChanges();
        return account;
    }

    private Account SeedLiveAccountWithPosition(TradingDbContext db)
    {
        var account = new Account
        {
            UserId = _user.UserId,
            Name = "Live Account",
            Balance = 98_000m,
            Currency = "USD",
            AccountType = AccountType.Live
        };
        db.Accounts.Add(account);

        db.Positions.Add(new Position
        {
            AccountId = account.Id,
            Symbol = "AAPL",
            Quantity = 10,
            AverageEntryPrice = 175m,
            CurrentPrice = 180m,
            Status = PositionStatus.Open,
            OpenedAt = DateTime.UtcNow
        });
        db.SaveChanges();
        return account;
    }

    private (Account account, Position position) SeedPaperAccountWithOpenPosition(TradingDbContext db)
    {
        var account = new Account
        {
            UserId = _user.UserId,
            Name = "Paper Account",
            Balance = 98_000m,
            Currency = "USD",
            AccountType = AccountType.Paper
        };
        db.Accounts.Add(account);

        var position = new Position
        {
            AccountId = account.Id,
            Symbol = "AAPL",
            Quantity = 10,
            AverageEntryPrice = 175m,
            CurrentPrice = 180m,
            Status = PositionStatus.Open,
            OpenedAt = DateTime.UtcNow
        };
        db.Positions.Add(position);

        db.Portfolios.Add(new Portfolio
        {
            AccountId = account.Id,
            TotalValue = 100_000m,
            CashBalance = 98_000m,
            InvestedValue = 1_800m,
            TotalPnL = 50m
        });
        db.SaveChanges();
        return (account, position);
    }
}
