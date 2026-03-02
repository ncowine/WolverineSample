using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using TradingAssistant.Application.Handlers.Intelligence;
using TradingAssistant.Application.Intelligence;
using TradingAssistant.Application.Intelligence.Prompts;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Domain.Backtesting;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Tests.Helpers;

namespace TradingAssistant.Tests.Intelligence;

#region PlaybookGenerator Tests

public class PlaybookGeneratorTests
{
    [Fact]
    public void GenerateAll_ReturnsThreeTemplates()
    {
        var templates = PlaybookGenerator.GenerateAll("US");

        Assert.Equal(3, templates.Count);
        Assert.Contains(templates, t => t.Type == PlaybookGenerator.Momentum);
        Assert.Contains(templates, t => t.Type == PlaybookGenerator.MeanReversion);
        Assert.Contains(templates, t => t.Type == PlaybookGenerator.Breakout);
    }

    [Theory]
    [InlineData("US")]
    [InlineData("IN")]
    [InlineData("IN_NIFTY50")]
    public void GenerateAll_AllTemplatesHaveValidDefinitions(string marketCode)
    {
        var templates = PlaybookGenerator.GenerateAll(marketCode);

        foreach (var (type, regimes, def) in templates)
        {
            Assert.NotNull(def);
            Assert.NotEmpty(def.EntryConditions);
            Assert.NotEmpty(def.ExitConditions);
            Assert.NotNull(def.StopLoss);
            Assert.NotNull(def.TakeProfit);
            Assert.NotNull(def.PositionSizing);
            Assert.NotEmpty(regimes);
        }
    }

    [Fact]
    public void Momentum_TaggedWithBullRegime()
    {
        var templates = PlaybookGenerator.GenerateAll("US");
        var momentum = templates.Single(t => t.Type == PlaybookGenerator.Momentum);

        Assert.Equal("Bull", momentum.Regimes);
    }

    [Fact]
    public void MeanReversion_TaggedWithSidewaysRegime()
    {
        var templates = PlaybookGenerator.GenerateAll("US");
        var mr = templates.Single(t => t.Type == PlaybookGenerator.MeanReversion);

        Assert.Equal("Sideways", mr.Regimes);
    }

    [Fact]
    public void Breakout_TaggedWithBullAndHighVolatility()
    {
        var templates = PlaybookGenerator.GenerateAll("US");
        var breakout = templates.Single(t => t.Type == PlaybookGenerator.Breakout);

        Assert.Equal("Bull,HighVolatility", breakout.Regimes);
    }

    [Fact]
    public void IndiaMarket_HasWiderStops()
    {
        var usMom = PlaybookGenerator.BuildMomentum("US");
        var inMom = PlaybookGenerator.BuildMomentum("IN");

        Assert.True(inMom.StopLoss.Multiplier > usMom.StopLoss.Multiplier,
            "India momentum should have wider ATR stops than US");
    }

    [Fact]
    public void IndiaMarket_HasHigherVolumeThresholds()
    {
        var usMom = PlaybookGenerator.BuildMomentum("US");
        var inMom = PlaybookGenerator.BuildMomentum("IN");

        Assert.True(inMom.Filters.MinVolume > usMom.Filters.MinVolume,
            "India should have higher minimum volume threshold");
    }

    [Fact]
    public void IndiaMarket_HasLowerRiskPercent()
    {
        var usMom = PlaybookGenerator.BuildMomentum("US");
        var inMom = PlaybookGenerator.BuildMomentum("IN");

        Assert.True(inMom.PositionSizing.RiskPercent < usMom.PositionSizing.RiskPercent,
            "India should have lower risk per trade");
    }

    [Fact]
    public void IndiaMarket_HigherMinPrice()
    {
        var usBk = PlaybookGenerator.BuildBreakout("US");
        var inBk = PlaybookGenerator.BuildBreakout("IN");

        Assert.True(inBk.Filters.MinPrice > usBk.Filters.MinPrice,
            "India should have higher minimum price filter");
    }

    [Fact]
    public void IN_NIFTY50_TreatedAsIndiaMarket()
    {
        var def = PlaybookGenerator.BuildMomentum("IN_NIFTY50");

        // India-specific: 3x ATR stops
        Assert.Equal(3m, def.StopLoss.Multiplier);
    }

    [Fact]
    public void AllTemplates_SerializeAsValidJson()
    {
        var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        foreach (var market in new[] { "US", "IN" })
        {
            var templates = PlaybookGenerator.GenerateAll(market);
            foreach (var (_, _, def) in templates)
            {
                var json = JsonSerializer.Serialize(def, opts);
                Assert.NotEmpty(json);
                Assert.StartsWith("{", json);
            }
        }
    }

    [Fact]
    public void Momentum_HasMultiTimeframeEntry()
    {
        var def = PlaybookGenerator.BuildMomentum("US");

        var timeframes = def.EntryConditions.Select(g => g.Timeframe).Distinct().ToList();
        Assert.True(timeframes.Count >= 2, "Momentum should use at least 2 timeframes");
        Assert.Contains("Daily", timeframes);
        Assert.Contains("Weekly", timeframes);
    }
}

