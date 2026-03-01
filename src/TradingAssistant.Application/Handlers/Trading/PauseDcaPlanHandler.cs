using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application.Exceptions;
using TradingAssistant.Application.Services;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Trading;

public class PauseDcaPlanHandler
{
    public static async Task<DcaPlanDto> HandleAsync(
        PauseDcaPlanCommand command,
        TradingDbContext db,
        ICurrentUser currentUser)
    {
        var plan = await db.DcaPlans
            .Include(p => p.Account)
            .FirstOrDefaultAsync(p => p.Id == command.PlanId)
            ?? throw new InvalidOperationException($"DCA plan '{command.PlanId}' not found.");

        if (plan.Account.UserId != currentUser.UserId)
            throw new ForbiddenAccessException("You do not have access to this DCA plan.");

        if (!plan.IsActive)
            throw new InvalidOperationException("DCA plan is already paused.");

        plan.IsActive = false;
        await db.SaveChangesAsync();

        return new DcaPlanDto(
            plan.Id, plan.AccountId, plan.Symbol, plan.Amount,
            plan.Frequency.ToString(), plan.NextExecutionDate,
            plan.IsActive, plan.CreatedAt);
    }
}
