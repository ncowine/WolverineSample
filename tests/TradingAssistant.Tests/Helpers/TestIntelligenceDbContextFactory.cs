using Microsoft.EntityFrameworkCore;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Tests.Helpers;

public static class TestIntelligenceDbContextFactory
{
    public static IntelligenceDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<IntelligenceDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        var context = new IntelligenceDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
