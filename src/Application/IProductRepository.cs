using Domain;

namespace Application;

// Simple interface without UOW
public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Product>> ListProductsAsync(int page, int pageSize, CancellationToken ct);
    Task AddAsync(Product product, CancellationToken ct);
    void Delete(Product product, CancellationToken ct);
}
