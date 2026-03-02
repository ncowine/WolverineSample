using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application.Intelligence;
using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.Infrastructure.Persistence;
using TradingAssistant.Tests.Helpers;

namespace TradingAssistant.Tests.Intelligence;

public class DailyPipelineOrchestratorTests
{
    private IntelligenceDbContext CreateDb() => TestIntelligenceDbContextFactory.Create();

    private static PipelineContext MakeContext(string market = "US_SP500")
        => new() { MarketCode = market, RunDate = new DateTime(2026, 3, 2, 21, 0, 0, DateTimeKind.Utc) };

    // Zero-delay retries for fast tests
    private static readonly int[] NoDelayRetries = { 0, 0, 0 };

    // ── Basic Execution ──

    [Fact]
    public async Task Execute_AllStepsComplete_RecordsAllLogs()
    {
        await using var db = CreateDb();
        var context = MakeContext();

        var steps = DailyPipelineOrchestrator.BuildDefaultSteps();

        var logs = await DailyPipelineOrchestrator.ExecuteAsync(
            context, steps, db, retryDelaysMs: NoDelayRetries);

        Assert.Equal(10, logs.Count);
        Assert.All(logs, l => Assert.Equal(PipelineStepStatus.Completed, l.Status));
        Assert.All(logs, l => Assert.Equal("US_SP500", l.MarketCode));

        // Verify persisted to database
        var persisted = await db.PipelineRunLogs.CountAsync();
        Assert.Equal(10, persisted);
    }

    [Fact]
    public async Task Execute_StepsRunInOrder()
    {
        await using var db = CreateDb();
        var context = MakeContext();
        var executionOrder = new List<string>();

        var steps = DailyPipelineOrchestrator.BuildDefaultSteps(
            dataIngestion: (_, _) => { executionOrder.Add("DataIngestion"); return Task.CompletedTask; },
            dataQualityCheck: (_, _) => { executionOrder.Add("DataQualityCheck"); return Task.CompletedTask; },
            breadthComputation: (_, _) => { executionOrder.Add("BreadthComputation"); return Task.CompletedTask; },
            regimeClassification: (_, _) => { executionOrder.Add("RegimeClassification"); return Task.CompletedTask; },
            strategySelection: (_, _) => { executionOrder.Add("StrategySelection"); return Task.CompletedTask; },
            indicatorComputation: (_, _) => { executionOrder.Add("IndicatorComputation"); return Task.CompletedTask; },
            screenerRun: (_, _) => { executionOrder.Add("ScreenerRun"); return Task.CompletedTask; },
            mlScoring: (_, _) => { executionOrder.Add("MLScoring"); return Task.CompletedTask; },
            riskChecks: (_, _) => { executionOrder.Add("RiskChecks"); return Task.CompletedTask; },
            orderGeneration: (_, _) => { executionOrder.Add("OrderGeneration"); return Task.CompletedTask; });

        await DailyPipelineOrchestrator.ExecuteAsync(
            context, steps, db, retryDelaysMs: NoDelayRetries);

        Assert.Equal(10, executionOrder.Count);
        Assert.Equal("DataIngestion", executionOrder[0]);
        Assert.Equal("DataQualityCheck", executionOrder[1]);
        Assert.Equal("BreadthComputation", executionOrder[2]);
        Assert.Equal("RegimeClassification", executionOrder[3]);
        Assert.Equal("StrategySelection", executionOrder[4]);
        Assert.Equal("IndicatorComputation", executionOrder[5]);
        Assert.Equal("ScreenerRun", executionOrder[6]);
        Assert.Equal("MLScoring", executionOrder[7]);
        Assert.Equal("RiskChecks", executionOrder[8]);
        Assert.Equal("OrderGeneration", executionOrder[9]);
    }

