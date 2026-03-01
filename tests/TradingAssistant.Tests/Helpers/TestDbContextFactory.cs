using Microsoft.EntityFrameworkCore;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Tests.Helpers;

public static class TestDbContextFactory
{
    public static TradingDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<TradingDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        var context = new TradingDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
