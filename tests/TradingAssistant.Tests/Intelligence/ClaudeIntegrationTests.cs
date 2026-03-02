using TradingAssistant.Application.Intelligence.Prompts;
using TradingAssistant.Contracts;
using TradingAssistant.Infrastructure.Claude;

namespace TradingAssistant.Tests.Intelligence;

/// <summary>
/// Fake Claude client for testing — returns canned JSON responses.
/// </summary>
public class FakeClaudeClient : IClaudeClient
{
    private readonly Dictionary<string, string> _responses = new();
    private int _callCount;
    private readonly int _maxDailyCalls;

    public FakeClaudeClient(int maxDailyCalls = 50)
    {
        _maxDailyCalls = maxDailyCalls;
    }

    public int RemainingCallsToday => Math.Max(0, _maxDailyCalls - _callCount);
    public bool IsRateLimited => _callCount >= _maxDailyCalls;
    public int CallCount => _callCount;

    public void SetResponse(string containsInPrompt, string jsonResponse)
    {
        _responses[containsInPrompt] = jsonResponse;
    }

    public void SetDefaultResponse(string jsonResponse)
    {
        _responses["*"] = jsonResponse;
    }

    public Task<ClaudeResponse> CompleteAsync(ClaudeRequest request, CancellationToken ct = default)
    {
        if (_callCount >= _maxDailyCalls)
        {
            return Task.FromResult(new ClaudeResponse(
                Content: string.Empty, InputTokens: 0, OutputTokens: 0,
                Success: false, Error: "Rate limit exceeded"));
        }

        _callCount++;

        foreach (var (key, response) in _responses)
        {
            if (key == "*" || request.UserPrompt.Contains(key, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new ClaudeResponse(
                    Content: response, InputTokens: 100, OutputTokens: 200,
                    Success: true));
            }
        }

        return Task.FromResult(new ClaudeResponse(
            Content: "{}", InputTokens: 10, OutputTokens: 10, Success: true));
    }

    public void ResetCallCount() => _callCount = 0;
}

#region FakeClaudeClient Tests

public class FakeClaudeClientTests
{
    [Fact]
    public async Task CompleteAsync_ReturnsMatchedResponse()
    {
        var client = new FakeClaudeClient();
        client.SetResponse("strategy", """{"strategyName":"TestStrat"}""");

        var result = await client.CompleteAsync(new ClaudeRequest("sys", "Generate a strategy"));

        Assert.True(result.Success);
        Assert.Contains("TestStrat", result.Content);
        Assert.Equal(1, client.CallCount);
    }

    [Fact]
    public async Task CompleteAsync_ReturnsDefaultWhenNoMatch()
    {
        var client = new FakeClaudeClient();
        client.SetDefaultResponse("""{"default":true}""");

        var result = await client.CompleteAsync(new ClaudeRequest("sys", "xyz unmatched"));

        Assert.True(result.Success);
        Assert.Contains("default", result.Content);
    }

    [Fact]
    public async Task CompleteAsync_RateLimitedAfterMaxCalls()
    {
        var client = new FakeClaudeClient(maxDailyCalls: 3);
        client.SetDefaultResponse("{}");

        for (var i = 0; i < 3; i++)
        {
            var ok = await client.CompleteAsync(new ClaudeRequest("s", "p"));
            Assert.True(ok.Success);
        }

        Assert.True(client.IsRateLimited);
        Assert.Equal(0, client.RemainingCallsToday);

        var limited = await client.CompleteAsync(new ClaudeRequest("s", "p"));
        Assert.False(limited.Success);
        Assert.Contains("Rate limit", limited.Error);
    }

    [Fact]
    public void RemainingCallsToday_DecreasesWithCalls()
    {
        var client = new FakeClaudeClient(maxDailyCalls: 10);
        Assert.Equal(10, client.RemainingCallsToday);
        Assert.False(client.IsRateLimited);
    }
}

#endregion

#region ClaudeClientWrapper Rate Limiting Tests

public class ClaudeClientWrapperRateLimitTests
{
    [Fact]
    public void RateLimiter_StartsWithFullCapacity()
    {
        var wrapper = new ClaudeClientWrapper(maxDailyCalls: 50);
        Assert.Equal(50, wrapper.RemainingCallsToday);
        Assert.False(wrapper.IsRateLimited);
    }

    [Fact]
    public void RateLimiter_DecreasesOnCalls()
    {
        var wrapper = new ClaudeClientWrapper(maxDailyCalls: 5);
        wrapper.SetDailyCallCount(3);
        Assert.Equal(2, wrapper.RemainingCallsToday);
        Assert.False(wrapper.IsRateLimited);
    }