    [Fact]
    public async Task Execute_StepOrderNumbers_AreSequential()
    {
        await using var db = CreateDb();
        var context = MakeContext();

        var steps = DailyPipelineOrchestrator.BuildDefaultSteps();
        var logs = await DailyPipelineOrchestrator.ExecuteAsync(
            context, steps, db, retryDelaysMs: NoDelayRetries);

        for (var i = 0; i < logs.Count; i++)
        {
            Assert.Equal(i + 1, logs[i].StepOrder);
        }
    }

    [Fact]
    public async Task Execute_RecordsDuration()
    {
        await using var db = CreateDb();
        var context = MakeContext();

        var steps = DailyPipelineOrchestrator.BuildDefaultSteps(
            dataIngestion: async (_, _) => await Task.Delay(50));

        var logs = await DailyPipelineOrchestrator.ExecuteAsync(
            context, steps, db, retryDelaysMs: NoDelayRetries);

        // DataIngestion should have measurable duration
        var ingestionLog = logs.First(l => l.StepName == "DataIngestion");
        Assert.True(ingestionLog.Duration >= TimeSpan.FromMilliseconds(40));
    }

    [Fact]
    public async Task Execute_SetsRunDate()
    {
        await using var db = CreateDb();
        var runDate = new DateTime(2026, 3, 2, 21, 0, 0, DateTimeKind.Utc);
        var context = new PipelineContext { MarketCode = "US_SP500", RunDate = runDate };

        var steps = DailyPipelineOrchestrator.BuildDefaultSteps();
        var logs = await DailyPipelineOrchestrator.ExecuteAsync(
            context, steps, db, retryDelaysMs: NoDelayRetries);

        Assert.All(logs, l => Assert.Equal(runDate, l.RunDate));
    }

    // ── Retry Logic ──

    [Fact]
    public async Task Execute_FailThenSucceed_RetriesAndCompletes()
    {
        await using var db = CreateDb();
        var context = MakeContext();
        var callCount = 0;

        var steps = DailyPipelineOrchestrator.BuildDefaultSteps(
            dataIngestion: (_, _) =>
            {
                callCount++;
                if (callCount <= 2) throw new Exception("Transient error");
                return Task.CompletedTask;
            });

        var logs = await DailyPipelineOrchestrator.ExecuteAsync(
            context, steps, db, retryDelaysMs: NoDelayRetries);

        var ingestionLog = logs.First(l => l.StepName == "DataIngestion");
        Assert.Equal(PipelineStepStatus.Completed, ingestionLog.Status);
        Assert.Equal(2, ingestionLog.RetryCount);
        Assert.Equal(3, callCount); // 1 initial + 2 retries
    }

    [Fact]
    public async Task Execute_FailAllRetries_StatusFailed()
    {
        await using var db = CreateDb();
        var context = MakeContext();

        var steps = DailyPipelineOrchestrator.BuildDefaultSteps(
            dataIngestion: (_, _) => throw new Exception("Persistent error"));

        var logs = await DailyPipelineOrchestrator.ExecuteAsync(
            context, steps, db, retryDelaysMs: NoDelayRetries);

        var ingestionLog = logs.First(l => l.StepName == "DataIngestion");
        Assert.Equal(PipelineStepStatus.Failed, ingestionLog.Status);
        Assert.Equal(3, ingestionLog.RetryCount);
        Assert.Contains("Persistent error", ingestionLog.ErrorMessage);
    }

    [Fact]
    public async Task Execute_FailedStep_ContinuesWithRemainingSteps()
    {
        await using var db = CreateDb();
        var context = MakeContext();
        var orderGenRan = false;

        var steps = DailyPipelineOrchestrator.BuildDefaultSteps(
            dataIngestion: (_, _) => throw new Exception("Fail"),
            orderGeneration: (_, _) => { orderGenRan = true; return Task.CompletedTask; });

        var logs = await DailyPipelineOrchestrator.ExecuteAsync(
            context, steps, db, retryDelaysMs: NoDelayRetries);

        Assert.Equal(PipelineStepStatus.Failed, logs[0].Status);
        Assert.True(orderGenRan); // Last step still ran
        Assert.Equal(10, logs.Count); // All steps tracked
    }

