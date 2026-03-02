using Microsoft.EntityFrameworkCore;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Tests.Intelligence;

public class MarketProfileSeedTests
{
    private IntelligenceDbContext CreateSeededDb()
    {
        var options = new DbContextOptionsBuilder<IntelligenceDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        var context = new IntelligenceDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task SeedData_ContainsUsSp500()
    {
        await using var db = CreateSeededDb();

        var profile = await db.MarketProfiles.FirstOrDefaultAsync(p => p.MarketCode == "US_SP500");

        Assert.NotNull(profile);
        Assert.Equal("NYSE/NASDAQ", profile.Exchange);
        Assert.Equal("USD", profile.Currency);
        Assert.Equal("America/New_York", profile.Timezone);
        Assert.Equal("^VIX", profile.VixSymbol);
        Assert.Equal("yahoo", profile.DataSource);
        Assert.True(profile.IsActive);
        Assert.Contains("highVol", profile.ConfigJson);
        Assert.Contains("bullBreadth", profile.ConfigJson);
    }

    [Fact]
    public async Task SeedData_ContainsInNifty50()
    {
        await using var db = CreateSeededDb();

        var profile = await db.MarketProfiles.FirstOrDefaultAsync(p => p.MarketCode == "IN_NIFTY50");

        Assert.NotNull(profile);
        Assert.Equal("NSE", profile.Exchange);
        Assert.Equal("INR", profile.Currency);
        Assert.Equal("Asia/Kolkata", profile.Timezone);
        Assert.Equal("^INDIAVIX", profile.VixSymbol);
        Assert.Contains("highVol", profile.ConfigJson);
    }

    [Fact]
    public async Task SeedData_HasExactlyTwoProfiles()
    {
        await using var db = CreateSeededDb();

        var count = await db.MarketProfiles.CountAsync();

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task SeedData_IndiaHasLowerVolThreshold()
    {
        await using var db = CreateSeededDb();

        var us = await db.MarketProfiles.FirstAsync(p => p.MarketCode == "US_SP500");
        var india = await db.MarketProfiles.FirstAsync(p => p.MarketCode == "IN_NIFTY50");

        // US highVol = 30, India highVol = 25
        Assert.Contains("\"highVol\":30", us.ConfigJson);
        Assert.Contains("\"highVol\":25", india.ConfigJson);
    }
}
