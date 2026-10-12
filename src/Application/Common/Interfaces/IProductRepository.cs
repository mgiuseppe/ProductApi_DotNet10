using Domain.Products;

namespace Application.Common.Interfaces;

// Simple interface without UOW
public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Product>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct);
    Task AddAsync(Product product, CancellationToken ct);
    void Delete(Product product);
}