    [Fact]
    public async Task Execute_NoRetries_FailsImmediately()
    {
        await using var db = CreateDb();
        var context = MakeContext();
        var callCount = 0;

        var steps = DailyPipelineOrchestrator.BuildDefaultSteps(
            dataIngestion: (_, _) => { callCount++; throw new Exception("Fail"); });

        var logs = await DailyPipelineOrchestrator.ExecuteAsync(
            context, steps, db, retryDelaysMs: Array.Empty<int>());

        var ingestionLog = logs.First(l => l.StepName == "DataIngestion");
        Assert.Equal(PipelineStepStatus.Failed, ingestionLog.Status);
        Assert.Equal(0, ingestionLog.RetryCount);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task Execute_ErrorMessageTruncatedTo2000Chars()
    {
        await using var db = CreateDb();
        var context = MakeContext();
        var longMessage = new string('x', 3000);

        var steps = DailyPipelineOrchestrator.BuildDefaultSteps(
            dataIngestion: (_, _) => throw new Exception(longMessage));

        var logs = await DailyPipelineOrchestrator.ExecuteAsync(
            context, steps, db, retryDelaysMs: Array.Empty<int>());

        var log = logs.First(l => l.StepName == "DataIngestion");
        Assert.Equal(2000, log.ErrorMessage!.Length);
    }

    // ── Circuit Breaker Skip ──

    [Fact]
    public async Task Execute_CircuitBreakerTripped_SkipsOrderGeneration()
    {
        await using var db = CreateDb();
        var context = MakeContext();
        context.CircuitBreakerTripped = true;
        var orderGenRan = false;

        var steps = DailyPipelineOrchestrator.BuildDefaultSteps(
            orderGeneration: (_, _) => { orderGenRan = true; return Task.CompletedTask; });

        var logs = await DailyPipelineOrchestrator.ExecuteAsync(
            context, steps, db, retryDelaysMs: NoDelayRetries);

        var orderLog = logs.First(l => l.StepName == "OrderGeneration");
        Assert.Equal(PipelineStepStatus.Skipped, orderLog.Status);
        Assert.Contains("Circuit breaker", orderLog.ErrorMessage);
        Assert.False(orderGenRan);
    }

    [Fact]
    public async Task Execute_CircuitBreakerNotTripped_RunsOrderGeneration()
    {
        await using var db = CreateDb();
        var context = MakeContext();
        context.CircuitBreakerTripped = false;
        var orderGenRan = false;

        var steps = DailyPipelineOrchestrator.BuildDefaultSteps(
            orderGeneration: (_, _) => { orderGenRan = true; return Task.CompletedTask; });

        var logs = await DailyPipelineOrchestrator.ExecuteAsync(
            context, steps, db, retryDelaysMs: NoDelayRetries);

        var orderLog = logs.First(l => l.StepName == "OrderGeneration");
        Assert.Equal(PipelineStepStatus.Completed, orderLog.Status);
        Assert.True(orderGenRan);
    }

    [Fact]
    public async Task Execute_RiskChecksTripsBreaker_OrderGenerationSkipped()
    {
        await using var db = CreateDb();
        var context = MakeContext();

        var steps = DailyPipelineOrchestrator.BuildDefaultSteps(
            riskChecks: (ctx, _) =>
            {
                ctx.CircuitBreakerTripped = true; // Simulates breaker tripping during risk checks
                return Task.CompletedTask;
            });

        var logs = await DailyPipelineOrchestrator.ExecuteAsync(
            context, steps, db, retryDelaysMs: NoDelayRetries);

        var riskLog = logs.First(l => l.StepName == "RiskChecks");
        Assert.Equal(PipelineStepStatus.Completed, riskLog.Status);

        var orderLog = logs.First(l => l.StepName == "OrderGeneration");
        Assert.Equal(PipelineStepStatus.Skipped, orderLog.Status);
    }

    // ── Context Sharing ──

    [Fact]
    public async Task Execute_ContextSharedAcrossSteps()
    {
        await using var db = CreateDb();
        var context = MakeContext();
        var flaggedSymbols = new List<string> { "BAD_STOCK" };

        var steps = DailyPipelineOrchestrator.BuildDefaultSteps(
            dataQualityCheck: (ctx, _) =>
            {
                ctx.FlaggedSymbols = flaggedSymbols;
                return Task.CompletedTask;
            },
            screenerRun: (ctx, _) =>
            {
                // Verify context was set by previous step
                Assert.Single(ctx.FlaggedSymbols);
                Assert.Equal("BAD_STOCK", ctx.FlaggedSymbols[0]);
                return Task.CompletedTask;
            });

        var logs = await DailyPipelineOrchestrator.ExecuteAsync(
            context, steps, db, retryDelaysMs: NoDelayRetries);

        Assert.All(logs, l => Assert.Equal(PipelineStepStatus.Completed, l.Status));
    }

    // ── BuildDefaultSteps ──

    [Fact]
    public void BuildDefaultSteps_Returns10Steps()
    {
        var steps = DailyPipelineOrchestrator.BuildDefaultSteps();

        Assert.Equal(10, steps.Count);
        Assert.Equal("DataIngestion", steps[0].StepName);
        Assert.Equal(1, steps[0].StepOrder);
        Assert.Equal("OrderGeneration", steps[9].StepName);
        Assert.Equal(10, steps[9].StepOrder);
    }

    [Fact]
    public void BuildDefaultSteps_CustomStepOverridesDefault()
    {
        var customRan = false;

        var steps = DailyPipelineOrchestrator.BuildDefaultSteps(
            dataIngestion: (_, _) => { customRan = true; return Task.CompletedTask; });

        Assert.Equal(10, steps.Count);
        steps[0].Execute(MakeContext(), CancellationToken.None);
        Assert.True(customRan);
    }

    // ── Multiple Markets ──

    [Fact]
    public async Task Execute_DifferentMarkets_IsolatedLogs()
    {
        await using var db = CreateDb();

        var usContext = MakeContext("US_SP500");
        var inContext = MakeContext("IN_NIFTY50");
        var steps = DailyPipelineOrchestrator.BuildDefaultSteps();

        await DailyPipelineOrchestrator.ExecuteAsync(
            usContext, steps, db, retryDelaysMs: NoDelayRetries);
        await DailyPipelineOrchestrator.ExecuteAsync(
            inContext, steps, db, retryDelaysMs: NoDelayRetries);

        var usLogs = await db.PipelineRunLogs
            .Where(l => l.MarketCode == "US_SP500").CountAsync();
        var inLogs = await db.PipelineRunLogs
            .Where(l => l.MarketCode == "IN_NIFTY50").CountAsync();

        Assert.Equal(10, usLogs);
        Assert.Equal(10, inLogs);
    }

    // ── Cancellation ──

    [Fact]
    public async Task Execute_CancellationRequested_ThrowsWithoutRetry()
    {
        await using var db = CreateDb();
        var context = MakeContext();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var steps = DailyPipelineOrchestrator.BuildDefaultSteps();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            DailyPipelineOrchestrator.ExecuteAsync(
                context, steps, db, retryDelaysMs: NoDelayRetries, ct: cts.Token));
    }
}

