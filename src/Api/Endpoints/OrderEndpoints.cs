using Api.Requests;
using Application;
using Application.Exceptions;
using Application.Orders;
using Application.Responses;
using Domain.Exceptions;
using MediatR;

namespace Api.Endpoints;

public static class OrderEndpoints
{
    public static WebApplication MapOrderEndpoints(this WebApplication app)
    {
        app.MapGet("/orders/{id:guid}", async (Guid id, IMediator mediator, CancellationToken ct) =>
        {
            var o = await mediator.Send(new GetOrderByIdQuery(id), ct);
            return o != null ? Results.Ok(o) : Results.NotFound();
        });

        app.MapGet("/orders", async (int page, int pageSize, IMediator mediator, CancellationToken ct) =>
        {
            if (page <= 0 || pageSize <= 0)
                return Results.BadRequest($"Page and PageSize should be greater than zero - Page: {page} - PageSize: {pageSize} ");

            var ps = await mediator.Send(new GetOrdersListQuery(page, pageSize), ct);
            return Results.Ok(ps);
        });

        app.MapPost("/orders", async (CreateOrderCommand cmd, IMediator mediator, CancellationToken ct) =>
        {
            var o = await mediator.Send(cmd, ct);
            return Results.Created($"/orders/{o.Id}", o);
        });

        app.MapPut("/orders/{id:guid}", async (Guid id, UpdateOrderRequest cmd, IMediator mediator, CancellationToken ct) =>
        {
            var o = await mediator.Send(new UpdateOrderCommand(id, cmd.LinesToAdd, cmd.LinesToUpdate, cmd.LineIdsToDelete), ct);
            return Results.Ok(o);
        });

        app.MapPost("/orders/{id:guid}/confirm", async (Guid id, Guid? voucherId, IMediator mediator, CancellationToken ct) =>
        {
            var o = await mediator.Send(new ConfirmOrderCommand(id, voucherId), ct);
            return Results.Ok(o);
        });
        app.MapPost("/orders/{id:guid}/ship", async (Guid id, IMediator mediator, CancellationToken ct) =>
        {
            var o = await mediator.Send(new ShipOrderCommand(id), ct);
            return Results.Ok(o);
        });
        app.MapPost("/orders/{id:guid}/cancel", async (Guid id, IMediator mediator, CancellationToken ct) =>
        {
            var o = await mediator.Send(new CancelOrderCommand(id), ct);
            return Results.Ok(o);
        });

        app.MapDelete("/orders/{id:guid}", async (Guid id, IMediator mediator, CancellationToken ct) =>
        {
            await mediator.Send(new DeleteOrderCommand(id), ct);
            return Results.NoContent();
        });

        return app;
    }
}
