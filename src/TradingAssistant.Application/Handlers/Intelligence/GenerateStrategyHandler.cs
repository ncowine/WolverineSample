using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingAssistant.Application.Handlers.Backtesting;
using TradingAssistant.Application.Intelligence;
using TradingAssistant.Application.Intelligence.Prompts;
using TradingAssistant.Contracts;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Domain.Backtesting;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Intelligence;

public class GenerateStrategyHandler
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static async Task<GenerateStrategyResultDto> HandleAsync(
        GenerateStrategyCommand command,
        IClaudeClient claude,
        MarketDataDbContext marketDb,
        BacktestDbContext backtestDb,
        ILogger<GenerateStrategyHandler> logger)
    {
        logger.LogInformation("Generating strategy for {Market} ({Regime}): {Desc}",
            command.MarketCode, command.RegimeType, command.Description);

        // 1. Call Claude to generate StrategyDefinition
        var input = new StrategyDefinitionInput(
            command.Description,
            command.MarketCode,
            command.RegimeType,
            command.MaxDrawdownPercent,
            command.TargetSharpe,
            command.AdditionalConstraints);

        var (definition, rationale, error) = await StrategyGenerator.GenerateAsync(claude, input);
        if (definition is null)
        {
            return new GenerateStrategyResultDto(
                Success: false,
                Rejected: false,
                Strategy: null,
                BacktestMetrics: null,
                Rationale: rationale,
                RejectionReason: error);
        }

        // 2. Fetch 2 years of daily candles for the market code
        var twoYearsAgo = DateTime.UtcNow.AddYears(-2);
        var stock = await marketDb.Stocks
            .FirstOrDefaultAsync(s => s.Symbol == command.MarketCode && s.IsActive);

        if (stock is null)
        {
            return new GenerateStrategyResultDto(
                Success: false,
                Rejected: false,
                Strategy: null,
                BacktestMetrics: null,
                Rationale: rationale,
                RejectionReason: $"No market data found for symbol '{command.MarketCode}'.");
        }

        var candles = await marketDb.PriceCandles
            .Where(c => c.StockId == stock.Id
                        && c.Interval == CandleInterval.Daily
                        && c.Timestamp >= twoYearsAgo)
            .OrderBy(c => c.Timestamp)
            .ToListAsync();

        if (candles.Count < 50)
        {
            return new GenerateStrategyResultDto(
                Success: false,
                Rejected: false,
                Strategy: null,
                BacktestMetrics: null,
                Rationale: rationale,
                RejectionReason: $"Insufficient historical data for '{command.MarketCode}' ({candles.Count} bars, need at least 50).");
        }

        // 3. Run backtest
        logger.LogInformation("Backtesting generated strategy on {Count} bars for {Symbol}",
            candles.Count, command.MarketCode);

        var (backtestResult, metrics) = StrategyGenerator.Backtest(
            definition, candles, command.MarketCode);

        var summary = StrategyGenerator.ToSummary(metrics);

        // 4. Validate Sharpe threshold
        var (accepted, rejectionReason) = StrategyGenerator.Validate(metrics, command.TargetSharpe);

        if (!accepted)
        {
            logger.LogInformation("Strategy rejected: {Reason}", rejectionReason);
            return new GenerateStrategyResultDto(
                Success: false,
                Rejected: true,
                Strategy: null,
                BacktestMetrics: summary,
                Rationale: rationale,
                RejectionReason: rejectionReason);
        }

        // 5. Save the accepted strategy
        var strategyName = $"AI-{command.MarketCode}-{command.RegimeType}-{DateTime.UtcNow:yyyyMMddHHmm}";
        var strategy = new Strategy
        {
            Name = strategyName,
            Description = $"[AI Generated] {command.Description}",
            IsActive = true,
            RulesJson = JsonSerializer.Serialize(definition, JsonOpts)
        };

        backtestDb.Strategies.Add(strategy);
        await backtestDb.SaveChangesAsync();

        var dto = CreateStrategyV2Handler.MapToDto(strategy, definition);

        logger.LogInformation("Strategy '{Name}' saved (Sharpe: {Sharpe:F2})", strategyName, metrics.SharpeRatio);

        return new GenerateStrategyResultDto(
            Success: true,
            Rejected: false,
            Strategy: dto,
            BacktestMetrics: summary,
            Rationale: rationale);
    }
}