public class GetPipelineStatusHandlerTests
{
    private IntelligenceDbContext CreateDb() => TestIntelligenceDbContextFactory.Create();

    [Fact]
    public async Task HandleAsync_NoLogs_ReturnsEmpty()
    {
        await using var db = CreateDb();
        var query = new TradingAssistant.Contracts.Queries.GetPipelineStatusQuery();

        var result = await TradingAssistant.Application.Handlers.Intelligence
            .GetPipelineStatusHandler.HandleAsync(query, db);

        Assert.Empty(result);
    }

    [Fact]
    public async Task HandleAsync_WithLogs_ReturnsLatestRun()
    {
        await using var db = CreateDb();
        var context = new PipelineContext
        {
            MarketCode = "US_SP500",
            RunDate = new DateTime(2026, 3, 2, 21, 0, 0, DateTimeKind.Utc)
        };

        var steps = DailyPipelineOrchestrator.BuildDefaultSteps();
        await DailyPipelineOrchestrator.ExecuteAsync(
            context, steps, db, retryDelaysMs: new[] { 0, 0, 0 });

        var query = new TradingAssistant.Contracts.Queries.GetPipelineStatusQuery();
        var result = await TradingAssistant.Application.Handlers.Intelligence
            .GetPipelineStatusHandler.HandleAsync(query, db);

        Assert.Single(result);
        Assert.Equal("US_SP500", result[0].MarketCode);
        Assert.Equal(10, result[0].TotalSteps);
        Assert.Equal(10, result[0].CompletedSteps);
        Assert.Equal(0, result[0].FailedSteps);
        Assert.Equal("Completed", result[0].OverallStatus);
    }

