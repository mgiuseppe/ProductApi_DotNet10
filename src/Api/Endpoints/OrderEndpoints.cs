using Application;
using Application.Commands;
using Application.Exceptions;
using Application.Responses;
using Domain.Exceptions;

namespace Api.Endpoints;

public static class OrderEndpoints
{
    public static WebApplication MapOrderEndpoints(this WebApplication app)
    {
        app.MapGet("/orders/{id:guid}", async (Guid id, IOrderService service, CancellationToken ct) =>
        {
            var o = await service.GetByIdAsync(id, ct);
            return o != null ? Results.Ok(OrderWithLinesResponse.FromDomain(o)) : Results.NotFound();
        });

        app.MapGet("/orders", async (int page, int pageSize, IOrderService service, CancellationToken ct) =>
        {
            if (page <= 0 || pageSize <= 0)
                return Results.BadRequest($"Page and PageSize should be greater than zero - Page: {page} - PageSize: {pageSize} ");

            var ps = await service.GetListAsync(page, pageSize, ct);
            return Results.Ok(ps.Select(p => OrderResponse.FromDomain(p)));
        });

        app.MapPost("/orders", async (CreateOrderCommand cmd, IOrderService service, CancellationToken ct) =>
        {
            try
            {
                var o = await service.CreateAsync(cmd, ct);
                return Results.Created($"/orders/{o.Id}", OrderWithLinesResponse.FromDomain(o));
            }
            catch (DomainException e)
            {
                return Results.BadRequest(e.Message);
            }
        });

        app.MapPut("/orders/{id:guid}", async (Guid id, UpdateOrderCommand cmd, IOrderService service, CancellationToken ct) =>
        {
            try
            {
                var o = await service.UpdateAsync(id, cmd, ct);
                return Results.Ok(OrderResponse.FromDomain(o));
            }
            catch (NotFoundException e)
            {
                // TODO: log
                return Results.NotFound(e.Message);
            }
            catch (DomainException e)
            {
                return Results.BadRequest(e.Message);
            }
        });

        app.MapPost("/orders/{id:guid}/confirm", async (Guid id, IOrderService service, CancellationToken ct) =>
        {
            try
            {
                var o = await service.ConfirmAsync(id, ct);
                return Results.Ok(OrderResponse.FromDomain(o));
            }
            catch (NotFoundException e)
            {
                // TODO: log
                return Results.NotFound(e.Message);
            }
            catch (DomainException e)
            {
                return Results.BadRequest(e.Message);
            }
        });
        app.MapPost("/orders/{id:guid}/ship", async (Guid id, IOrderService service, CancellationToken ct) =>
        {
            try
            {
                var o = await service.ShipAsync(id, ct);
                return Results.Ok(OrderResponse.FromDomain(o));
            }
            catch (NotFoundException e)
            {
                // TODO: log
                return Results.NotFound(e.Message);
            }
            catch (DomainException e)
            {
                return Results.BadRequest(e.Message);
            }
        });
        app.MapPost("/orders/{id:guid}/cancel", async (Guid id, IOrderService service, CancellationToken ct) =>
        {
            try
            {
                var o = await service.CancelAsync(id, ct);
                return Results.Ok(OrderResponse.FromDomain(o));
            }
            catch (NotFoundException e)
            {
                // TODO: log
                return Results.NotFound(e.Message);
            }
            catch (DomainException e)
            {
                return Results.BadRequest(e.Message);
            }
        });

        app.MapDelete("/orders/{id:guid}", async (Guid id, IOrderService service, CancellationToken ct) =>
        {
            try
            {
                await service.DeleteAsync(id, ct);
                return Results.NoContent();
            }
            catch (NotFoundException e)
            {
                // TODO: log
                return Results.NotFound(e.Message);
            }
        });

        return app;
    }
}