    [Fact]
    public void RateLimiter_IsRateLimitedWhenFull()
    {
        var wrapper = new ClaudeClientWrapper(maxDailyCalls: 5);
        wrapper.SetDailyCallCount(5);
        Assert.True(wrapper.IsRateLimited);
        Assert.Equal(0, wrapper.RemainingCallsToday);
    }

    [Fact]
    public async Task CompleteAsync_ReturnsErrorWhenRateLimited()
    {
        var wrapper = new ClaudeClientWrapper(maxDailyCalls: 2);
        wrapper.SetDailyCallCount(2);

        var result = await wrapper.CompleteAsync(new ClaudeRequest("sys", "prompt"));

        Assert.False(result.Success);
        Assert.Contains("rate limit", result.Error!, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.Content);
    }

    [Fact]
    public void DefaultMaxDailyCalls_Is50()
    {
        Assert.Equal(50, ClaudeClientWrapper.DefaultMaxDailyCalls);
    }
}

#endregion

#region Strategy Generation Prompt Tests

public class StrategyGenerationPromptTests
{
    [Fact]
    public void BuildSystemPrompt_ContainsKeyInstructions()
    {
        var prompt = StrategyGenerationPrompt.BuildSystemPrompt();

        Assert.Contains("quantitative", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("JSON", prompt);
    }

    [Fact]
    public void BuildUserPrompt_IncludesAllInputFields()
    {
        var input = new StrategyGenerationInput(
            MarketCode: "US_SP500",
            RegimeType: "Bull",
            AvailableIndicators: ["RSI", "MACD", "SMA"],
            MaxDrawdownPercent: 10m,
            TargetSharpe: 1.5m,
            AdditionalConstraints: "No overnight holds");

        var prompt = StrategyGenerationPrompt.BuildUserPrompt(input);

        Assert.Contains("US_SP500", prompt);
        Assert.Contains("Bull", prompt);
        Assert.Contains("RSI", prompt);
        Assert.Contains("MACD", prompt);
        Assert.Contains("10", prompt);
        Assert.Contains("1.5", prompt);
        Assert.Contains("No overnight holds", prompt);
    }

    [Fact]
    public void BuildUserPrompt_HandlesNullConstraints()
    {
        var input = new StrategyGenerationInput(
            MarketCode: "IN_NIFTY50",
            RegimeType: "Sideways",
            AvailableIndicators: ["RSI"],
            MaxDrawdownPercent: 5m,
            TargetSharpe: 1.0m);

        var prompt = StrategyGenerationPrompt.BuildUserPrompt(input);

        Assert.Contains("None", prompt);
    }

    [Fact]
    public void ParseResponse_ValidJson_ReturnsOutput()
    {
        var json = """
            {
              "strategyName": "BullMomentum",
              "description": "Momentum strategy for bull markets",
              "timeframeMinutes": 1440,
              "entryRules": ["RSI > 50", "MACD histogram positive"],
              "exitRules": ["RSI < 40"],
              "stopLossPercent": 3.0,
              "takeProfitPercent": 6.0,
              "indicatorConfigs": [{"name": "RSI", "parameters": {"period": 14}}],
              "rationale": "Momentum works well in bull regimes"
            }
            """;

        var result = StrategyGenerationPrompt.ParseResponse(json);

        Assert.NotNull(result);
        Assert.Equal("BullMomentum", result!.StrategyName);
        Assert.Equal(1440, result.TimeframeMinutes);
        Assert.Equal(2, result.EntryRules.Count);
        Assert.Single(result.ExitRules);
        Assert.Equal(3.0m, result.StopLossPercent);
        Assert.Single(result.IndicatorConfigs);
        Assert.Equal("RSI", result.IndicatorConfigs[0].Name);
    }

    [Fact]
    public void ParseResponse_WithSurroundingText_ExtractsJson()
    {
        var json = """
            Here is the strategy:
            {"strategyName":"Test","description":"d","timeframeMinutes":60,
             "entryRules":[],"exitRules":[],"stopLossPercent":2,"takeProfitPercent":4,
             "indicatorConfigs":[],"rationale":"r"}
            Hope this helps!
            """;

        var result = StrategyGenerationPrompt.ParseResponse(json);

        Assert.NotNull(result);
        Assert.Equal("Test", result!.StrategyName);
    }

    [Fact]
    public void ParseResponse_EmptyJson_ReturnsDefaults()
    {
        var result = StrategyGenerationPrompt.ParseResponse("{}");

        Assert.NotNull(result);
        Assert.Equal(string.Empty, result!.StrategyName);
        Assert.Empty(result.EntryRules);
    }
}

#endregion

#region Trade Review Prompt Tests

public class TradeReviewPromptTests
{
    [Fact]
    public void BuildSystemPrompt_ContainsClassifications()
    {
        var prompt = TradeReviewPrompt.BuildSystemPrompt();

        Assert.Contains("Good", prompt);
        Assert.Contains("Bad", prompt);
        Assert.Contains("Lucky", prompt);
        Assert.Contains("Unlucky", prompt);
    }

