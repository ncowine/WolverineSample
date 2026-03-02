using Microsoft.EntityFrameworkCore;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.Tests.Helpers;

namespace TradingAssistant.Tests.Intelligence;

public class IntelligenceDbContextTests
{
    [Fact]
    public async Task CanPersistAndRetrieveMarketRegime()
    {
        await using var db = TestIntelligenceDbContextFactory.Create();

        var regime = new MarketRegime
        {
            MarketCode = "US_SP500",
            CurrentRegime = RegimeType.Bull,
            RegimeStartDate = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            RegimeDuration = 45,
            SmaSlope50 = 0.0025m,
            SmaSlope200 = 0.0012m,
            VixLevel = 14.5m,
            BreadthScore = 72.5m,
            PctAbove200Sma = 0.68m,
            AdvanceDeclineRatio = 1.85m,
            ConfidenceScore = 0.92m
        };

        db.MarketRegimes.Add(regime);
        await db.SaveChangesAsync();

        var loaded = await db.MarketRegimes.FirstAsync(r => r.MarketCode == "US_SP500");

        Assert.Equal(RegimeType.Bull, loaded.CurrentRegime);
        Assert.Equal(0.0025m, loaded.SmaSlope50);
        Assert.Equal(14.5m, loaded.VixLevel);
        Assert.Equal(0.68m, loaded.PctAbove200Sma);
        Assert.Equal(0.92m, loaded.ConfidenceScore);
    }

    [Fact]
    public async Task CanPersistAndRetrieveRegimeTransition()
    {
        await using var db = TestIntelligenceDbContextFactory.Create();

        var transition = new RegimeTransition
        {
            MarketCode = "IN_NIFTY50",
            FromRegime = RegimeType.Bull,
            ToRegime = RegimeType.Sideways,
            TransitionDate = new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc),
            SmaSlope50 = -0.001m,
            SmaSlope200 = 0.0005m,
            VixLevel = 18.2m,
            BreadthScore = 48.0m,
            PctAbove200Sma = 0.42m
        };

        db.RegimeTransitions.Add(transition);
        await db.SaveChangesAsync();

        var loaded = await db.RegimeTransitions.FirstAsync(t => t.MarketCode == "IN_NIFTY50");

