using TradingAssistant.Application.Handlers.Intelligence;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Infrastructure.Persistence;
using TradingAssistant.Tests.Helpers;

namespace TradingAssistant.Tests.Intelligence;

public class MarketProfileHandlerTests
{
    private IntelligenceDbContext CreateDb() => TestIntelligenceDbContextFactory.Create();

    // ── CreateMarketProfileHandler ──

    [Fact]
    public async Task Create_PersistsProfile()
    {
        await using var db = CreateDb();

        var cmd = new CreateMarketProfileCommand(
            "UK_FTSE100", "LSE", "GBP", "Europe/London", "^VFTSE", "yahoo",
            """{"regimeThresholds":{"highVol":28}}""");

        var dto = await CreateMarketProfileHandler.HandleAsync(cmd, db);

        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.Equal("UK_FTSE100", dto.MarketCode);
        Assert.Equal("LSE", dto.Exchange);
        Assert.Equal("GBP", dto.Currency);
        Assert.Equal("Europe/London", dto.Timezone);
        Assert.Equal("^VFTSE", dto.VixSymbol);
        Assert.Equal("yahoo", dto.DataSource);
        Assert.True(dto.IsActive);
        Assert.Contains("highVol", dto.ConfigJson);
    }

    [Fact]
    public async Task Create_NormalizesMarketCode()
    {
        await using var db = CreateDb();

        var cmd = new CreateMarketProfileCommand("  uk_ftse100  ", "LSE");

        var dto = await CreateMarketProfileHandler.HandleAsync(cmd, db);

        Assert.Equal("UK_FTSE100", dto.MarketCode);
    }

    [Fact]
    public async Task Create_TrimsExchangeAndVixSymbol()
    {
        await using var db = CreateDb();

        var cmd = new CreateMarketProfileCommand("JP_NIKKEI225", "  TSE  ", VixSymbol: "  ^VXJ  ");

        var dto = await CreateMarketProfileHandler.HandleAsync(cmd, db);

        Assert.Equal("TSE", dto.Exchange);
        Assert.Equal("^VXJ", dto.VixSymbol);
    }

    [Fact]
    public async Task Create_DuplicateMarketCode_Throws()
    {
        await using var db = CreateDb();

        var cmd = new CreateMarketProfileCommand("DE_DAX40", "XETRA");
        await CreateMarketProfileHandler.HandleAsync(cmd, db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateMarketProfileHandler.HandleAsync(cmd, db));
    }

    [Fact]
    public async Task Create_NormalizesDataSourceToLower()
    {
        await using var db = CreateDb();

        var cmd = new CreateMarketProfileCommand("HK_HSI", "HKEX", DataSource: "Yahoo");

        var dto = await CreateMarketProfileHandler.HandleAsync(cmd, db);

        Assert.Equal("yahoo", dto.DataSource);
    }

    [Fact]
    public async Task Create_NormalizesCurrencyToUpper()
    {
        await using var db = CreateDb();

        var cmd = new CreateMarketProfileCommand("AU_ASX200", "ASX", Currency: "aud");

        var dto = await CreateMarketProfileHandler.HandleAsync(cmd, db);

        Assert.Equal("AUD", dto.Currency);
    }

    // ── UpdateMarketProfileHandler ──

    [Fact]
    public async Task Update_PartialUpdate_OnlyChangesSpecifiedFields()
    {
        await using var db = CreateDb();

        db.MarketProfiles.Add(new MarketProfile
        {
            MarketCode = "UK_FTSE100", Exchange = "LSE", Currency = "GBP",
            Timezone = "Europe/London", VixSymbol = "^VFTSE", DataSource = "yahoo"
        });
        await db.SaveChangesAsync();
        var profile = db.MarketProfiles.First(p => p.MarketCode == "UK_FTSE100");

        var cmd = new UpdateMarketProfileCommand(profile.Id, Exchange: "LSE/AIM");

        var dto = await UpdateMarketProfileHandler.HandleAsync(cmd, db);

        Assert.Equal("LSE/AIM", dto.Exchange);
        Assert.Equal("GBP", dto.Currency); // unchanged
        Assert.Equal("Europe/London", dto.Timezone); // unchanged
    }

    [Fact]
    public async Task Update_SetsUpdatedAt()
    {
        await using var db = CreateDb();

        db.MarketProfiles.Add(new MarketProfile { MarketCode = "UK_FTSE100", Exchange = "LSE" });
        await db.SaveChangesAsync();
        var profile = db.MarketProfiles.First(p => p.MarketCode == "UK_FTSE100");

        var before = DateTime.UtcNow;
        var cmd = new UpdateMarketProfileCommand(profile.Id, Exchange: "XLON");
        await UpdateMarketProfileHandler.HandleAsync(cmd, db);

        var updated = await db.MarketProfiles.FindAsync(profile.Id);
        Assert.NotNull(updated!.UpdatedAt);
        Assert.True(updated.UpdatedAt >= before);
    }