    [Fact]
    public async Task HandleAsync_WithFailedStep_ReturnsPartialFailure()
    {
        await using var db = CreateDb();
        var context = new PipelineContext
        {
            MarketCode = "US_SP500",
            RunDate = new DateTime(2026, 3, 2, 21, 0, 0, DateTimeKind.Utc)
        };

        var steps = DailyPipelineOrchestrator.BuildDefaultSteps(
            dataIngestion: (_, _) => throw new Exception("Fail"));

        await DailyPipelineOrchestrator.ExecuteAsync(
            context, steps, db, retryDelaysMs: new[] { 0, 0, 0 });

        var result = await TradingAssistant.Application.Handlers.Intelligence
            .GetPipelineStatusHandler.HandleAsync(
                new TradingAssistant.Contracts.Queries.GetPipelineStatusQuery(), db);

        Assert.Equal("PartialFailure", result[0].OverallStatus);
        Assert.Equal(1, result[0].FailedSteps);
        Assert.Equal(9, result[0].CompletedSteps);
    }

    [Fact]
    public async Task HandleAsync_WithSkippedStep_ReturnsCompletedWithSkips()
    {
        await using var db = CreateDb();
        var context = new PipelineContext
        {
            MarketCode = "US_SP500",
            RunDate = new DateTime(2026, 3, 2, 21, 0, 0, DateTimeKind.Utc),
            CircuitBreakerTripped = true
        };

        var steps = DailyPipelineOrchestrator.BuildDefaultSteps();
        await DailyPipelineOrchestrator.ExecuteAsync(
            context, steps, db, retryDelaysMs: new[] { 0, 0, 0 });

        var result = await TradingAssistant.Application.Handlers.Intelligence
            .GetPipelineStatusHandler.HandleAsync(
                new TradingAssistant.Contracts.Queries.GetPipelineStatusQuery(), db);

        Assert.Equal("CompletedWithSkips", result[0].OverallStatus);
        Assert.Equal(1, result[0].SkippedSteps);
        Assert.Equal(9, result[0].CompletedSteps);
    }

