using Api.Requests;
using Application.Products;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Api.Endpoints;

public static class ProductEndpoints
{
    public static WebApplication MapProductEndpoints(this WebApplication app)
    {
        var products = app.MapGroup("/products").WithTags("Products");

        products.MapGet("/{id:guid}", GetProductById).WithName(nameof(GetProductById));
        products.MapGet("/", GetProducts).WithName(nameof(GetProducts))
            .ProducesProblem(StatusCodes.Status400BadRequest);
        products.MapPost("/", CreateProduct).WithName(nameof(CreateProduct))
            .ProducesProblem(StatusCodes.Status400BadRequest);
        products.MapPut("/{id:guid}", UpdateProduct).WithName(nameof(UpdateProduct))
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
        products.MapDelete("/{id:guid}", DeleteProduct).WithName(nameof(DeleteProduct))
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    /// <summary>Get a product</summary>
    /// <response code="200">The product.</response>
    /// <response code="404">No product has the given id.</response>
    internal static async Task<Results<Ok<ProductResponse>, NotFound>> GetProductById(
        Guid id, IMediator mediator, CancellationToken ct)
    {
        var product = await mediator.Send(new GetProductByIdQuery(id), ct);
        return product != null ? TypedResults.Ok(product) : TypedResults.NotFound();
    }

    /// <summary>List products</summary>
    /// <remarks>One page of products. Page and page size start at 1.</remarks>
    /// <param name="page">Page number, from 1. Default 1.</param>
    /// <param name="pageSize">Products per page, from 1 to 100. Default 10.</param>
    /// <response code="200">The products of the page; empty after the last page.</response>
    /// <response code="400">Page lower than 1, or page size not between 1 and 100.</response>
    internal static async Task<Ok<IReadOnlyList<ProductResponse>>> GetProducts(
        IMediator mediator, CancellationToken ct, int page = 1, int pageSize = 10)
    {
        return TypedResults.Ok(await mediator.Send(new GetProductsListQuery(page, pageSize), ct));
    }

    /// <summary>Create a product</summary>
    /// <response code="201">The product created; the Location header is its URL.</response>
    /// <response code="400">The name is empty or too long, the price is negative or the description is too long.</response>
    internal static async Task<CreatedAtRoute<ProductResponse>> CreateProduct(
        CreateProductCommand cmd, IMediator mediator, CancellationToken ct)
    {
        var product = await mediator.Send(cmd, ct);
        return TypedResults.CreatedAtRoute(product, nameof(GetProductById), new { id = product.Id });
    }

    /// <summary>Update a product</summary>
    /// <remarks>Replaces name, price and description.</remarks>
    /// <response code="200">The product updated.</response>
    /// <response code="400">The values are not valid (same rules as create).</response>
    /// <response code="404">The product does not exist.</response>
    internal static async Task<Ok<ProductResponse>> UpdateProduct(
        Guid id, UpdateProductRequest req, IMediator mediator, CancellationToken ct)
    {
        var product = await mediator.Send(new UpdateProductCommand(id, req.Name, req.Price, req.Description), ct);
        return TypedResults.Ok(product);
    }

    /// <summary>Delete a product</summary>
    /// <response code="204">The product deleted.</response>
    /// <response code="404">The product does not exist.</response>
    internal static async Task<NoContent> DeleteProduct(Guid id, IMediator mediator, CancellationToken ct)
    {
        await mediator.Send(new DeleteProductCommand(id), ct);
        return TypedResults.NoContent();
    }
}
