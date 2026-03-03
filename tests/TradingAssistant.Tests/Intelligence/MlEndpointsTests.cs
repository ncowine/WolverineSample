using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application.Handlers.Intelligence;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Infrastructure.Persistence;
using TradingAssistant.Tests.Helpers;

namespace TradingAssistant.Tests.Intelligence;

public class MlEndpointsTests
{
    // ── GetModelRegistryHandler Tests ──────────────────────────────

    [Fact]
    public async Task Registry_EmptyDb_ReturnsEmptyList()
    {
        using var db = TestIntelligenceDbContextFactory.Create();

        var result = await GetModelRegistryHandler.HandleAsync(
            new GetModelRegistryQuery(), db);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Registry_NoFilter_ReturnsAllModelsAcrossMarkets()
    {
        using var db = TestIntelligenceDbContextFactory.Create();
        SeedModels(db, "US_SP500", 2);
        SeedModels(db, "IN_NIFTY50", 1);
        await db.SaveChangesAsync();

        var result = await GetModelRegistryHandler.HandleAsync(
            new GetModelRegistryQuery(), db);

        Assert.Equal(3, result.Count);
        // Ordered by MarketCode then version desc
        Assert.Equal("IN_NIFTY50", result[0].MarketCode);
        Assert.Equal("US_SP500", result[1].MarketCode);
        Assert.Equal(2, result[1].ModelVersion);
        Assert.Equal("US_SP500", result[2].MarketCode);
        Assert.Equal(1, result[2].ModelVersion);
    }

    [Fact]
    public async Task Registry_WithMarketFilter_ReturnsOnlyThatMarket()
    {
        using var db = TestIntelligenceDbContextFactory.Create();
        SeedModels(db, "US_SP500", 3);
        SeedModels(db, "IN_NIFTY50", 2);
        await db.SaveChangesAsync();

        var result = await GetModelRegistryHandler.HandleAsync(
            new GetModelRegistryQuery("IN_NIFTY50"), db);

        Assert.Equal(2, result.Count);
        Assert.All(result, m => Assert.Equal("IN_NIFTY50", m.MarketCode));
    }

    [Fact]
    public async Task Registry_IncludesFeatureImportance()
    {
        using var db = TestIntelligenceDbContextFactory.Create();
        var features = new[]
        {
            new { Name = "rsi_14", Importance = 0.35 },
            new { Name = "macd_signal", Importance = 0.25 }
        };
        db.MlModels.Add(new MlModel
        {
            MarketCode = "US_SP500",
            ModelVersion = 1,
            FeatureVersion = 1,
            ModelPath = "data/models/US_SP500/v1.zip",
            TrainedAt = DateTime.UtcNow,
            Auc = 0.72,
            IsActive = true,
            FeatureImportanceJson = JsonSerializer.Serialize(features)
        });
        await db.SaveChangesAsync();

        var result = await GetModelRegistryHandler.HandleAsync(
            new GetModelRegistryQuery(), db);

        Assert.Single(result);
        Assert.NotNull(result[0].TopFeatures);
        Assert.Equal(2, result[0].TopFeatures!.Count);
        Assert.Equal("rsi_14", result[0].TopFeatures[0].Name);
    }

    [Fact]
    public async Task Registry_HandlesEmptyFeatureImportanceJson()
    {
        using var db = TestIntelligenceDbContextFactory.Create();
        db.MlModels.Add(new MlModel
        {
            MarketCode = "US_SP500",
            ModelVersion = 1,
            FeatureVersion = 1,
            ModelPath = "data/models/US_SP500/v1.zip",
            TrainedAt = DateTime.UtcNow,
            Auc = 0.65,
            IsActive = false,
            FeatureImportanceJson = ""
        });
        await db.SaveChangesAsync();

        var result = await GetModelRegistryHandler.HandleAsync(
            new GetModelRegistryQuery(), db);

        Assert.Single(result);
        Assert.Null(result[0].TopFeatures);
    }

    // ── GetMlModelsHandler Tests (per-market) ────────────────────

    [Fact]
    public async Task GetModels_ReturnsModelsForMarketOnly()
    {
        using var db = TestIntelligenceDbContextFactory.Create();
        SeedModels(db, "US_SP500", 2);
        SeedModels(db, "IN_NIFTY50", 1);
        await db.SaveChangesAsync();

        var result = await GetMlModelsHandler.HandleAsync(
            new GetMlModelsQuery("US_SP500"), db);

        Assert.Equal(2, result.Count);
        Assert.All(result, m => Assert.Equal("US_SP500", m.MarketCode));
        // Ordered by version desc
        Assert.Equal(2, result[0].ModelVersion);
        Assert.Equal(1, result[1].ModelVersion);
    }

    // ── GetActiveMlModelHandler Tests ────────────────────────────

    [Fact]
    public async Task GetActiveModel_ReturnsActiveModel()
    {
        using var db = TestIntelligenceDbContextFactory.Create();
        db.MlModels.Add(new MlModel
        {
            MarketCode = "US_SP500", ModelVersion = 1, FeatureVersion = 1,
            ModelPath = "v1.zip", TrainedAt = DateTime.UtcNow,
            Auc = 0.70, IsActive = false, FeatureImportanceJson = "[]"
        });
        db.MlModels.Add(new MlModel
        {
            MarketCode = "US_SP500", ModelVersion = 2, FeatureVersion = 1,
            ModelPath = "v2.zip", TrainedAt = DateTime.UtcNow,
            Auc = 0.75, IsActive = true, FeatureImportanceJson = "[]"
        });
        await db.SaveChangesAsync();

        var result = await GetActiveMlModelHandler.HandleAsync(
            new GetActiveMlModelQuery("US_SP500"), db);

        Assert.NotNull(result);
        Assert.Equal(2, result!.ModelVersion);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task GetActiveModel_ReturnsNull_WhenNoActiveModel()
    {
        using var db = TestIntelligenceDbContextFactory.Create();

        var result = await GetActiveMlModelHandler.HandleAsync(
            new GetActiveMlModelQuery("US_SP500"), db);

        Assert.Null(result);
    }

    // ── GetMlFeatureImportanceHandler Tests ─────────────────────

    [Fact]
    public async Task GetFeatureImportance_ReturnsFromActiveModel()
    {
        using var db = TestIntelligenceDbContextFactory.Create();
        var features = new[]
        {
            new { Name = "vol_20d", Importance = 0.40 },
            new { Name = "rsi_14", Importance = 0.30 }
        };
        db.MlModels.Add(new MlModel
        {
            MarketCode = "US_SP500", ModelVersion = 1, FeatureVersion = 1,
            ModelPath = "v1.zip", TrainedAt = DateTime.UtcNow,
            Auc = 0.75, IsActive = true,
            FeatureImportanceJson = JsonSerializer.Serialize(features)
        });
        await db.SaveChangesAsync();

        var result = await GetMlFeatureImportanceHandler.HandleAsync(
            new GetMlFeatureImportanceQuery("US_SP500"), db);

        Assert.Equal(2, result.Count);
        Assert.Equal("vol_20d", result[0].Name);
        Assert.Equal(0.40, result[0].Importance);
    }

    [Fact]
    public async Task GetFeatureImportance_ReturnsEmpty_WhenNoActiveModel()
    {
        using var db = TestIntelligenceDbContextFactory.Create();

        var result = await GetMlFeatureImportanceHandler.HandleAsync(
            new GetMlFeatureImportanceQuery("US_SP500"), db);

        Assert.Empty(result);
    }

    // ── Helper ───────────────────────────────────────────────────

    private static void SeedModels(IntelligenceDbContext db, string marketCode, int count)
    {
        for (var i = 1; i <= count; i++)
        {
            db.MlModels.Add(new MlModel
            {
                MarketCode = marketCode,
                ModelVersion = i,
                FeatureVersion = 1,
                ModelPath = $"data/models/{marketCode}/v{i}.zip",
                TrainedAt = DateTime.UtcNow.AddDays(-count + i),
                Auc = 0.60 + i * 0.05,
                IsActive = i == count,
                FeatureImportanceJson = "[]"
            });
        }
    }
}
