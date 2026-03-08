using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TradingAssistant.Application.Handlers.Intelligence;
using TradingAssistant.Contracts;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Infrastructure.Persistence;
using TradingAssistant.SharedKernel;
using Wolverine;
using Wolverine.Http;

namespace TradingAssistant.Api.Endpoints;

public class BacktestEndpoints : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        var strategies = app.MapGroup("/api/strategies")
            .WithTags("Strategies")
            .RequireAuthorization();

        strategies.MapPostToWolverine<CreateStrategyCommand, StrategyDto>("/")
            .WithSummary("Create a new trading strategy with rules (v1 — simple rules)");

        strategies.MapPostToWolverine<CreateStrategyV2Command, StrategyV2Dto>("/v2")
            .WithSummary("Create a v2 strategy with multi-timeframe conditions and risk management");

        strategies.MapGet("/", ListStrategies)
            .WithSummary("List all trading strategies (paginated)");

        strategies.MapGet("/{strategyId}/optimized-params", GetOptimizedParams)
            .WithSummary("Get current and historical optimized parameters for a strategy");

        strategies.MapGet("/templates/{marketCode}", GetTemplates)
            .WithSummary("Get pre-built strategy templates (playbooks) for a market");

        strategies.MapPost("/templates/generate", GeneratePlaybooks)
            .WithSummary("Generate market-specific strategy templates using AI (with hardcoded fallback)");

        var backtests = app.MapGroup("/api/backtests")
            .WithTags("Backtests")
            .RequireAuthorization();

        // Unified: single-symbol OR portfolio backtest (set UniverseId for portfolio)
        backtests.MapPost("/", RunBacktest)
            .WithSummary("Run a backtest (single-symbol or portfolio). Set UniverseId for portfolio mode.");

        backtests.MapGet("/{backtestRunId}", GetBacktestResult)
            .WithSummary("Get the result of a specific backtest run");

        backtests.MapGet("/", ListBacktestRuns)
            .WithSummary("List all backtest runs (single + portfolio, unified)");

        backtests.MapGet("/compare", CompareBacktests)
            .WithSummary("Compare multiple backtest runs side-by-side (up to 10)");

        backtests.MapDelete("/", ClearAllBacktests)
            .WithSummary("Delete all backtest results and runs");

        backtests.MapGet("/{backtestRunId}/trades/{tradeIndex:int}/chart", GetTradeChartData)
            .WithSummary("Get OHLCV candle data around a specific trade for chart visualization");

        backtests.MapPost("/optimize", RunOptimization)
            .WithSummary("Run walk-forward optimization to find best parameters for a strategy");

        backtests.MapPost("/{backtestRunId}/auto-optimize", AutoOptimize)
            .WithSummary("Auto-diagnose weaknesses and run optimization with generated parameter ranges");

        backtests.MapGet("/{backtestRunId}/auto-optimize/progress", GetAutoOptimizeProgress)
            .WithSummary("Poll auto-optimize progress");
    }

    private static async Task<BacktestResultDto> RunBacktest(
        RunBacktestCommand command,
        IMessageBus bus)
    {
        return await bus.InvokeAsync<BacktestResultDto>(command);
    }

    private static async Task<OptimizationResultDto> RunOptimization(
        RunOptimizationCommand command,
        IMessageBus bus)
    {
        return await bus.InvokeAsync<OptimizationResultDto>(command);
    }

    private static async Task<AutoOptimizeResultDto> AutoOptimize(
        Guid backtestRunId,
        IMessageBus bus)
    {
        return await bus.InvokeAsync<AutoOptimizeResultDto>(new AutoOptimizeCommand(backtestRunId));
    }

    private static IResult GetAutoOptimizeProgress(Guid backtestRunId)
    {
        var entry = Application.Backtesting.AutoOptimizeProgressStore.Get(backtestRunId);
        if (entry is null)
            return Results.Ok(new { step = "idle", windowIndex = 0, totalWindows = 0, completedCombos = 0L, totalCombos = 0L });
        return Results.Ok(new
        {
            step = entry.Step,
            windowIndex = entry.WindowIndex,
            totalWindows = entry.TotalWindows,
            completedCombos = entry.CompletedCombos,
            totalCombos = entry.TotalCombos,
        });
    }

    private static async Task<PagedResponse<StrategyDto>> ListStrategies(
        int page,
        int pageSize,
        IMessageBus bus)
    {
        return await bus.InvokeAsync<PagedResponse<StrategyDto>>(
            new ListStrategiesQuery(page > 0 ? page : 1, pageSize > 0 ? pageSize : 20));
    }

    private static async Task<BacktestResultDto> GetBacktestResult(Guid backtestRunId, IMessageBus bus)
    {
        return await bus.InvokeAsync<BacktestResultDto>(new GetBacktestResultQuery(backtestRunId));
    }

    private static async Task<PagedResponse<BacktestRunDto>> ListBacktestRuns(
        Guid? strategyId,
        int page,
        int pageSize,
        IMessageBus bus)
    {
        return await bus.InvokeAsync<PagedResponse<BacktestRunDto>>(
            new ListBacktestRunsQuery(strategyId, page > 0 ? page : 1, pageSize > 0 ? pageSize : 20));
    }

    private static async Task<BacktestComparisonDto> CompareBacktests(
        [FromQuery] string ids,
        IMessageBus bus)
    {
        var parsedIds = ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Guid.TryParse(s, out var g) ? g : (Guid?)null)
            .Where(g => g.HasValue)
            .Select(g => g!.Value)
            .ToList();

        return await bus.InvokeAsync<BacktestComparisonDto>(new CompareBacktestsQuery(parsedIds));
    }

    private static async Task<int> ClearAllBacktests(IMessageBus bus)
    {
        return await bus.InvokeAsync<int>(new ClearBacktestResultsCommand());
    }

    private static async Task<TradeChartDataDto> GetTradeChartData(
        Guid backtestRunId,
        int tradeIndex,
        BacktestDbContext backtestDb,
        MarketDataDbContext marketDb)
    {
        return await Application.Handlers.Backtesting.GetTradeChartDataHandler.HandleAsync(
            new GetTradeChartDataQuery(backtestRunId, tradeIndex),
            backtestDb,
            marketDb);
    }

    private static async Task<OptimizedParamsResponse> GetOptimizedParams(
        Guid strategyId,
        IMessageBus bus)
    {
        return await bus.InvokeAsync<OptimizedParamsResponse>(
            new GetOptimizedParamsQuery(strategyId));
    }

    private static async Task<IReadOnlyList<StrategyV2Dto>> GetTemplates(
        [FromRoute] string marketCode,
        BacktestDbContext db)
    {
        return await GetTemplatesHandler.HandleAsync(new GetTemplatesQuery(marketCode), db);
    }

    private static async Task<GeneratePlaybooksResultDto> GeneratePlaybooks(
        [FromBody] GeneratePlaybooksCommand command,
        IClaudeClient claude,
        BacktestDbContext backtestDb,
        IntelligenceDbContext intelligenceDb,
        ILogger<GeneratePlaybooksHandler> logger)
    {
        return await GeneratePlaybooksHandler.HandleAsync(command, claude, backtestDb, intelligenceDb, logger);
    }
}
