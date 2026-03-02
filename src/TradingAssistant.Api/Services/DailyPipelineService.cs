using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application.Intelligence;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Api.Services;

/// <summary>
/// Background service that orchestrates the daily end-of-day pipeline.
///
/// Polls every 60 seconds. For each active MarketProfile, checks if the
/// current UTC time has passed the market's configured trigger time and
/// whether the pipeline has already run today. If due, executes the
/// 10-step pipeline via DailyPipelineOrchestrator.
///
/// Trigger times are read from MarketProfile.ConfigJson:
///   {"pipelineTriggerUtcHour": 21}   (default: 21 for US, 11 for India)
/// </summary>
public class DailyPipelineService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DailyPipelineService> _logger;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);

    public DailyPipelineService(
        IServiceScopeFactory scopeFactory,
        ILogger<DailyPipelineService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Daily pipeline service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndRunDueMarkets(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in daily pipeline service");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task CheckAndRunDueMarkets(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var intelligenceDb = scope.ServiceProvider.GetRequiredService<IntelligenceDbContext>();

        var profiles = await intelligenceDb.MarketProfiles
            .Where(p => p.IsActive)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var today = now.Date;

        foreach (var profile in profiles)
        {
            var triggerHour = DailyPipelineOrchestrator.GetTriggerHourUtc(profile.ConfigJson);

            // Not yet trigger time
            if (now.Hour < triggerHour)
                continue;

            // Check if already ran today
            var alreadyRan = await intelligenceDb.PipelineRunLogs
                .AnyAsync(l => l.MarketCode == profile.MarketCode
                    && l.RunDate.Date == today, ct);

            if (alreadyRan)
                continue;

            _logger.LogInformation(
                "Triggering daily pipeline for {Market} (trigger hour: {Hour} UTC)",
                profile.MarketCode, triggerHour);

            var context = new PipelineContext
            {
                MarketCode = profile.MarketCode,
                RunDate = now
            };

            // Build steps — most are stubs until real implementations are wired
            var steps = DailyPipelineOrchestrator.BuildDefaultSteps();

            await DailyPipelineOrchestrator.ExecuteAsync(
                context, steps, intelligenceDb, _logger, ct: ct);

            _logger.LogInformation(
                "Daily pipeline completed for {Market}", profile.MarketCode);
        }
    }

}