    [Fact]
    public async Task HandleAsync_FilterByMarketCode()
    {
        await using var db = CreateDb();
        var runDate = new DateTime(2026, 3, 2, 21, 0, 0, DateTimeKind.Utc);
        var steps = DailyPipelineOrchestrator.BuildDefaultSteps();

        await DailyPipelineOrchestrator.ExecuteAsync(
            new PipelineContext { MarketCode = "US_SP500", RunDate = runDate },
            steps, db, retryDelaysMs: new[] { 0, 0, 0 });
        await DailyPipelineOrchestrator.ExecuteAsync(
            new PipelineContext { MarketCode = "IN_NIFTY50", RunDate = runDate },
            steps, db, retryDelaysMs: new[] { 0, 0, 0 });

        var usResult = await TradingAssistant.Application.Handlers.Intelligence
            .GetPipelineStatusHandler.HandleAsync(
                new TradingAssistant.Contracts.Queries.GetPipelineStatusQuery("US_SP500"), db);

        Assert.Single(usResult);
        Assert.Equal("US_SP500", usResult[0].MarketCode);
    }

    [Fact]
    public async Task HandleAsync_ReturnsLatestRunOnly()
    {
        await using var db = CreateDb();
        var steps = DailyPipelineOrchestrator.BuildDefaultSteps();

        // Run 1 (older)
        await DailyPipelineOrchestrator.ExecuteAsync(
            new PipelineContext
            {
                MarketCode = "US_SP500",
                RunDate = new DateTime(2026, 3, 1, 21, 0, 0, DateTimeKind.Utc)
            },
            steps, db, retryDelaysMs: new[] { 0, 0, 0 });

        // Run 2 (newer)
        await DailyPipelineOrchestrator.ExecuteAsync(
            new PipelineContext
            {
                MarketCode = "US_SP500",
                RunDate = new DateTime(2026, 3, 2, 21, 0, 0, DateTimeKind.Utc)
            },
            steps, db, retryDelaysMs: new[] { 0, 0, 0 });

        var result = await TradingAssistant.Application.Handlers.Intelligence
            .GetPipelineStatusHandler.HandleAsync(
                new TradingAssistant.Contracts.Queries.GetPipelineStatusQuery(), db);

        Assert.Single(result);
        Assert.Equal(new DateTime(2026, 3, 2, 21, 0, 0, DateTimeKind.Utc), result[0].RunDate);
    }

    [Fact]
    public async Task HandleAsync_StepsOrderedByStepOrder()
    {
        await using var db = CreateDb();
        var steps = DailyPipelineOrchestrator.BuildDefaultSteps();
        await DailyPipelineOrchestrator.ExecuteAsync(
            new PipelineContext
            {
                MarketCode = "US_SP500",
                RunDate = new DateTime(2026, 3, 2, 21, 0, 0, DateTimeKind.Utc)
            },
            steps, db, retryDelaysMs: new[] { 0, 0, 0 });

        var result = await TradingAssistant.Application.Handlers.Intelligence
            .GetPipelineStatusHandler.HandleAsync(
                new TradingAssistant.Contracts.Queries.GetPipelineStatusQuery(), db);

        var stepDtos = result[0].Steps;
        for (var i = 0; i < stepDtos.Count - 1; i++)
        {
            Assert.True(stepDtos[i].StepOrder < stepDtos[i + 1].StepOrder);
        }
    }
}

public class PipelineTriggerHourTests
{
    [Theory]
    [InlineData("""{"pipelineTriggerUtcHour": 11}""", 11)]
    [InlineData("""{"pipelineTriggerUtcHour": 21}""", 21)]
    [InlineData("""{"pipelineTriggerUtcHour": 0}""", 0)]
    [InlineData("""{}""", 21)]
    [InlineData("""{"tradingHours":{"open":"09:30"}}""", 21)]
    [InlineData("", 21)]
    [InlineData("{invalid json", 21)]
    public void GetTriggerHourUtc_ParsesCorrectly(string configJson, int expected)
    {
        var result = DailyPipelineOrchestrator.GetTriggerHourUtc(configJson);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetTriggerHourUtc_CustomDefault()
    {
        var result = DailyPipelineOrchestrator.GetTriggerHourUtc("{}", defaultHour: 11);
        Assert.Equal(11, result);
    }
}
