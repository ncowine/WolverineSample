using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application.Services;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Domain.Identity;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Identity;

public class GetUserSettingsHandler
{
    public static async Task<UserSettingsDto> HandleAsync(
        GetUserSettingsQuery query,
        TradingDbContext db,
        ICurrentUser currentUser)
    {
        var settings = await db.UserSettings
            .FirstOrDefaultAsync(s => s.UserId == currentUser.UserId);

        if (settings is null)
        {
            settings = new UserSettings { UserId = currentUser.UserId };
            db.UserSettings.Add(settings);
            await db.SaveChangesAsync();
        }

        return new UserSettingsDto(
            settings.DefaultCurrency,
            settings.DefaultInitialCapital,
            settings.CostProfileMarket,
            settings.BrokerSettingsJson);
    }
}