    [Fact]
    public void BuildUserPrompt_IncludesTradeDetails()
    {
        var input = new TradeReviewInput(
            Symbol: "AAPL",
            Side: "Long",
            EntryDate: new DateTime(2026, 1, 15),
            ExitDate: new DateTime(2026, 1, 20),
            EntryPrice: 180.50m,
            ExitPrice: 190.00m,
            PnlPercent: 5.26m,
            StrategyName: "BullMomentum",
            IndicatorValuesAtEntry: new Dictionary<string, decimal>
            {
                ["RSI"] = 62.5m,
                ["MACD"] = 1.2m
            });

        var prompt = TradeReviewPrompt.BuildUserPrompt(input);

        Assert.Contains("AAPL", prompt);
        Assert.Contains("Long", prompt);
        Assert.Contains("BullMomentum", prompt);
        Assert.Contains("180.50", prompt);
        Assert.Contains("190.00", prompt);
        Assert.Contains("5.26", prompt);
        Assert.Contains("RSI=62.50", prompt);
    }

    [Fact]
    public void BuildUserPrompt_HandlesNullIndicators()
    {
        var input = new TradeReviewInput("MSFT", "Short",
            new DateTime(2026, 1, 1), new DateTime(2026, 1, 5),
            300m, 290m, 3.33m, "MeanReversion");

        var prompt = TradeReviewPrompt.BuildUserPrompt(input);
        Assert.Contains("N/A", prompt);
    }

    [Fact]
    public void ParseResponse_ValidJson_ReturnsOutput()
    {
        var json = """
            {
              "classification": "Good",
              "score": 8,
              "strengths": ["Timed the entry well", "Good risk management"],
              "weaknesses": ["Could have held longer"],
              "lessonsLearned": ["Momentum confirmation works"],
              "summary": "Well-executed momentum trade"
            }
            """;

        var result = TradeReviewPrompt.ParseResponse(json);

        Assert.NotNull(result);
        Assert.Equal("Good", result!.Classification);
        Assert.Equal(8, result.Score);
        Assert.Equal(2, result.Strengths.Count);
        Assert.Single(result.Weaknesses);
        Assert.Single(result.LessonsLearned);
    }
}

#endregion

#region Strategy Autopsy Prompt Tests

public class StrategyAutopsyPromptTests
{
    [Fact]
    public void BuildUserPrompt_IncludesPerformanceData()
    {
        var input = new StrategyAutopsyInput(
            StrategyName: "BullMomentum",
            PeriodStart: new DateTime(2026, 1, 1),
            PeriodEnd: new DateTime(2026, 3, 1),
            TotalReturnPercent: -5.2m,
            MaxDrawdownPercent: 12.5m,
            WinRate: 35m,
            SharpeRatio: -0.5m,
            TradeCount: 20,
            WorstTrades: ["AAPL -8.2%", "TSLA -6.1%"],
            RegimesDuringPeriod: "Bull → HighVol");

        var prompt = StrategyAutopsyPrompt.BuildUserPrompt(input);

        Assert.Contains("BullMomentum", prompt);
        Assert.Contains("-5.20", prompt);
        Assert.Contains("12.50", prompt);
        Assert.Contains("35.00", prompt);
        Assert.Contains("AAPL -8.2%", prompt);
        Assert.Contains("Bull", prompt);
    }

    [Fact]
    public void BuildUserPrompt_HandlesNullWorstTrades()
    {
        var input = new StrategyAutopsyInput("Strat", DateTime.Now, DateTime.Now,
            -3m, 8m, 40m, 0.2m, 10);

        var prompt = StrategyAutopsyPrompt.BuildUserPrompt(input);
        Assert.Contains("N/A", prompt);
    }

