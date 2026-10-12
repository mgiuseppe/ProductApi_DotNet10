using Application.Common.Interfaces;
using Application.Common.Exceptions;
using Domain.Products;

namespace Application.Orders;

internal class ProductLookup(IProductRepository productRepository)
{
    internal async Task<Dictionary<Guid, Product>> GetProductsOrThrowAsync(IEnumerable<Guid> productIds, CancellationToken ct)
    {
        var ids = productIds.Distinct().ToList();
        var prods = await productRepository.GetByIdsAsync(ids, ct);
        var prodsById = prods.ToDictionary(p => p.Id);

        var missing = ids.Except(prodsById.Keys).ToList();
        if (missing.Count > 0)
            throw new NotFoundException($"Product(s) not found: {string.Join(", ", missing)}");

        return prodsById;
    }
}
