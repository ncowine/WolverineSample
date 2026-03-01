using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Infrastructure.Persistence;
using TradingAssistant.SharedKernel;
using Wolverine;
using Wolverine.Http;

namespace TradingAssistant.Api.Endpoints;

public class TradingEndpoints : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/trading")
            .WithTags("Trading")
            .RequireAuthorization();

        // PlaceOrder has custom response construction, needs manual handler
        group.MapPost("/orders", PlaceOrder)
            .WithSummary("Place a new order");

        group.MapPostToWolverine<CancelOrderCommand, string>("/orders/cancel")
            .WithSummary("Cancel a pending order");

        group.MapPostToWolverine<ClosePositionCommand, string>("/positions/close")
            .WithSummary("Close an open position");

        group.MapPostToWolverine<CreatePaperAccountCommand, AccountDto>("/accounts/paper")
            .WithSummary("Create a paper trading account");

        group.MapGet("/portfolio/{accountId}", GetPortfolio)
            .WithSummary("Get portfolio summary for an account");

        group.MapGet("/orders/{accountId}", GetOrderHistory)
            .WithSummary("Get paginated order history for an account");

        group.MapGet("/positions/{accountId}", GetPositions)
            .WithSummary("Get positions for an account, optionally filtered by status");

        group.MapPostToWolverine<CreateDcaPlanCommand, DcaPlanDto>("/dca-plans")
            .WithSummary("Create a Dollar-Cost Averaging plan");

        group.MapGet("/dca-plans/{accountId}", GetDcaPlans)
            .WithSummary("List DCA plans for an account");

        group.MapPost("/dca-plans/{planId}/pause", PauseDcaPlan)
            .WithSummary("Pause an active DCA plan");

        group.MapPost("/dca-plans/{planId}/resume", ResumeDcaPlan)
            .WithSummary("Resume a paused DCA plan");

        group.MapDelete("/dca-plans/{planId}", CancelDcaPlan)
            .WithSummary("Cancel a DCA plan permanently");

        group.MapGet("/dca-plans/{planId}/executions", GetDcaExecutions)
            .WithSummary("List execution history for a DCA plan");
    }

    private static async Task<OrderDto> PlaceOrder(PlaceOrderCommand command, IMessageBus bus, TradingDbContext db)
    {
        var account = await db.Accounts.FindAsync(command.AccountId);
        await bus.InvokeAsync(command);

        return new OrderDto(
            Guid.Empty, command.AccountId, command.Symbol,
            command.Side, command.Type, command.Quantity, command.Price,
            "Pending", DateTime.UtcNow, null,
            account?.AccountType.ToString() ?? "Live");
    }

    private static async Task<PortfolioDto> GetPortfolio(Guid accountId, IMessageBus bus)
    {
        return await bus.InvokeAsync<PortfolioDto>(new GetPortfolioQuery(accountId));
    }

    private static async Task<PagedResponse<OrderDto>> GetOrderHistory(
        Guid accountId,
        int page,
        int pageSize,
        IMessageBus bus)
    {
        return await bus.InvokeAsync<PagedResponse<OrderDto>>(
            new GetOrderHistoryQuery(accountId, page > 0 ? page : 1, pageSize > 0 ? pageSize : 20));
    }

    private static async Task<List<PositionDto>> GetPositions(
        Guid accountId,
        string? status,
        IMessageBus bus)
    {
        return await bus.InvokeAsync<List<PositionDto>>(new GetPositionsQuery(accountId, status));
    }

    private static async Task<List<DcaPlanDto>> GetDcaPlans(Guid accountId, IMessageBus bus)
    {
        return await bus.InvokeAsync<List<DcaPlanDto>>(new GetDcaPlansQuery(accountId));
    }

    private static async Task<DcaPlanDto> PauseDcaPlan(Guid planId, IMessageBus bus)
    {
        return await bus.InvokeAsync<DcaPlanDto>(new PauseDcaPlanCommand(planId));
    }

    private static async Task<DcaPlanDto> ResumeDcaPlan(Guid planId, IMessageBus bus)
    {
        return await bus.InvokeAsync<DcaPlanDto>(new ResumeDcaPlanCommand(planId));
    }

    private static async Task<string> CancelDcaPlan(Guid planId, IMessageBus bus)
    {
        return await bus.InvokeAsync<string>(new CancelDcaPlanCommand(planId));
    }

    private static async Task<PagedResponse<DcaExecutionDto>> GetDcaExecutions(
        Guid planId, int? page, int? pageSize, IMessageBus bus)
    {
        return await bus.InvokeAsync<PagedResponse<DcaExecutionDto>>(
            new GetDcaExecutionsQuery(planId, page ?? 1, pageSize ?? 20));
    }
}