    [Fact]
    public void ParseResponse_ValidJson_ReturnsOutput()
    {
        var json = """
            {
              "rootCauses": ["Strategy overfits to trending markets", "Stop losses too tight"],
              "marketConditionImpact": "Market shifted from Bull to HighVol",
              "recommendations": ["Widen stop losses", "Add volatility filter"],
              "shouldRetire": false,
              "confidence": 0.75,
              "summary": "Strategy underperformed due to regime change"
            }
            """;

        var result = StrategyAutopsyPrompt.ParseResponse(json);

        Assert.NotNull(result);
        Assert.Equal(2, result!.RootCauses.Count);
        Assert.False(result.ShouldRetire);
        Assert.Equal(0.75m, result.Confidence);
        Assert.Contains("regime change", result.Summary);
    }

    [Fact]
    public void ParseResponse_RetirementRecommendation()
    {
        var json = """{"rootCauses":["Fatal flaw"],"marketConditionImpact":"N/A","recommendations":["Retire"],"shouldRetire":true,"confidence":0.95,"summary":"Retire it"}""";

        var result = StrategyAutopsyPrompt.ParseResponse(json);

        Assert.NotNull(result);
        Assert.True(result!.ShouldRetire);
        Assert.Equal(0.95m, result.Confidence);
    }
}

#endregion

#region Rule Discovery Prompt Tests

public class RuleDiscoveryPromptTests
{
    [Fact]
    public void BuildUserPrompt_IncludesTradeData()
    {
        var input = new RuleDiscoveryInput(
            MarketCode: "US_SP500",
            Trades:
            [
                new TradeSummary("AAPL", "Long", 5.2m, 32m, 0.5m, 0.002m, 2.1m, 1000000, true),
                new TradeSummary("MSFT", "Long", -2.1m, 72m, -0.3m, -0.001m, 1.8m, 800000, false)
            ]);

        var prompt = RuleDiscoveryPrompt.BuildUserPrompt(input);

        Assert.Contains("US_SP500", prompt);
        Assert.Contains("2 trades", prompt);
        Assert.Contains("AAPL", prompt);
        Assert.Contains("MSFT", prompt);
        Assert.Contains("Won=True", prompt);
        Assert.Contains("Won=False", prompt);
    }

    [Fact]
    public void ParseResponse_ValidJson_ReturnsDiscoveredRules()
    {
        var json = """
            {
              "discoveredRules": [
                {
                  "rule": "RSI < 35 AND MACD_H > 0",
                  "confidence": 0.82,
                  "supportingTradeCount": 8,
                  "description": "Oversold bounce with MACD confirmation"
                }
              ],
              "patterns": ["Mean reversion works when RSI is oversold"],
              "summary": "Found 1 high-confidence rule"
            }
            """;

        var result = RuleDiscoveryPrompt.ParseResponse(json);

        Assert.NotNull(result);
        Assert.Single(result!.DiscoveredRules);
        Assert.Equal("RSI < 35 AND MACD_H > 0", result.DiscoveredRules[0].Rule);
        Assert.Equal(0.82m, result.DiscoveredRules[0].Confidence);
        Assert.Equal(8, result.DiscoveredRules[0].SupportingTradeCount);
        Assert.Single(result.Patterns);
    }

    [Fact]
    public void ParseResponse_MultipleRules()
    {
        var json = """
            {
              "discoveredRules": [
                {"rule": "Rule1", "confidence": 0.9, "supportingTradeCount": 10, "description": "d1"},
                {"rule": "Rule2", "confidence": 0.7, "supportingTradeCount": 5, "description": "d2"}
              ],
              "patterns": [],
              "summary": "Found 2 rules"
            }
            """;

        var result = RuleDiscoveryPrompt.ParseResponse(json);

        Assert.NotNull(result);
        Assert.Equal(2, result!.DiscoveredRules.Count);
    }
}

#endregion

#region Market Profile Prompt Tests

public class MarketProfilePromptTests
{
    [Fact]
    public void BuildUserPrompt_IncludesMarketData()
    {
        var input = new MarketProfileInput(
            MarketCode: "US_SP500",
            Exchange: "NYSE/NASDAQ",
            HistoricalYears: 5,
            AvgDailyVolume: 3500000000m,
            AnnualizedVolatility: 18.5m,
            TypicalRegimes: "Bull (50%), Sideways (30%), Bear (15%), HighVol (5%)",
            CorrelationPartners: "DXY (-0.6), VIX (-0.8), TLT (-0.3)");

        var prompt = MarketProfilePrompt.BuildUserPrompt(input);

        Assert.Contains("US_SP500", prompt);
        Assert.Contains("NYSE/NASDAQ", prompt);
        Assert.Contains("5 years", prompt);
        Assert.Contains("18.5", prompt);
        Assert.Contains("Bull", prompt);
        Assert.Contains("DXY", prompt);
    }

