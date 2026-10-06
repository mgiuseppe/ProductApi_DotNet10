using Application.Exceptions;
using Application.Vouchers;
using Domain.Exceptions;
using MediatR;

namespace Api.Endpoints;

public static class VoucherEndpoints
{
    public static WebApplication MapVoucherEndpoints(this WebApplication app)
    {
        app.MapGet("/vouchers/{id:guid}", async (Guid id, IMediator mediator, CancellationToken ct) =>
        {
            var v = await mediator.Send(new GetVoucherByIdQuery(id), ct);
            return v != null ? Results.Ok(v) : Results.NotFound();
        });

        app.MapPost("/vouchers", async (CreateVoucherCommand cmd, IMediator mediator, CancellationToken ct) =>
        {
            try
            {
                var v = await mediator.Send(cmd, ct);
                return Results.Created($"/vouchers/{v.Id}", v);
            }
            catch (DomainException e)
            {
                return Results.BadRequest(e.Message);
            }
        });

        app.MapPost("/vouchers/{id:guid}/cancel", async (Guid id, IMediator mediator, CancellationToken ct) =>
        {
            try
            {
                var v = await mediator.Send(new CancelVoucherCommand(id), ct);
                return Results.Ok(v);
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

        return app;
    }
}