        Assert.Equal(RegimeType.Bull, loaded.FromRegime);
        Assert.Equal(RegimeType.Sideways, loaded.ToRegime);
        Assert.Equal(18.2m, loaded.VixLevel);
    }

    [Fact]
    public async Task CanPersistAndRetrieveMarketProfile()
    {
        await using var db = TestIntelligenceDbContextFactory.Create();

        var profile = new MarketProfile
        {
            MarketCode = "UK_FTSE100",
            Exchange = "LSE",
            Currency = "GBP",
            Timezone = "Europe/London",
            VixSymbol = "^VFTSE",
            DataSource = "yahoo",
            ConfigJson = """{"regimeThresholds":{"highVol":28}}""",
            DnaProfileJson = """{"dominantBehavior":"trending"}"""
        };

        db.MarketProfiles.Add(profile);
        await db.SaveChangesAsync();

        var loaded = await db.MarketProfiles.FirstAsync(p => p.MarketCode == "UK_FTSE100");

        Assert.Equal("LSE", loaded.Exchange);
        Assert.Equal("GBP", loaded.Currency);
        Assert.Contains("highVol", loaded.ConfigJson);
        Assert.Contains("trending", loaded.DnaProfileJson);
    }

    [Fact]
    public async Task MarketProfileMarketCodeIsUnique()
    {
        await using var db = TestIntelligenceDbContextFactory.Create();

        db.MarketProfiles.Add(new MarketProfile { MarketCode = "UK_FTSE100", Exchange = "LSE" });
        await db.SaveChangesAsync();

        db.MarketProfiles.Add(new MarketProfile { MarketCode = "UK_FTSE100", Exchange = "AIM" });

        // InMemory doesn't enforce unique constraints, so this test documents the intent
        // Real SQLite would throw. Verify the index exists via model inspection.
        var indexes = db.Model.FindEntityType(typeof(MarketProfile))!.GetIndexes();
        Assert.Contains(indexes, i => i.IsUnique && i.Properties.Any(p => p.Name == "MarketCode"));
    }

    [Fact]
    public async Task CanPersistAndRetrieveBreadthSnapshot()
    {
        await using var db = TestIntelligenceDbContextFactory.Create();

        var snapshot = new BreadthSnapshot
        {
            MarketCode = "US_SP500",
            SnapshotDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            AdvanceDeclineRatio = 1.75m,
            PctAbove200Sma = 0.65m,
            PctAbove50Sma = 0.72m,
            NewHighs = 45,
            NewLows = 12,
            TotalStocks = 500,
            Advancing = 320,
            Declining = 180
        };

        db.BreadthSnapshots.Add(snapshot);
        await db.SaveChangesAsync();

        var loaded = await db.BreadthSnapshots.FirstAsync();

        Assert.Equal("US_SP500", loaded.MarketCode);
        Assert.Equal(1.75m, loaded.AdvanceDeclineRatio);
        Assert.Equal(45, loaded.NewHighs);
        Assert.Equal(500, loaded.TotalStocks);
    }

    [Fact]
    public async Task CanPersistAndRetrieveCorrelationSnapshot()
    {
        await using var db = TestIntelligenceDbContextFactory.Create();

        var snapshot = new CorrelationSnapshot
        {
            SnapshotDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            LookbackDays = 60,
            MatrixJson = """{"US_SP500|IN_NIFTY50":0.45}"""
        };

        db.CorrelationSnapshots.Add(snapshot);
        await db.SaveChangesAsync();

        var loaded = await db.CorrelationSnapshots.FirstAsync();

        Assert.Equal(60, loaded.LookbackDays);
        Assert.Contains("0.45", loaded.MatrixJson);
    }

    [Fact]
    public async Task CanPersistAndRetrieveCostProfile()
    {
        await using var db = TestIntelligenceDbContextFactory.Create();

        var costProfile = new CostProfile
        {
            MarketCode = "IN_NIFTY50",
            Name = "India Equities",
            CommissionPerShare = 0m,
            CommissionPercent = 0.03m,
            ExchangeFeePercent = 0.00345m,
            TaxPercent = 0.025m,
            SpreadEstimatePercent = 0.05m
        };

        db.CostProfiles.Add(costProfile);
        await db.SaveChangesAsync();

        var loaded = await db.CostProfiles.FirstAsync();

        Assert.Equal("IN_NIFTY50", loaded.MarketCode);
        Assert.Equal(0.03m, loaded.CommissionPercent);
    }

    [Fact]
    public void CostProfileEstimateRoundTrip_UsEquities()
    {
        var usCost = new CostProfile
        {
            CommissionPerShare = 0.005m,
            SpreadEstimatePercent = 0.10m
        };

        // 100 shares of $150 stock
        var cost = usCost.EstimateRoundTrip(150m, 100);

        // Per-share: 0.005 * 100 * 2 = 1.00
        // Percent: 15000 * 0.10 * 2 / 100 = 30.00
        Assert.Equal(31.00m, cost);
    }

    [Fact]
    public void CostProfileEstimateRoundTrip_IndiaEquities()
    {
        var indiaCost = new CostProfile
        {
            CommissionPercent = 0.03m,
            ExchangeFeePercent = 0.00345m,
            TaxPercent = 0.025m,
            SpreadEstimatePercent = 0.05m
        };

        // 100 shares at ₹2500
        var cost = indiaCost.EstimateRoundTrip(2500m, 100);

        // Notional: 250,000
        // Total pct: 0.03 + 0.00345 + 0.025 + 0.05 = 0.10845%
        // Round trip: 250000 * 0.10845 * 2 / 100 = 542.25
        Assert.Equal(542.25m, cost);
    }

    [Fact]
    public async Task CanPersistAndRetrievePipelineRunLog()
    {
        await using var db = TestIntelligenceDbContextFactory.Create();

        var log = new PipelineRunLog
        {
            MarketCode = "US_SP500",
            RunDate = new DateTime(2026, 3, 1, 21, 0, 0, DateTimeKind.Utc),
            StepName = "DataIngestion",
            StepOrder = 1,
            Status = PipelineStepStatus.Completed,
            Duration = TimeSpan.FromSeconds(12.5),
            RetryCount = 0
        };

        db.PipelineRunLogs.Add(log);
        await db.SaveChangesAsync();

        var loaded = await db.PipelineRunLogs.FirstAsync();

        Assert.Equal("DataIngestion", loaded.StepName);
        Assert.Equal(PipelineStepStatus.Completed, loaded.Status);
        Assert.Equal(1, loaded.StepOrder);
    }

    [Fact]
    public async Task CanPersistFailedPipelineStep()
    {
        await using var db = TestIntelligenceDbContextFactory.Create();

        var log = new PipelineRunLog
        {
            MarketCode = "IN_NIFTY50",
            RunDate = DateTime.UtcNow,
            StepName = "RegimeClassification",
            StepOrder = 4,
            Status = PipelineStepStatus.Failed,
            Duration = TimeSpan.FromSeconds(0.5),
            ErrorMessage = "VIX data unavailable for IN_NIFTY50",
            RetryCount = 3
        };

        db.PipelineRunLogs.Add(log);
        await db.SaveChangesAsync();

        var loaded = await db.PipelineRunLogs.FirstAsync();

        Assert.Equal(PipelineStepStatus.Failed, loaded.Status);
        Assert.Equal(3, loaded.RetryCount);
        Assert.Contains("VIX data unavailable", loaded.ErrorMessage);
    }

    [Fact]
    public async Task AllEnumTypesArePersistable()
    {
        await using var db = TestIntelligenceDbContextFactory.Create();

        // Test all RegimeType values
        foreach (var regime in Enum.GetValues<RegimeType>())
        {
            db.MarketRegimes.Add(new MarketRegime
            {
                MarketCode = $"TEST_{regime}",
                CurrentRegime = regime
            });
        }

        await db.SaveChangesAsync();

        var regimes = await db.MarketRegimes.ToListAsync();
        Assert.Equal(4, regimes.Count);
        Assert.Contains(regimes, r => r.CurrentRegime == RegimeType.Bull);
        Assert.Contains(regimes, r => r.CurrentRegime == RegimeType.Bear);
        Assert.Contains(regimes, r => r.CurrentRegime == RegimeType.Sideways);
        Assert.Contains(regimes, r => r.CurrentRegime == RegimeType.HighVolatility);
    }

    [Fact]
    public void AllEntitiesInheritFromBaseEntity()
    {
        var regime = new MarketRegime();
        var transition = new RegimeTransition();
        var profile = new MarketProfile();
        var breadth = new BreadthSnapshot();
        var correlation = new CorrelationSnapshot();
        var cost = new CostProfile();
        var pipeline = new PipelineRunLog();

        // All should have non-empty Guid Ids from BaseEntity
        Assert.NotEqual(Guid.Empty, regime.Id);
        Assert.NotEqual(Guid.Empty, transition.Id);
        Assert.NotEqual(Guid.Empty, profile.Id);
        Assert.NotEqual(Guid.Empty, breadth.Id);
        Assert.NotEqual(Guid.Empty, correlation.Id);
        Assert.NotEqual(Guid.Empty, cost.Id);
        Assert.NotEqual(Guid.Empty, pipeline.Id);

        // All should have CreatedAt set
        Assert.True(regime.CreatedAt > DateTime.MinValue);
    }
}
