using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application.Handlers.Screening;
using TradingAssistant.Application.Screening;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Api.Services;

/// <summary>
/// Runs the screener automatically after market close.
/// Polling-based (same pattern as DcaPlanExecutionService) since
/// Wolverine ScheduleAsync requires Marten/PostgreSQL transport.
/// </summary>
public class ScreenerSchedulingService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScreenerSchedulingService> _logger;

    /// <summary>
    /// Check every 5 minutes if it's time to run.
    /// </summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Default scan time: 5 PM Eastern (21:00 UTC during EST, 22:00 during EDT).
    /// </summary>
    private static readonly TimeOnly TargetTimeUtc = new(21, 0);

    public ScreenerSchedulingService(
        IServiceScopeFactory scopeFactory,
        ILogger<ScreenerSchedulingService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Screener scheduling service started (target: {TargetTime} UTC daily)", TargetTimeUtc);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndRunIfDue(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in screener scheduling service");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task CheckAndRunIfDue(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var currentTime = TimeOnly.FromDateTime(now);
        var today = DateOnly.FromDateTime(now);

        // Only run if we're past the target time
        if (currentTime < TargetTimeUtc)
            return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketDataDbContext>();

        // Check if we already ran today
        var alreadyRanToday = await db.ScreenerRuns
            .AnyAsync(r => r.ScanDate.Date == now.Date, ct);

        if (alreadyRanToday)
            return;

        _logger.LogInformation("Running scheduled screener scan for {Date}", today);

        // In a full implementation, this would:
        // 1. Load the active universe symbols
        // 2. Load the latest optimized strategy parameters
        // 3. Fetch/compute indicators for each symbol
        // 4. Call ScreenerEngine.Scan()
        // 5. Persist via RunScreenerHandler
        //
        // For now, create a placeholder run to mark today as scanned.
        // The actual scan logic will be connected in STORY-039 (param pipeline).

        var placeholderResult = new ScreenerRunResult
        {
            ScanDate = now,
            StrategyName = "Scheduled",
            SymbolsScanned = 0,
            SignalsFound = 0,
            Results = new(),
            Warnings = new List<string> { "Scheduled scan: no strategy parameters configured yet" },
            ElapsedTime = TimeSpan.Zero
        };

        await RunScreenerHandler.HandleAsync(placeholderResult, db);
        _logger.LogInformation("Scheduled screener scan completed for {Date}", today);
    }
}
