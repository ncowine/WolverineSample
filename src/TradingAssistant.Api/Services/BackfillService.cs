using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application.Handlers.MarketData;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.MarketData;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Domain.MarketData;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Api.Services;

public class BackfillService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackfillService> _logger;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan RateLimitDelay = TimeSpan.FromMilliseconds(500);

    public BackfillService(
        IServiceScopeFactory scopeFactory,
        ILogger<BackfillService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Backfill service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingJobs(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in backfill service");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ProcessPendingJobs(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketDataDbContext>();

        var pendingJob = await db.BackfillJobs
            .Where(j => j.Status == BackfillStatus.Pending)
            .OrderBy(j => j.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (pendingJob is null)
            return;

        var universe = await db.StockUniverses.FindAsync(new object[] { pendingJob.UniverseId }, ct);
        if (universe is null)
        {
            pendingJob.Status = BackfillStatus.Failed;
            pendingJob.ErrorLog = JsonSerializer.Serialize(new[] { new { symbol = "*", error = "Universe not found" } });
            pendingJob.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return;
        }

        var symbols = universe.GetSymbolList();
        if (universe.IncludesBenchmark && !symbols.Contains("SPY", StringComparer.OrdinalIgnoreCase))
            symbols.Add("SPY");

        pendingJob.Status = BackfillStatus.Running;
        pendingJob.StartedAt = DateTime.UtcNow;
        pendingJob.TotalSymbols = symbols.Count;
        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Starting backfill job {JobId}: {Count} symbols, {Years}y back, incremental={Incr}",
            pendingJob.Id, symbols.Count, pendingJob.YearsBack, pendingJob.IsIncremental);

        var provider = scope.ServiceProvider.GetRequiredService<IMarketDataProvider>();
        var ingestLogger = scope.ServiceProvider.GetRequiredService<ILogger<IngestMarketDataHandler>>();
        var errors = new List<object>();

        foreach (var symbol in symbols)
        {
            if (ct.IsCancellationRequested)
                break;

            try
            {
                if (pendingJob.IsIncremental)
                {
                    await IngestIncremental(db, provider, ingestLogger, symbol, ct);
                }
                else
                {
                    await IngestMarketDataHandler.HandleAsync(
                        new IngestMarketDataCommand(symbol, pendingJob.YearsBack),
                        provider, db, ingestLogger);
                }

                pendingJob.CompletedSymbols++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Backfill failed for symbol {Symbol}", symbol);
                pendingJob.FailedSymbols++;
                errors.Add(new { symbol, error = ex.Message });
            }

            pendingJob.ErrorLog = JsonSerializer.Serialize(errors);
            await db.SaveChangesAsync(ct);

            // Rate limit: 500ms between Yahoo Finance requests
            await Task.Delay(RateLimitDelay, ct);
        }

        pendingJob.Status = pendingJob.FailedSymbols == pendingJob.TotalSymbols
            ? BackfillStatus.Failed
            : BackfillStatus.Completed;
        pendingJob.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Backfill job {JobId} finished: {Completed}/{Total} succeeded, {Failed} failed",
            pendingJob.Id, pendingJob.CompletedSymbols, pendingJob.TotalSymbols, pendingJob.FailedSymbols);
    }

    private static async Task IngestIncremental(
        MarketDataDbContext db,
        IMarketDataProvider provider,
        ILogger<IngestMarketDataHandler> logger,
        string symbol,
        CancellationToken ct)
    {
        var stock = await db.Stocks.FirstOrDefaultAsync(s => s.Symbol == symbol, ct);
        if (stock is null)
        {
            // No existing data — do a full 1-year fetch
            await IngestMarketDataHandler.HandleAsync(
                new IngestMarketDataCommand(symbol, 1), provider, db, logger);
            return;
        }

        // Find the latest daily candle for this stock
        var latestDate = await db.PriceCandles
            .Where(c => c.StockId == stock.Id && c.Interval == CandleInterval.Daily)
            .MaxAsync(c => (DateTime?)c.Timestamp, ct);

        if (latestDate is null)
        {
            await IngestMarketDataHandler.HandleAsync(
                new IngestMarketDataCommand(symbol, 1), provider, db, logger);
            return;
        }

        // Calculate years back to cover from latest date to now
        var daysSinceLatest = (DateTime.UtcNow.Date - latestDate.Value.Date).TotalDays;
        if (daysSinceLatest <= 1)
            return; // Already up to date

        // Fetch with enough history to cover the gap (minimum 1 year for aggregation)
        var yearsBack = Math.Max(1, (int)Math.Ceiling(daysSinceLatest / 365.0));
        await IngestMarketDataHandler.HandleAsync(
            new IngestMarketDataCommand(symbol, yearsBack), provider, db, logger);
    }
}
