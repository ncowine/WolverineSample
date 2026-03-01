using Microsoft.EntityFrameworkCore;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Tests.Helpers;

public static class TestMarketDataDbContextFactory
{
    public static MarketDataDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<MarketDataDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        var context = new MarketDataDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
