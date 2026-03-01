using TradingAssistant.Application.Handlers.Trading;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Domain.Trading;
using TradingAssistant.Infrastructure.Persistence;
using TradingAssistant.Tests.Helpers;

namespace TradingAssistant.Tests.Handlers.Trading;

public class DcaPlanManagementTests
{
    private readonly FakeCurrentUser _user = new();

    // ── Pause ──

    [Fact]
    public async Task Pause_sets_plan_inactive()
    {
        using var db = TestDbContextFactory.Create();
        var plan = SeedActivePlan(db);

        var result = await PauseDcaPlanHandler.HandleAsync(
            new PauseDcaPlanCommand(plan.Id), db, _user);

        Assert.False(result.IsActive);
        Assert.False(db.DcaPlans.Single().IsActive);
    }

    [Fact]
    public async Task Pause_throws_when_already_paused()
    {
        using var db = TestDbContextFactory.Create();
        var plan = SeedActivePlan(db);
        plan.IsActive = false;
        db.SaveChanges();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => PauseDcaPlanHandler.HandleAsync(
                new PauseDcaPlanCommand(plan.Id), db, _user));
    }

    [Fact]
    public async Task Pause_throws_when_plan_not_found()
    {
        using var db = TestDbContextFactory.Create();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => PauseDcaPlanHandler.HandleAsync(
                new PauseDcaPlanCommand(Guid.NewGuid()), db, _user));
    }

    [Fact]
    public async Task Pause_throws_forbidden_for_wrong_user()
    {
        using var db = TestDbContextFactory.Create();
        var plan = SeedActivePlan(db);
        var otherUser = new FakeCurrentUser { UserId = Guid.NewGuid() };

        await Assert.ThrowsAsync<Application.Exceptions.ForbiddenAccessException>(
            () => PauseDcaPlanHandler.HandleAsync(
                new PauseDcaPlanCommand(plan.Id), db, otherUser));
    }

    // ── Resume ──

    [Fact]
    public async Task Resume_sets_plan_active_and_recalculates_next_execution()
    {
        using var db = TestDbContextFactory.Create();
        var plan = SeedActivePlan(db);
        plan.IsActive = false;
        plan.NextExecutionDate = DateTime.UtcNow.AddDays(-10); // old date
        db.SaveChanges();

        var result = await ResumeDcaPlanHandler.HandleAsync(
            new ResumeDcaPlanCommand(plan.Id), db, _user);

        Assert.True(result.IsActive);
        Assert.True(result.NextExecutionDate > DateTime.UtcNow.Date);
    }

    [Fact]
    public async Task Resume_throws_when_already_active()
    {
        using var db = TestDbContextFactory.Create();
        var plan = SeedActivePlan(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ResumeDcaPlanHandler.HandleAsync(
                new ResumeDcaPlanCommand(plan.Id), db, _user));
    }

    [Fact]
    public async Task Resume_throws_forbidden_for_wrong_user()
    {
        using var db = TestDbContextFactory.Create();
        var plan = SeedActivePlan(db);
        plan.IsActive = false;
        db.SaveChanges();
        var otherUser = new FakeCurrentUser { UserId = Guid.NewGuid() };

        await Assert.ThrowsAsync<Application.Exceptions.ForbiddenAccessException>(
            () => ResumeDcaPlanHandler.HandleAsync(
                new ResumeDcaPlanCommand(plan.Id), db, otherUser));
    }

    // ── Cancel ──

    [Fact]
    public async Task Cancel_removes_plan_from_database()
    {
        using var db = TestDbContextFactory.Create();
        var plan = SeedActivePlan(db);

        var result = await CancelDcaPlanHandler.HandleAsync(
            new CancelDcaPlanCommand(plan.Id), db, _user);

        Assert.Contains("cancelled", result, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(db.DcaPlans);
    }

    [Fact]
    public async Task Cancel_throws_when_plan_not_found()
    {
        using var db = TestDbContextFactory.Create();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CancelDcaPlanHandler.HandleAsync(
                new CancelDcaPlanCommand(Guid.NewGuid()), db, _user));
    }

    [Fact]
    public async Task Cancel_throws_forbidden_for_wrong_user()
    {
        using var db = TestDbContextFactory.Create();
        var plan = SeedActivePlan(db);
        var otherUser = new FakeCurrentUser { UserId = Guid.NewGuid() };

        await Assert.ThrowsAsync<Application.Exceptions.ForbiddenAccessException>(
            () => CancelDcaPlanHandler.HandleAsync(
                new CancelDcaPlanCommand(plan.Id), db, otherUser));
    }

    // ── Get Executions ──

    [Fact]
    public async Task GetExecutions_returns_paged_results()
    {
        using var db = TestDbContextFactory.Create();
        var plan = SeedActivePlan(db);

        // Seed 3 executions
        for (var i = 0; i < 3; i++)
        {
            db.DcaExecutions.Add(new DcaExecution
            {
                DcaPlanId = plan.Id,
                Amount = 500m,
                Status = DcaExecutionStatus.Success,
                ExecutedPrice = 150m,
                Quantity = 3m,
                OrderId = Guid.NewGuid()
            });
        }
        db.SaveChanges();

        var result = await GetDcaExecutionsHandler.HandleAsync(
            new GetDcaExecutionsQuery(plan.Id, Page: 1, PageSize: 2), db, _user);

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(1, result.Page);
    }

    [Fact]
    public async Task GetExecutions_returns_empty_when_no_executions()
    {
        using var db = TestDbContextFactory.Create();
        var plan = SeedActivePlan(db);

        var result = await GetDcaExecutionsHandler.HandleAsync(
            new GetDcaExecutionsQuery(plan.Id), db, _user);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task GetExecutions_throws_forbidden_for_wrong_user()
    {
        using var db = TestDbContextFactory.Create();
        var plan = SeedActivePlan(db);
        var otherUser = new FakeCurrentUser { UserId = Guid.NewGuid() };

        await Assert.ThrowsAsync<Application.Exceptions.ForbiddenAccessException>(
            () => GetDcaExecutionsHandler.HandleAsync(
                new GetDcaExecutionsQuery(plan.Id), db, otherUser));
    }

    [Fact]
    public async Task GetExecutions_maps_dto_fields_correctly()
    {
        using var db = TestDbContextFactory.Create();
        var plan = SeedActivePlan(db);

        var orderId = Guid.NewGuid();
        db.DcaExecutions.Add(new DcaExecution
        {
            DcaPlanId = plan.Id,
            OrderId = orderId,
            Amount = 500m,
            ExecutedPrice = 150m,
            Quantity = 3m,
            Status = DcaExecutionStatus.Success
        });
        db.SaveChanges();

        var result = await GetDcaExecutionsHandler.HandleAsync(
            new GetDcaExecutionsQuery(plan.Id), db, _user);

        var dto = result.Items.Single();
        Assert.Equal(plan.Id, dto.DcaPlanId);
        Assert.Equal(orderId, dto.OrderId);
        Assert.Equal(500m, dto.Amount);
        Assert.Equal(150m, dto.ExecutedPrice);
        Assert.Equal(3m, dto.Quantity);
        Assert.Equal("Success", dto.Status);
        Assert.Null(dto.ErrorReason);
    }

    // ── Helpers ──

    private DcaPlan SeedActivePlan(TradingDbContext db)
    {
        var account = new Account
        {
            UserId = _user.UserId,
            Name = "Test Account",
            Balance = 100_000m,
            Currency = "USD",
            AccountType = AccountType.Live
        };
        db.Accounts.Add(account);

        var plan = new DcaPlan
        {
            AccountId = account.Id,
            Symbol = "AAPL",
            Amount = 500m,
            Frequency = DcaFrequency.Weekly,
            NextExecutionDate = DateTime.UtcNow.AddDays(7),
            IsActive = true
        };
        db.DcaPlans.Add(plan);
        db.SaveChanges();

        return plan;
    }
}
