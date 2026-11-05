using Api.Requests;
using Application.Orders;
using Application.Responses;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Api.Endpoints;

public static class OrderEndpoints
{
    public static WebApplication MapOrderEndpoints(this WebApplication app)
    {
        var orders = app.MapGroup("/orders").WithTags("Orders");

        orders.MapGet("/{id:guid}", GetOrderById).WithName(nameof(GetOrderById));
        orders.MapGet("/", GetOrders).WithName(nameof(GetOrders))
            .ProducesValidationProblem();
        orders.MapGet("/{id:guid}/history", GetOrderHistory).WithName(nameof(GetOrderHistory));
        orders.MapGet("/{id:guid}/summary", SummarizeOrder).WithName(nameof(SummarizeOrder))
            .ProducesProblem(StatusCodes.Status404NotFound);
        orders.MapPost("/", CreateOrder).WithName(nameof(CreateOrder))
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
        orders.MapPut("/{id:guid}", UpdateOrder).WithName(nameof(UpdateOrder))
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
        orders.MapPost("/{id:guid}/confirm", ConfirmOrder).WithName(nameof(ConfirmOrder))
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        orders.MapPost("/{id:guid}/ship", ShipOrder).WithName(nameof(ShipOrder))
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
        orders.MapPost("/{id:guid}/cancel", CancelOrder).WithName(nameof(CancelOrder))
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
        orders.MapDelete("/{id:guid}", DeleteOrder).WithName(nameof(DeleteOrder))
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    /// <summary>Get an order</summary>
    /// <remarks>The order with its lines.</remarks>
    /// <response code="200">The order.</response>
    /// <response code="404">No order has the given id.</response>
    internal static async Task<Results<Ok<OrderWithLinesResponse>, NotFound>> GetOrderById(
        Guid id, IMediator mediator, CancellationToken ct)
    {
        var order = await mediator.Send(new GetOrderByIdQuery(id), ct);
        return order != null ? TypedResults.Ok(order) : TypedResults.NotFound();
    }

    /// <summary>List orders</summary>
    /// <remarks>One page of orders, without lines.</remarks>
    /// <param name="page">Page number, from 1. Default 1.</param>
    /// <param name="pageSize">Orders per page, from 1 to 100. Default 10.</param>
    /// <response code="200">The orders of the page; empty after the last page.</response>
    /// <response code="400">Page lower than 1, or page size not between 1 and 100.</response>
    internal static async Task<Ok<IReadOnlyList<OrderResponse>>> GetOrders(
        IMediator mediator, CancellationToken ct, int page = 1, int pageSize = 10)
    {
        return TypedResults.Ok(await mediator.Send(new GetOrdersListQuery(page, pageSize), ct));
    }

    /// <summary>Get the history of an order</summary>
    /// <remarks>Confirmation and cancellation entries, oldest first; the <c>type</c> property tells them apart.</remarks>
    /// <response code="200">The entries; empty if the order has no history or does not exist.</response>
    internal static async Task<Ok<IReadOnlyList<OrderHistoryEntry>>> GetOrderHistory(
        Guid id, IMediator mediator, CancellationToken ct)
    {
        return TypedResults.Ok(await mediator.Send(new GetOrderHistoryQuery(id), ct));
    }

    /// <summary>Summarize an order</summary>
    /// <remarks>A short text summary of the order and its history, written by an LLM.</remarks>
    /// <response code="200">The summary.</response>
    /// <response code="404">The order does not exist.</response>
    internal static async Task<Ok<string>> SummarizeOrder(Guid id, IMediator mediator, CancellationToken ct)
    {
        return TypedResults.Ok(await mediator.Send(new SummarizeOrderQuery(id), ct));
    }

    /// <summary>Create an order</summary>
    /// <remarks>A draft order with the given lines.</remarks>
    /// <response code="201">The order created; the Location header is its URL.</response>
    /// <response code="400">A line is not valid (quantity lower than 1).</response>
    /// <response code="404">A product does not exist.</response>
    internal static async Task<CreatedAtRoute<OrderWithLinesResponse>> CreateOrder(
        CreateOrderCommand cmd, IMediator mediator, CancellationToken ct)
    {
        var order = await mediator.Send(cmd, ct);
        return TypedResults.CreatedAtRoute(order, nameof(GetOrderById), new { id = order.Id });
    }

    /// <summary>Update the lines of an order</summary>
    /// <remarks>Adds, updates and deletes lines in one request; only draft orders can be edited.</remarks>
    /// <response code="200">The order updated.</response>
    /// <response code="400">The order is not a draft, or a line is not valid (a line to delete that does not exist included).</response>
    /// <response code="404">The order, a product or a line to update does not exist.</response>
    internal static async Task<Ok<OrderWithLinesResponse>> UpdateOrder(
        Guid id, UpdateOrderRequest req, IMediator mediator, CancellationToken ct)
    {
        var order = await mediator.Send(new UpdateOrderCommand(id, req.LinesToAdd, req.LinesToUpdate, req.LineIdsToDelete), ct);
        return TypedResults.Ok(order);
    }

    /// <summary>Confirm an order</summary>
    /// <remarks>Optionally redeems a voucher, whose amount is subtracted from the order.</remarks>
    /// <param name="voucherId">Voucher to redeem, optional.</param>
    /// <response code="200">The order confirmed.</response>
    /// <response code="400">The order cannot be confirmed (no lines, wrong status) or the voucher cannot be used (not valid at this date, canceled, already redeemed, amount over the subtotal).</response>
    /// <response code="404">The order, the voucher or a product on its lines does not exist.</response>
    /// <response code="409">Another order redeemed the same voucher at the same time.</response>
    internal static async Task<Ok<OrderWithLinesResponse>> ConfirmOrder(
        Guid id, Guid? voucherId, IMediator mediator, CancellationToken ct)
    {
        return TypedResults.Ok(await mediator.Send(new ConfirmOrderCommand(id, voucherId), ct));
    }

    /// <summary>Ship an order</summary>
    /// <response code="200">The order shipped.</response>
    /// <response code="400">The order has no lines or is not in a status that can be shipped.</response>
    /// <response code="404">The order does not exist.</response>
    internal static async Task<Ok<OrderWithLinesResponse>> ShipOrder(Guid id, IMediator mediator, CancellationToken ct)
    {
        return TypedResults.Ok(await mediator.Send(new ShipOrderCommand(id), ct));
    }

    /// <summary>Cancel an order</summary>
    /// <response code="200">The order canceled.</response>
    /// <response code="400">The order is not in a status that can be canceled.</response>
    /// <response code="404">The order does not exist.</response>
    internal static async Task<Ok<OrderWithLinesResponse>> CancelOrder(Guid id, IMediator mediator, CancellationToken ct)
    {
        return TypedResults.Ok(await mediator.Send(new CancelOrderCommand(id), ct));
    }

    /// <summary>Delete an order</summary>
    /// <response code="204">The order deleted.</response>
    /// <response code="404">The order does not exist.</response>
    internal static async Task<NoContent> DeleteOrder(Guid id, IMediator mediator, CancellationToken ct)
    {
        await mediator.Send(new DeleteOrderCommand(id), ct);
        return TypedResults.NoContent();
    }
}