    [Fact]
    public void ParseResponse_ValidJson_ReturnsProfile()
    {
        var json = """
            {
              "behavioralProfile": "Large-cap US equities market with strong momentum tendencies",
              "typicalPatterns": ["January effect", "Sell in May", "Santa rally"],
              "regimeTendencies": [
                {
                  "regime": "Bull",
                  "frequency": "50%",
                  "avgDuration": "18 months",
                  "bestStrategies": ["momentum", "trend-following"]
                }
              ],
              "riskCharacteristics": {
                "maxHistoricalDrawdown": "-34% (COVID crash, March 2020)",
                "tailRiskProfile": "Fat-tailed, 3-sigma events occur more than normal distribution",
                "liquidityProfile": "Highly liquid, tight spreads",
                "gapRisk": "Low for index, moderate for individual stocks"
              },
              "tradingRecommendations": ["Use momentum strategies in bull regimes", "Reduce exposure when VIX > 30"],
              "summary": "Well-diversified US large-cap market"
            }
            """;

        var result = MarketProfilePrompt.ParseResponse(json);

        Assert.NotNull(result);
        Assert.Contains("momentum", result!.BehavioralProfile);
        Assert.Equal(3, result.TypicalPatterns.Count);
        Assert.Single(result.RegimeTendencies);
        Assert.Equal("Bull", result.RegimeTendencies[0].Regime);
        Assert.Contains("Fat-tailed", result.RiskCharacteristics.TailRiskProfile);
        Assert.Equal(2, result.TradingRecommendations.Count);
    }

    [Fact]
    public void ParseResponse_MinimalJson_ReturnsDefaults()
    {
        var result = MarketProfilePrompt.ParseResponse("{}");

        Assert.NotNull(result);
        Assert.Equal(string.Empty, result!.BehavioralProfile);
        Assert.Empty(result.TypicalPatterns);
    }
}

#endregion

#region End-to-End Prompt + FakeClient Tests

public class ClaudePromptEndToEndTests
{
    [Fact]
    public async Task StrategyGeneration_EndToEnd()
    {
        var client = new FakeClaudeClient();
        client.SetDefaultResponse("""
            {"strategyName":"AutoGenStrat","description":"Auto generated","timeframeMinutes":60,
             "entryRules":["RSI < 30"],"exitRules":["RSI > 70"],"stopLossPercent":2.5,
             "takeProfitPercent":5.0,"indicatorConfigs":[],"rationale":"Mean reversion"}
            """);

        var input = new StrategyGenerationInput("US_SP500", "Sideways", ["RSI", "MACD"], 10m, 1.5m);
        var request = new ClaudeRequest(
            StrategyGenerationPrompt.BuildSystemPrompt(),
            StrategyGenerationPrompt.BuildUserPrompt(input));

        var response = await client.CompleteAsync(request);
        Assert.True(response.Success);

        var output = StrategyGenerationPrompt.ParseResponse(response.Content);
        Assert.NotNull(output);
        Assert.Equal("AutoGenStrat", output!.StrategyName);
        Assert.Single(output.EntryRules);
    }

    [Fact]
    public async Task TradeReview_EndToEnd()
    {
        var client = new FakeClaudeClient();
        client.SetDefaultResponse("""
            {"classification":"Good","score":7,"strengths":["Good timing"],
             "weaknesses":["Could improve exit"],"lessonsLearned":["Patience pays"],
             "summary":"Solid trade"}
            """);

        var input = new TradeReviewInput("AAPL", "Long",
            new DateTime(2026, 1, 1), new DateTime(2026, 1, 10),
            150m, 160m, 6.67m, "TestStrat");
        var request = new ClaudeRequest(
            TradeReviewPrompt.BuildSystemPrompt(),
            TradeReviewPrompt.BuildUserPrompt(input));

        var response = await client.CompleteAsync(request);
        var output = TradeReviewPrompt.ParseResponse(response.Content);

        Assert.NotNull(output);
        Assert.Equal("Good", output!.Classification);
        Assert.Equal(7, output.Score);
    }

    [Fact]
    public async Task RateLimited_ReturnsErrorResponse()
    {
        var client = new FakeClaudeClient(maxDailyCalls: 1);
        client.SetDefaultResponse("{}");

        // First call succeeds
        var r1 = await client.CompleteAsync(new ClaudeRequest("s", "p"));
        Assert.True(r1.Success);

        // Second call is rate limited
        var r2 = await client.CompleteAsync(new ClaudeRequest("s", "p"));
        Assert.False(r2.Success);
        Assert.Contains("Rate limit", r2.Error);
    }
}

#endregion
