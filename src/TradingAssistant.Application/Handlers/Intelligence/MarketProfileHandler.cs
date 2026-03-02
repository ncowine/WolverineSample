using Microsoft.EntityFrameworkCore;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Intelligence;

public class CreateMarketProfileHandler
{
    public static async Task<MarketProfileDto> HandleAsync(
        CreateMarketProfileCommand command,
        IntelligenceDbContext db)
    {
        var code = command.MarketCode.Trim().ToUpperInvariant();

        var exists = await db.MarketProfiles.AnyAsync(p => p.MarketCode == code);
        if (exists)
            throw new InvalidOperationException($"A market profile for '{code}' already exists.");

        var profile = new MarketProfile
        {
            MarketCode = code,
            Exchange = command.Exchange.Trim(),
            Currency = command.Currency.Trim().ToUpperInvariant(),
            Timezone = command.Timezone.Trim(),
            VixSymbol = command.VixSymbol.Trim(),
            DataSource = command.DataSource.Trim().ToLowerInvariant(),
            ConfigJson = command.ConfigJson
        };

        db.MarketProfiles.Add(profile);
        await db.SaveChangesAsync();

        return MapToDto(profile);
    }

    internal static MarketProfileDto MapToDto(MarketProfile p) =>
        new(p.Id, p.MarketCode, p.Exchange, p.Currency, p.Timezone,
            p.VixSymbol, p.DataSource, p.IsActive, p.ConfigJson,
            p.DnaProfileJson, p.DnaProfileUpdatedAt, p.CreatedAt);
}

public class UpdateMarketProfileHandler
{
    public static async Task<MarketProfileDto> HandleAsync(
        UpdateMarketProfileCommand command,
        IntelligenceDbContext db)
    {
        var profile = await db.MarketProfiles.FindAsync(command.ProfileId)
            ?? throw new InvalidOperationException($"Market profile '{command.ProfileId}' not found.");

        if (command.Exchange != null) profile.Exchange = command.Exchange.Trim();
        if (command.Currency != null) profile.Currency = command.Currency.Trim().ToUpperInvariant();
        if (command.Timezone != null) profile.Timezone = command.Timezone.Trim();
        if (command.VixSymbol != null) profile.VixSymbol = command.VixSymbol.Trim();
        if (command.DataSource != null) profile.DataSource = command.DataSource.Trim().ToLowerInvariant();
        if (command.ConfigJson != null) profile.ConfigJson = command.ConfigJson;
        if (command.IsActive.HasValue) profile.IsActive = command.IsActive.Value;

        profile.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return CreateMarketProfileHandler.MapToDto(profile);
    }
}

public class GetMarketProfilesHandler
{
    public static async Task<List<MarketProfileDto>> HandleAsync(
        GetMarketProfilesQuery query,
        IntelligenceDbContext db)
    {
        var profiles = await db.MarketProfiles
            .OrderBy(p => p.MarketCode)
            .ToListAsync();

        return profiles.Select(CreateMarketProfileHandler.MapToDto).ToList();
    }
}

public class GetMarketProfileHandler
{
    public static async Task<MarketProfileDto> HandleAsync(
        GetMarketProfileQuery query,
        IntelligenceDbContext db)
    {
        var profile = await db.MarketProfiles
            .FirstOrDefaultAsync(p => p.MarketCode == query.MarketCode)
            ?? throw new InvalidOperationException($"Market profile '{query.MarketCode}' not found.");

        return CreateMarketProfileHandler.MapToDto(profile);
    }
}