    [Fact]
    public async Task Update_NotFound_Throws()
    {
        await using var db = CreateDb();

        var cmd = new UpdateMarketProfileCommand(Guid.NewGuid(), Exchange: "NYSE");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => UpdateMarketProfileHandler.HandleAsync(cmd, db));
    }

    [Fact]
    public async Task Update_Deactivate()
    {
        await using var db = CreateDb();

        db.MarketProfiles.Add(new MarketProfile { MarketCode = "UK_FTSE100", Exchange = "LSE" });
        await db.SaveChangesAsync();
        var profile = db.MarketProfiles.First(p => p.MarketCode == "UK_FTSE100");

        var cmd = new UpdateMarketProfileCommand(profile.Id, IsActive: false);

        var dto = await UpdateMarketProfileHandler.HandleAsync(cmd, db);

        Assert.False(dto.IsActive);
    }

    [Fact]
    public async Task Update_ConfigJson()
    {
        await using var db = CreateDb();

        db.MarketProfiles.Add(new MarketProfile { MarketCode = "UK_FTSE100", Exchange = "LSE" });
        await db.SaveChangesAsync();
        var profile = db.MarketProfiles.First(p => p.MarketCode == "UK_FTSE100");

        var newConfig = """{"regimeThresholds":{"highVol":35}}""";
        var cmd = new UpdateMarketProfileCommand(profile.Id, ConfigJson: newConfig);

        var dto = await UpdateMarketProfileHandler.HandleAsync(cmd, db);

        Assert.Contains("highVol", dto.ConfigJson);
        Assert.Contains("35", dto.ConfigJson);
    }

    // ── GetMarketProfilesHandler ──

    [Fact]
    public async Task GetAll_ReturnsOrderedByMarketCode()
    {
        await using var db = CreateDb();

        db.MarketProfiles.Add(new MarketProfile { MarketCode = "UK_FTSE100", Exchange = "LSE" });
        db.MarketProfiles.Add(new MarketProfile { MarketCode = "AU_ASX200", Exchange = "ASX" });
        await db.SaveChangesAsync();

        var result = await GetMarketProfilesHandler.HandleAsync(new GetMarketProfilesQuery(), db);

        // Includes seed data (IN_NIFTY50, US_SP500) + 2 new profiles
        Assert.Equal(4, result.Count);
        Assert.Equal("AU_ASX200", result[0].MarketCode);
        Assert.Equal("IN_NIFTY50", result[1].MarketCode);
        Assert.Equal("UK_FTSE100", result[2].MarketCode);
        Assert.Equal("US_SP500", result[3].MarketCode);
    }

    // ── GetMarketProfileHandler ──

    [Fact]
    public async Task GetByCode_ReturnsProfile()
    {
        await using var db = CreateDb();

        db.MarketProfiles.Add(new MarketProfile
        {
            MarketCode = "UK_FTSE100", Exchange = "LSE", Currency = "GBP",
            VixSymbol = "^VFTSE", DnaProfileJson = """{"behavior":"mean_reverting"}"""
        });
        await db.SaveChangesAsync();

        var dto = await GetMarketProfileHandler.HandleAsync(
            new GetMarketProfileQuery("UK_FTSE100"), db);

        Assert.Equal("UK_FTSE100", dto.MarketCode);
        Assert.Equal("LSE", dto.Exchange);
        Assert.Contains("mean_reverting", dto.DnaProfileJson);
    }

    [Fact]
    public async Task GetByCode_SeededProfile_ReturnsUsProfile()
    {
        await using var db = CreateDb();

        var dto = await GetMarketProfileHandler.HandleAsync(
            new GetMarketProfileQuery("US_SP500"), db);

        Assert.Equal("US_SP500", dto.MarketCode);
        Assert.Equal("NYSE/NASDAQ", dto.Exchange);
        Assert.Equal("USD", dto.Currency);
    }

    [Fact]
    public async Task GetByCode_NotFound_Throws()
    {
        await using var db = CreateDb();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => GetMarketProfileHandler.HandleAsync(
                new GetMarketProfileQuery("NONEXISTENT"), db));
    }
}

public class CostProfileHandlerTests
{
    private IntelligenceDbContext CreateDb() => TestIntelligenceDbContextFactory.Create();

    // ── CreateCostProfileHandler ──

    [Fact]
    public async Task Create_PersistsProfile()
    {
        await using var db = CreateDb();

        var cmd = new CreateCostProfileCommand("UK_FTSE100", "UK Equities",
            CommissionPerShare: 0.005m, SpreadEstimatePercent: 0.10m);

        var dto = await CreateCostProfileHandler.HandleAsync(cmd, db);

        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.Equal("UK_FTSE100", dto.MarketCode);
        Assert.Equal("UK Equities", dto.Name);
        Assert.Equal(0.005m, dto.CommissionPerShare);
        Assert.Equal(0.10m, dto.SpreadEstimatePercent);
        Assert.True(dto.IsActive);
    }

