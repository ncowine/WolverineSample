using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Intelligence;

/// <summary>
/// Shared context passed through all pipeline steps.
/// Steps can read/write state to coordinate behavior.
/// </summary>
public class PipelineContext
{
    public string MarketCode { get; init; } = string.Empty;
    public DateTime RunDate { get; init; }

    /// <summary>
    /// Set by RiskChecks step when drawdown circuit breaker is tripped.
    /// OrderGeneration step checks this to skip order creation.
    /// </summary>
    public bool CircuitBreakerTripped { get; set; }

    /// <summary>
    /// Set by DataQualityCheck step with flagged symbols.
    /// Downstream steps can use this to skip flagged symbols.
    /// </summary>
    public IReadOnlyList<string> FlaggedSymbols { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Definition of a single pipeline step.
/// </summary>
public record PipelineStepDefinition(
    string StepName,
    int StepOrder,
    Func<PipelineContext, CancellationToken, Task> Execute);

/// <summary>
/// Orchestrates a sequence of pipeline steps with retry, timing, and logging.
///
/// Features:
/// - Sequential step execution in StepOrder
/// - 3× retry with exponential backoff (1s, 5s, 15s) on failure
/// - Each step tracked in PipelineRunLog (step name, status, duration, error, retry count)
/// - Circuit breaker check: OrderGeneration step skipped if CircuitBreakerTripped is set
/// - Continues through remaining steps even if one fails after retries
/// </summary>
public static class DailyPipelineOrchestrator
{
    public const string OrderGenerationStepName = "OrderGeneration";

    private static readonly int[] DefaultRetryDelaysMs = { 1_000, 5_000, 15_000 };

    /// <summary>
    /// Execute all pipeline steps sequentially.
    /// </summary>
    /// <param name="context">Shared context for inter-step communication.</param>
    /// <param name="steps">Step definitions (executed in StepOrder).</param>
    /// <param name="db">Intelligence DB context for persisting PipelineRunLog entries.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <param name="retryDelaysMs">Override retry delays (default: 1000, 5000, 15000). Pass empty for no retries.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of PipelineRunLog entries, one per step.</returns>
    public static async Task<IReadOnlyList<PipelineRunLog>> ExecuteAsync(
        PipelineContext context,
        IReadOnlyList<PipelineStepDefinition> steps,
        IntelligenceDbContext db,
        ILogger? logger = null,
        int[]? retryDelaysMs = null,
        CancellationToken ct = default)
    {
        var delays = retryDelaysMs ?? DefaultRetryDelaysMs;
        var logs = new List<PipelineRunLog>();

        foreach (var step in steps.OrderBy(s => s.StepOrder))
        {
            ct.ThrowIfCancellationRequested();

            var log = new PipelineRunLog
            {
                MarketCode = context.MarketCode,
                RunDate = context.RunDate,
                StepName = step.StepName,
                StepOrder = step.StepOrder,
                Status = PipelineStepStatus.Running
            };

            // Skip order generation if circuit breaker tripped
            if (string.Equals(step.StepName, OrderGenerationStepName, StringComparison.OrdinalIgnoreCase)
                && context.CircuitBreakerTripped)
            {
                log.Status = PipelineStepStatus.Skipped;
                log.Duration = TimeSpan.Zero;
                log.ErrorMessage = "Circuit breaker tripped — orders skipped";
                logger?.LogWarning(
                    "Pipeline [{Market}] step {Step} SKIPPED: circuit breaker tripped",
                    context.MarketCode, step.StepName);

                db.PipelineRunLogs.Add(log);
                await db.SaveChangesAsync(ct);
                logs.Add(log);
                continue;
            }

            var sw = Stopwatch.StartNew();
            var retryCount = 0;

            while (true)
            {
                try
                {
                    await step.Execute(context, ct);
                    sw.Stop();
                    log.Status = PipelineStepStatus.Completed;
                    log.Duration = sw.Elapsed;
                    log.RetryCount = retryCount;
                    logger?.LogInformation(
                        "Pipeline [{Market}] step {Step} completed in {Duration:F1}s",
                        context.MarketCode, step.StepName, sw.Elapsed.TotalSeconds);
                    break;
                }
                catch (OperationCanceledException)
                {
                    throw; // Don't retry cancellation
                }
                catch (Exception ex) when (retryCount < delays.Length)
                {
                    retryCount++;
                    logger?.LogWarning(ex,
                        "Pipeline [{Market}] step {Step} failed (attempt {Attempt}/{MaxAttempts}), retrying in {Delay}ms",
                        context.MarketCode, step.StepName, retryCount, delays.Length + 1,
                        delays[retryCount - 1]);

                    if (delays[retryCount - 1] > 0)
                        await Task.Delay(delays[retryCount - 1], ct);
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    log.Status = PipelineStepStatus.Failed;
                    log.Duration = sw.Elapsed;
                    log.RetryCount = retryCount;
                    log.ErrorMessage = Truncate(ex.Message, 2000);
                    logger?.LogError(ex,
                        "Pipeline [{Market}] step {Step} FAILED after {Retries} retries",
                        context.MarketCode, step.StepName, retryCount);
                    break;
                }
            }

            db.PipelineRunLogs.Add(log);
            await db.SaveChangesAsync(ct);
            logs.Add(log);
        }

        return logs;
    }

    /// <summary>
    /// Build the standard 10-step pipeline definitions.
    /// Each step receives a PipelineContext and CancellationToken.
    /// Pass custom implementations or stubs for each step.
    /// </summary>
    public static IReadOnlyList<PipelineStepDefinition> BuildDefaultSteps(
        Func<PipelineContext, CancellationToken, Task>? dataIngestion = null,
        Func<PipelineContext, CancellationToken, Task>? dataQualityCheck = null,
        Func<PipelineContext, CancellationToken, Task>? breadthComputation = null,
        Func<PipelineContext, CancellationToken, Task>? regimeClassification = null,
        Func<PipelineContext, CancellationToken, Task>? strategySelection = null,
        Func<PipelineContext, CancellationToken, Task>? indicatorComputation = null,
        Func<PipelineContext, CancellationToken, Task>? screenerRun = null,
        Func<PipelineContext, CancellationToken, Task>? mlScoring = null,
        Func<PipelineContext, CancellationToken, Task>? riskChecks = null,
        Func<PipelineContext, CancellationToken, Task>? orderGeneration = null)
    {
        static Task NoOp(PipelineContext _, CancellationToken __) => Task.CompletedTask;

        return new List<PipelineStepDefinition>
        {
            new("DataIngestion", 1, dataIngestion ?? NoOp),
            new("DataQualityCheck", 2, dataQualityCheck ?? NoOp),
            new("BreadthComputation", 3, breadthComputation ?? NoOp),
            new("RegimeClassification", 4, regimeClassification ?? NoOp),
            new("StrategySelection", 5, strategySelection ?? NoOp),
            new("IndicatorComputation", 6, indicatorComputation ?? NoOp),
            new("ScreenerRun", 7, screenerRun ?? NoOp),
            new("MLScoring", 8, mlScoring ?? NoOp),
            new("RiskChecks", 9, riskChecks ?? NoOp),
            new(OrderGenerationStepName, 10, orderGeneration ?? NoOp),
        };
    }

    /// <summary>
    /// Parse the pipeline trigger hour from a MarketProfile's ConfigJson.
    /// Looks for {"pipelineTriggerUtcHour": N}. Returns 21 (US default) if not found.
    /// </summary>
    public static int GetTriggerHourUtc(string configJson, int defaultHour = 21)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(configJson);
            if (doc.RootElement.TryGetProperty("pipelineTriggerUtcHour", out var prop)
                && prop.TryGetInt32(out var hour))
            {
                return hour;
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // Fall through to default
        }

        return defaultHour;
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}
