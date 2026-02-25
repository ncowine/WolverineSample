using Microsoft.AspNetCore.Authorization;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.SharedKernel;
using Wolverine;
using Wolverine.Http;

namespace TradingAssistant.Api.Endpoints;

public static class TradingEndpoints
{
    [Authorize]
    [WolverinePost("/api/trading/orders")]
    public static async Task<OrderDto> PlaceOrder(PlaceOrderCommand command, IMessageBus bus)
    {
        // InvokeAsync returns the first non-event return type
        // The handler returns (OrderPlaced event, OrderDto_Internal) as a tuple
        // The OrderPlaced event cascades, and we need to query the order after
        await bus.InvokeAsync(command);

        // Return a simple confirmation
        return new OrderDto(
            Guid.Empty, command.AccountId, command.Symbol,
            command.Side, command.Type, command.Quantity, command.Price,
            "Pending", DateTime.UtcNow, null);
    }

    [Authorize]
    [WolverinePost("/api/trading/orders/cancel")]
    public static async Task<string> CancelOrder(CancelOrderCommand command, IMessageBus bus)
    {
        await bus.InvokeAsync(command);
        return "Order cancelled successfully.";
    }

    [Authorize]
    [WolverinePost("/api/trading/positions/close")]
    public static async Task<string> ClosePosition(ClosePositionCommand command, IMessageBus bus)
    {
        await bus.InvokeAsync(command);
        return "Position closed successfully.";
    }

    [Authorize]
    [WolverineGet("/api/trading/portfolio/{accountId}")]
    public static async Task<PortfolioDto> GetPortfolio(Guid accountId, IMessageBus bus)
    {
        return await bus.InvokeAsync<PortfolioDto>(new GetPortfolioQuery(accountId));
    }

    [Authorize]
    [WolverineGet("/api/trading/orders/{accountId}")]
    public static async Task<PagedResponse<OrderDto>> GetOrderHistory(
        Guid accountId,
        int page,
        int pageSize,
        IMessageBus bus)
    {
        return await bus.InvokeAsync<PagedResponse<OrderDto>>(
            new GetOrderHistoryQuery(accountId, page > 0 ? page : 1, pageSize > 0 ? pageSize : 20));
    }

    [Authorize]
    [WolverineGet("/api/trading/positions/{accountId}")]
    public static async Task<List<PositionDto>> GetPositions(
        Guid accountId,
        string? status,
        IMessageBus bus)
    {
        return await bus.InvokeAsync<List<PositionDto>>(new GetPositionsQuery(accountId, status));
    }
}