    [Fact]
    public async Task Create_NormalizesMarketCode()
    {
        await using var db = CreateDb();

        var cmd = new CreateCostProfileCommand("  jp_nikkei225  ", "Japan Equities");

        var dto = await CreateCostProfileHandler.HandleAsync(cmd, db);

        Assert.Equal("JP_NIKKEI225", dto.MarketCode);
    }

    [Fact]
    public async Task Create_TrimsName()
    {
        await using var db = CreateDb();

        var cmd = new CreateCostProfileCommand("AU_ASX200", "  AU Equities  ");

        var dto = await CreateCostProfileHandler.HandleAsync(cmd, db);

        Assert.Equal("AU Equities", dto.Name);
    }

    [Fact]
    public async Task Create_DefaultZeroFees()
    {
        await using var db = CreateDb();

        var cmd = new CreateCostProfileCommand("DE_DAX40", "Zero Cost");

        var dto = await CreateCostProfileHandler.HandleAsync(cmd, db);

        Assert.Equal(0m, dto.CommissionPerShare);
        Assert.Equal(0m, dto.CommissionPercent);
        Assert.Equal(0m, dto.ExchangeFeePercent);
        Assert.Equal(0m, dto.TaxPercent);
        Assert.Equal(0m, dto.SpreadEstimatePercent);
    }

    // ── UpdateCostProfileHandler ──

    [Fact]
    public async Task Update_PartialUpdate()
    {
        await using var db = CreateDb();

        db.CostProfiles.Add(new CostProfile
        {
            MarketCode = "UK_FTSE100", Name = "UK Equities",
            CommissionPerShare = 0.005m, SpreadEstimatePercent = 0.10m
        });
        await db.SaveChangesAsync();
        var profile = db.CostProfiles.First();

        var cmd = new UpdateCostProfileCommand(profile.Id, CommissionPerShare: 0.01m);

        var dto = await UpdateCostProfileHandler.HandleAsync(cmd, db);

        Assert.Equal(0.01m, dto.CommissionPerShare);
        Assert.Equal(0.10m, dto.SpreadEstimatePercent); // unchanged
        Assert.Equal("UK Equities", dto.Name); // unchanged
    }

    [Fact]
    public async Task Update_NotFound_Throws()
    {
        await using var db = CreateDb();

        var cmd = new UpdateCostProfileCommand(Guid.NewGuid(), Name: "Test");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => UpdateCostProfileHandler.HandleAsync(cmd, db));
    }

    [Fact]
    public async Task Update_Deactivate()
    {
        await using var db = CreateDb();

        db.CostProfiles.Add(new CostProfile { MarketCode = "UK_FTSE100", Name = "UK Equities" });
        await db.SaveChangesAsync();
        var profile = db.CostProfiles.First();

        var cmd = new UpdateCostProfileCommand(profile.Id, IsActive: false);

        var dto = await UpdateCostProfileHandler.HandleAsync(cmd, db);

        Assert.False(dto.IsActive);
    }

    // ── GetCostProfilesHandler ──

    [Fact]
    public async Task GetAll_ReturnsOrderedByMarketCodeThenName()
    {
        await using var db = CreateDb();

        db.CostProfiles.Add(new CostProfile { MarketCode = "UK_FTSE100", Name = "Plan B" });
        db.CostProfiles.Add(new CostProfile { MarketCode = "AU_ASX200", Name = "Australia" });
        db.CostProfiles.Add(new CostProfile { MarketCode = "UK_FTSE100", Name = "Plan A" });
        await db.SaveChangesAsync();

        var result = await GetCostProfilesHandler.HandleAsync(new GetCostProfilesQuery(), db);

        Assert.Equal(3, result.Count);
        Assert.Equal("AU_ASX200", result[0].MarketCode);
        Assert.Equal("UK_FTSE100", result[1].MarketCode);
        Assert.Equal("Plan A", result[1].Name);
        Assert.Equal("Plan B", result[2].Name);
    }

    [Fact]
    public async Task GetByMarketCode_FiltersCorrectly()
    {
        await using var db = CreateDb();

        db.CostProfiles.Add(new CostProfile { MarketCode = "UK_FTSE100", Name = "UK" });
        db.CostProfiles.Add(new CostProfile { MarketCode = "AU_ASX200", Name = "Australia" });
        await db.SaveChangesAsync();

        var result = await GetCostProfilesHandler.HandleAsync(
            new GetCostProfilesQuery("UK_FTSE100"), db);

        Assert.Single(result);
        Assert.Equal("UK_FTSE100", result[0].MarketCode);
    }

    [Fact]
    public async Task GetAll_NoCostProfiles_ReturnsEmptyList()
    {
        await using var db = CreateDb();

        var result = await GetCostProfilesHandler.HandleAsync(new GetCostProfilesQuery(), db);

        Assert.Empty(result);
    }
}
