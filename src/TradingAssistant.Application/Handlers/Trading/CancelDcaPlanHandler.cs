using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application.Exceptions;
using TradingAssistant.Application.Services;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Trading;

public class CancelDcaPlanHandler
{
    public static async Task<string> HandleAsync(
        CancelDcaPlanCommand command,
        TradingDbContext db,
        ICurrentUser currentUser)
    {
        var plan = await db.DcaPlans
            .Include(p => p.Account)
            .FirstOrDefaultAsync(p => p.Id == command.PlanId)
            ?? throw new InvalidOperationException($"DCA plan '{command.PlanId}' not found.");

        if (plan.Account.UserId != currentUser.UserId)
            throw new ForbiddenAccessException("You do not have access to this DCA plan.");

        db.DcaPlans.Remove(plan);
        await db.SaveChangesAsync();

        return "DCA plan cancelled successfully.";
    }
}
