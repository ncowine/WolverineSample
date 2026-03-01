using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
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

        group.MapGet("/portfolio/{accountId}", GetPortfolio)
            .WithSummary("Get portfolio summary for an account");

        group.MapGet("/orders/{accountId}", GetOrderHistory)
            .WithSummary("Get paginated order history for an account");

        group.MapGet("/positions/{accountId}", GetPositions)
            .WithSummary("Get positions for an account, optionally filtered by status");
    }

    private static async Task<OrderDto> PlaceOrder(PlaceOrderCommand command, IMessageBus bus)
    {
        await bus.InvokeAsync(command);

        return new OrderDto(
            Guid.Empty, command.AccountId, command.Symbol,
            command.Side, command.Type, command.Quantity, command.Price,
            "Pending", DateTime.UtcNow, null);
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
}
