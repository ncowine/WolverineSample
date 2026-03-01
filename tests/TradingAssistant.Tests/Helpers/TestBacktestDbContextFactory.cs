using Microsoft.EntityFrameworkCore;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Tests.Helpers;

public static class TestBacktestDbContextFactory
{
    public static BacktestDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<BacktestDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        var context = new BacktestDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