#endregion

#region PlaybookPrompt Tests

public class PlaybookPromptTests
{
    [Fact]
    public void BuildSystemPrompt_ContainsStrategyDesign()
    {
        var prompt = PlaybookPrompt.BuildSystemPrompt();
        Assert.Contains("quantitative trading strategist", prompt);
        Assert.Contains("JSON", prompt);
    }

    [Fact]
    public void BuildUserPrompt_ContainsMarketAndType()
    {
        var input = new PlaybookInput("US", "Momentum", "S&P 500 large-cap US equities");
        var prompt = PlaybookPrompt.BuildUserPrompt(input);

        Assert.Contains("US", prompt);
        Assert.Contains("Momentum", prompt);
        Assert.Contains("S&P 500", prompt);
    }

    [Fact]
    public void ParseResponse_ValidJson_ReturnsDefinition()
    {
        var json = """
        {
            "entryConditions": [{"timeframe": "Daily", "conditions": [{"indicator": "RSI", "comparison": "LessThan", "value": 30, "period": 14}]}],
            "exitConditions": [{"timeframe": "Daily", "conditions": [{"indicator": "RSI", "comparison": "GreaterThan", "value": 70, "period": 14}]}],
            "stopLoss": {"type": "Atr", "multiplier": 2},
            "takeProfit": {"type": "RMultiple", "multiplier": 2},
            "positionSizing": {"sizingMethod": "Fixed", "riskPercent": 1, "maxPositions": 6},
            "filters": {"minVolume": 200000}
        }
        """;

        var result = PlaybookPrompt.ParseResponse(json);
        Assert.NotNull(result);
        Assert.Single(result!.EntryConditions);
    }

    [Fact]
    public void ParseResponse_InvalidJson_ReturnsNull()
    {
        var result = PlaybookPrompt.ParseResponse("not valid json at all");
        Assert.Null(result);
    }
}

#endregion

#region GeneratePlaybooksHandler Tests

public class GeneratePlaybooksHandlerTests
{
    private static readonly NullLogger<GeneratePlaybooksHandler> Logger = new();

    [Fact]
    public async Task HandleAsync_CreatesThreeTemplates()
    {
        using var backtestDb = TestBacktestDbContextFactory.Create();
        using var intelligenceDb = TestIntelligenceDbContextFactory.Create();
        var claude = new FakeClaudeClient(maxDailyCalls: 0); // Rate-limited → uses hardcoded

        var result = await GeneratePlaybooksHandler.HandleAsync(
            new GeneratePlaybooksCommand("US"), claude, backtestDb, intelligenceDb, Logger);

        Assert.Equal("US", result.MarketCode);
        Assert.Equal(3, result.TemplatesCreated);
        Assert.Equal(3, result.Templates.Count);

        // Verify saved in DB
        Assert.Equal(3, backtestDb.Strategies.Count(s => s.IsTemplate));
    }

    [Fact]
    public async Task HandleAsync_TemplatesHaveCorrectFields()
    {
        using var backtestDb = TestBacktestDbContextFactory.Create();
        using var intelligenceDb = TestIntelligenceDbContextFactory.Create();
        var claude = new FakeClaudeClient(maxDailyCalls: 0);

        var result = await GeneratePlaybooksHandler.HandleAsync(
            new GeneratePlaybooksCommand("IN"), claude, backtestDb, intelligenceDb, Logger);

        foreach (var template in result.Templates)
        {
            Assert.True(template.IsTemplate);
            Assert.Equal("IN", template.TemplateMarketCode);
            Assert.NotNull(template.TemplateType);
            Assert.NotNull(template.TemplateRegimes);
            Assert.True(template.UsesV2Engine);
            Assert.Contains("[Template]", template.Name);
        }
    }

    [Fact]
    public async Task HandleAsync_NoDuplicates_SkipsExisting()
    {
        using var backtestDb = TestBacktestDbContextFactory.Create();
        using var intelligenceDb = TestIntelligenceDbContextFactory.Create();
        var claude = new FakeClaudeClient(maxDailyCalls: 0);

        // Generate first time
        await GeneratePlaybooksHandler.HandleAsync(
            new GeneratePlaybooksCommand("US"), claude, backtestDb, intelligenceDb, Logger);

        // Generate again — should skip all
        var result = await GeneratePlaybooksHandler.HandleAsync(
            new GeneratePlaybooksCommand("US"), claude, backtestDb, intelligenceDb, Logger);

        Assert.Equal(0, result.TemplatesCreated);
        Assert.Equal(3, backtestDb.Strategies.Count(s => s.IsTemplate)); // Still 3, not 6
    }

