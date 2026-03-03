using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application.Handlers.Intelligence;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Infrastructure.Persistence;
using Wolverine;

namespace TradingAssistant.Api.Services;

/// <summary>
/// Background service that triggers ML model retraining when criteria are met.
///
/// Polls every hour. For each active market, checks:
/// 1. Has it been 30+ days since the last model was trained?
/// 2. Are there 50+ new closed trades (labeled FeatureSnapshots) since the last model?
///
/// If either condition is met, triggers retraining via IMessageBus.
/// </summary>
public class MlRetrainService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MlRetrainService> _logger;

    internal static readonly TimeSpan PollInterval = TimeSpan.FromHours(1);

    public MlRetrainService(
        IServiceScopeFactory scopeFactory,
        ILogger<MlRetrainService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ML retrain service started (poll interval: {Interval})", PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndRetrain(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in ML retrain service");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    internal async Task CheckAndRetrain(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var intelDb = scope.ServiceProvider.GetRequiredService<IntelligenceDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var markets = await intelDb.MarketProfiles
            .Where(p => p.IsActive)
            .Select(p => p.MarketCode)
            .ToListAsync(ct);

        foreach (var marketCode in markets)
        {
            var (trigger, reason) = await MlRetrainChecker.ShouldRetrain(intelDb, marketCode, ct);
            if (!trigger)
                continue;

            _logger.LogInformation(
                "Triggering ML retrain for {Market}: {Reason}",
                marketCode, reason);

            try
            {
                await bus.InvokeAsync<RetrainResultDto>(
                    new RetrainModelCommand(marketCode), ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "ML retrain failed for {Market}", marketCode);
            }
        }
    }
}
