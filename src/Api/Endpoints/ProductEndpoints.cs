using Application;
using Application.Commands;
using Application.Exceptions;
using Application.Responses;
using Domain.Exceptions;

namespace Api.Endpoints;

public static class ProductEndpoints
{
    public static WebApplication MapProductEndpoints(this WebApplication app)
    {
        app.MapGet("/products/{id:guid}", async (Guid id, IProductService service, CancellationToken ct) =>
        {
            var p = await service.GetByIdAsync(id, ct);
            return p != null ? Results.Ok(ProductResponse.FromDomain(p)) : Results.NotFound();
        });

        app.MapGet("/products", async (int page, int pageSize, IProductService service, CancellationToken ct) =>
        {
            if (page <= 0 || pageSize <= 0)
                return Results.BadRequest($"Page and PageSize should be greater than zero - Page: {page} - PageSize: {pageSize} ");

            var ps = await service.GetListAsync(page, pageSize, ct);
            return Results.Ok(ps.Select(p => ProductResponse.FromDomain(p)));
        });

        app.MapPost("/products", async (CreateProductCommand cmd, IProductService service, CancellationToken ct) =>
        {
            try
            {
                var p = await service.CreateAsync(cmd, ct);
                return Results.Created($"/products/{p.Id}", ProductResponse.FromDomain(p));
            }
            catch (DomainException e)
            {
                return Results.BadRequest(e.Message);
            }
        });

        app.MapPut("/products/{id:guid}", async (Guid id, UpdateProductCommand cmd, IProductService service, CancellationToken ct) =>
        {
            try
            {
                var p = await service.UpdateAsync(id, cmd, ct);
                return Results.Ok(ProductResponse.FromDomain(p));
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

        app.MapDelete("/products/{id:guid}", async (Guid id, IProductService service, CancellationToken ct) =>
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
