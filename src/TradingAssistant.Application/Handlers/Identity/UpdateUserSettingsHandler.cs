using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application.Services;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Domain.Identity;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Identity;

public class UpdateUserSettingsHandler
{
    public static async Task<UserSettingsDto> HandleAsync(
        UpdateUserSettingsCommand command,
        TradingDbContext db,
        ICurrentUser currentUser)
    {
        var settings = await db.UserSettings
            .FirstOrDefaultAsync(s => s.UserId == currentUser.UserId);

        if (settings is null)
        {
            settings = new UserSettings { UserId = currentUser.UserId };
            db.UserSettings.Add(settings);
        }

        settings.DefaultCurrency = command.DefaultCurrency;
        settings.DefaultInitialCapital = command.DefaultInitialCapital;
        settings.CostProfileMarket = command.CostProfileMarket;
        settings.BrokerSettingsJson = command.BrokerSettingsJson;
        settings.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return new UserSettingsDto(
            settings.DefaultCurrency,
            settings.DefaultInitialCapital,
            settings.CostProfileMarket,
            settings.BrokerSettingsJson);
    }
}
