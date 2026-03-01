using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application.Handlers.Trading;
using TradingAssistant.Infrastructure.Caching;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Api.Services;

public class DcaPlanExecutionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly StockPriceCache _priceCache;
    private readonly ILogger<DcaPlanExecutionService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    public DcaPlanExecutionService(
        IServiceScopeFactory scopeFactory,
        StockPriceCache priceCache,
        ILogger<DcaPlanExecutionService> logger)
    {
        _scopeFactory = scopeFactory;
        _priceCache = priceCache;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DCA plan execution service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDuePlans(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error processing DCA plans");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task ProcessDuePlans(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TradingDbContext>();

        var duePlans = await db.DcaPlans
            .Include(p => p.Account)
            .Where(p => p.IsActive && p.NextExecutionDate <= DateTime.UtcNow)
            .ToListAsync(ct);

        if (duePlans.Count == 0)
            return;

        _logger.LogInformation("Found {Count} DCA plans due for execution", duePlans.Count);

        foreach (var plan in duePlans)
        {
            try
            {
                var execution = await ExecuteDcaPlanHandler.ExecuteAsync(plan, db, _priceCache, _logger);
                _logger.LogInformation(
                    "DCA plan {PlanId} ({Symbol}) execution result: {Status}",
                    plan.Id, plan.Symbol, execution.Status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute DCA plan {PlanId}", plan.Id);
            }
        }
    }
}
