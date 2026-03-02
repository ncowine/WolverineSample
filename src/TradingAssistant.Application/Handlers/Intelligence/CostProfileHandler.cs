using Microsoft.EntityFrameworkCore;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Intelligence;

public class CreateCostProfileHandler
{
    public static async Task<CostProfileDto> HandleAsync(
        CreateCostProfileCommand command,
        IntelligenceDbContext db)
    {
        var code = command.MarketCode.Trim().ToUpperInvariant();

        var profile = new CostProfile
        {
            MarketCode = code,
            Name = command.Name.Trim(),
            CommissionPerShare = command.CommissionPerShare,
            CommissionPercent = command.CommissionPercent,
            ExchangeFeePercent = command.ExchangeFeePercent,
            TaxPercent = command.TaxPercent,
            SpreadEstimatePercent = command.SpreadEstimatePercent
        };

        db.CostProfiles.Add(profile);
        await db.SaveChangesAsync();

        return MapToDto(profile);
    }

    internal static CostProfileDto MapToDto(CostProfile c) =>
        new(c.Id, c.MarketCode, c.Name, c.CommissionPerShare, c.CommissionPercent,
            c.ExchangeFeePercent, c.TaxPercent, c.SpreadEstimatePercent, c.IsActive, c.CreatedAt);
}

public class UpdateCostProfileHandler
{
    public static async Task<CostProfileDto> HandleAsync(
        UpdateCostProfileCommand command,
        IntelligenceDbContext db)
    {
        var profile = await db.CostProfiles.FindAsync(command.ProfileId)
            ?? throw new InvalidOperationException($"Cost profile '{command.ProfileId}' not found.");

        if (command.Name != null) profile.Name = command.Name.Trim();
        if (command.CommissionPerShare.HasValue) profile.CommissionPerShare = command.CommissionPerShare.Value;
        if (command.CommissionPercent.HasValue) profile.CommissionPercent = command.CommissionPercent.Value;
        if (command.ExchangeFeePercent.HasValue) profile.ExchangeFeePercent = command.ExchangeFeePercent.Value;
        if (command.TaxPercent.HasValue) profile.TaxPercent = command.TaxPercent.Value;
        if (command.SpreadEstimatePercent.HasValue) profile.SpreadEstimatePercent = command.SpreadEstimatePercent.Value;
        if (command.IsActive.HasValue) profile.IsActive = command.IsActive.Value;

        profile.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return CreateCostProfileHandler.MapToDto(profile);
    }
}

public class GetCostProfilesHandler
{
    public static async Task<List<CostProfileDto>> HandleAsync(
        GetCostProfilesQuery query,
        IntelligenceDbContext db)
    {
        var q = db.CostProfiles.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.MarketCode))
            q = q.Where(c => c.MarketCode == query.MarketCode);

        var profiles = await q.OrderBy(c => c.MarketCode).ThenBy(c => c.Name).ToListAsync();

        return profiles.Select(CreateCostProfileHandler.MapToDto).ToList();
    }
}
