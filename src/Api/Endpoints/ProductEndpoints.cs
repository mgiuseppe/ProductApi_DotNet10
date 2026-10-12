using Api.Requests;
using Application.Common.Exceptions;
using Application.Products;
using Domain.Exceptions;
using MediatR;

namespace Api.Endpoints;

public static class ProductEndpoints
{
    public static WebApplication MapProductEndpoints(this WebApplication app)
    {
        app.MapGet("/products/{id:guid}", async (Guid id, IMediator mediator, CancellationToken ct) =>
        {
            var p = await mediator.Send(new GetProductByIdQuery(id), ct);
            return p != null ? Results.Ok(p) : Results.NotFound();
        });

        app.MapGet("/products", async (int page, int pageSize, IMediator mediator, CancellationToken ct) =>
        {
            if (page <= 0 || pageSize <= 0)
                return Results.BadRequest($"Page and PageSize should be greater than zero - Page: {page} - PageSize: {pageSize} ");

            return Results.Ok(await mediator.Send(new GetProductsListQuery(page, pageSize), ct));
        });

        app.MapPost("/products", async (CreateProductCommand cmd, IMediator mediator, CancellationToken ct) =>
        {
            var response = await mediator.Send(cmd, ct);
            return Results.Created($"/products/{response.Id}", response);
        });

        app.MapPut("/products/{id:guid}", async (Guid id, UpdateProductRequest req, IMediator mediator, CancellationToken ct) =>
        {
            var cmd = new UpdateProductCommand(id, req.Name, req.Price, req.Description);
            var response = await mediator.Send(cmd, ct);
            return Results.Ok(response);
        });

        app.MapDelete("/products/{id:guid}", async (Guid id, IMediator mediator, CancellationToken ct) =>
        {
            var cmd = new DeleteProductCommand(id);
            await mediator.Send(cmd, ct);
            return Results.NoContent();
        });

        return app;
    }
}