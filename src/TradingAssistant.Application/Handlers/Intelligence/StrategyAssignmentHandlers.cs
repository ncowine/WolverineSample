using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application.Intelligence;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Intelligence;

public class LockStrategyHandler
{
    public static async Task<StrategyAssignmentDto> HandleAsync(
        LockStrategyCommand command,
        IntelligenceDbContext db)
    {
        var assignment = await db.StrategyAssignments
            .FirstOrDefaultAsync(a => a.MarketCode == command.MarketCode);

        if (assignment is null)
        {
            assignment = new StrategyAssignment
            {
                MarketCode = command.MarketCode,
                StrategyId = command.StrategyId,
                StrategyName = $"Strategy-{command.StrategyId.ToString("N")[..8]}",
                Regime = Domain.Intelligence.Enums.RegimeType.Sideways,
                AllocationPercent = StrategySelector.FullAllocation,
                IsLocked = true,
                SwitchoverStartDate = DateTime.UtcNow,
                AssignedAt = DateTime.UtcNow
            };
            db.StrategyAssignments.Add(assignment);
        }
        else
        {
            assignment.StrategyId = command.StrategyId;
            assignment.StrategyName = $"Strategy-{command.StrategyId.ToString("N")[..8]}";
            assignment.IsLocked = true;
            assignment.AllocationPercent = StrategySelector.FullAllocation;
            assignment.AssignedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        return StrategyAssignmentMapper.MapToDto(assignment);
    }
}

public class UnlockStrategyHandler
{
    public static async Task<StrategyAssignmentDto?> HandleAsync(
        UnlockStrategyCommand command,
        IntelligenceDbContext db)
    {
        var assignment = await db.StrategyAssignments
            .FirstOrDefaultAsync(a => a.MarketCode == command.MarketCode);

        if (assignment is null) return null;

        assignment.IsLocked = false;
        assignment.AssignedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return StrategyAssignmentMapper.MapToDto(assignment);
    }
}

public static class StrategyAssignmentMapper
{
    public static StrategyAssignmentDto MapToDto(StrategyAssignment a) =>
        new(
            Id: a.Id,
            MarketCode: a.MarketCode,
            StrategyId: a.StrategyId,
            StrategyName: a.StrategyName,
            Regime: a.Regime.ToString(),
            AllocationPercent: a.IsLocked
                ? a.AllocationPercent
                : StrategySelector.ComputeAllocation(a.SwitchoverStartDate, DateTime.UtcNow),
            IsLocked: a.IsLocked,
            SwitchoverStartDate: a.SwitchoverStartDate,
            AssignedAt: a.AssignedAt);
}