    [Fact]
    public async Task HandleAsync_GeneratesOnlyMissingTypes()
    {
        using var backtestDb = TestBacktestDbContextFactory.Create();
        using var intelligenceDb = TestIntelligenceDbContextFactory.Create();
        var claude = new FakeClaudeClient(maxDailyCalls: 0);

        // Seed one existing template
        backtestDb.Strategies.Add(new Strategy
        {
            Name = "Existing",
            IsTemplate = true,
            TemplateMarketCode = "US",
            TemplateType = PlaybookGenerator.Momentum,
            TemplateRegimes = "Bull",
            RulesJson = "{}"
        });
        await backtestDb.SaveChangesAsync();

        var result = await GeneratePlaybooksHandler.HandleAsync(
            new GeneratePlaybooksCommand("US"), claude, backtestDb, intelligenceDb, Logger);

        Assert.Equal(2, result.TemplatesCreated); // Only MeanReversion and Breakout
        Assert.Equal(3, backtestDb.Strategies.Count(s => s.IsTemplate));
    }

    [Fact]
    public async Task HandleAsync_ClaudeFailure_FallsBackToHardcoded()
    {
        using var backtestDb = TestBacktestDbContextFactory.Create();
        using var intelligenceDb = TestIntelligenceDbContextFactory.Create();
        // Claude returns failure responses
        var claude = new FakeClaudeClient();
        claude.SetDefaultResponse("invalid not json {{{{");

        var result = await GeneratePlaybooksHandler.HandleAsync(
            new GeneratePlaybooksCommand("US"), claude, backtestDb, intelligenceDb, Logger);

        // Should still create 3 templates via hardcoded fallback
        Assert.Equal(3, result.TemplatesCreated);
    }

    [Fact]
    public async Task HandleAsync_UsesMarketProfileForClaudePrompt()
    {
        using var backtestDb = TestBacktestDbContextFactory.Create();
        using var intelligenceDb = TestIntelligenceDbContextFactory.Create();

        // Add market profile
        intelligenceDb.MarketProfiles.Add(new MarketProfile
        {
            MarketCode = "US",
            Exchange = "NYSE",
            DnaProfileJson = "Large-cap US equities with high liquidity"
        });
        await intelligenceDb.SaveChangesAsync();

        var claude = new FakeClaudeClient();
        // Claude will use the market description in its prompt
        claude.SetDefaultResponse("{}"); // Will fail parse, fall back to hardcoded

        var result = await GeneratePlaybooksHandler.HandleAsync(
            new GeneratePlaybooksCommand("US"), claude, backtestDb, intelligenceDb, Logger);

        Assert.Equal(3, result.TemplatesCreated);
        Assert.Equal(3, claude.CallCount); // Claude was called 3 times (one per type)
    }

    [Fact]
    public async Task HandleAsync_MarketCodeNormalized()
    {
        using var backtestDb = TestBacktestDbContextFactory.Create();
        using var intelligenceDb = TestIntelligenceDbContextFactory.Create();
        var claude = new FakeClaudeClient(maxDailyCalls: 0);

        var result = await GeneratePlaybooksHandler.HandleAsync(
            new GeneratePlaybooksCommand("  us  "), claude, backtestDb, intelligenceDb, Logger);

        Assert.Equal("US", result.MarketCode);
        Assert.All(result.Templates, t => Assert.Equal("US", t.TemplateMarketCode));
    }
}

#endregion

#region GetTemplatesHandler Tests

public class GetTemplatesHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsTemplatesForMarket()
    {
        using var db = TestBacktestDbContextFactory.Create();

        // Add US and IN templates
        db.Strategies.AddRange(
            new Strategy { Name = "US-Mom", IsTemplate = true, TemplateMarketCode = "US", TemplateType = "Momentum", RulesJson = "{}" },
            new Strategy { Name = "US-MR", IsTemplate = true, TemplateMarketCode = "US", TemplateType = "MeanReversion", RulesJson = "{}" },
            new Strategy { Name = "IN-Mom", IsTemplate = true, TemplateMarketCode = "IN", TemplateType = "Momentum", RulesJson = "{}" },
            new Strategy { Name = "Regular", IsTemplate = false, TemplateMarketCode = null, RulesJson = "{}" }
        );
        await db.SaveChangesAsync();

        var result = await GetTemplatesHandler.HandleAsync(new GetTemplatesQuery("US"), db);

        Assert.Equal(2, result.Count);
        Assert.All(result, t => Assert.Equal("US", t.TemplateMarketCode));
    }

    [Fact]
    public async Task HandleAsync_EmptyForUnknownMarket()
    {
        using var db = TestBacktestDbContextFactory.Create();

        var result = await GetTemplatesHandler.HandleAsync(new GetTemplatesQuery("JP"), db);

        Assert.Empty(result);
    }

    [Fact]
    public async Task HandleAsync_ExcludesNonTemplateStrategies()
    {
        using var db = TestBacktestDbContextFactory.Create();

        db.Strategies.Add(new Strategy { Name = "User Strategy", IsTemplate = false, RulesJson = "{}" });
        await db.SaveChangesAsync();

        var result = await GetTemplatesHandler.HandleAsync(new GetTemplatesQuery("US"), db);

        Assert.Empty(result);
    }
}

#endregion
