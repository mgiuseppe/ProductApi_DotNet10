using Application.Commands;
using Domain;

namespace Application;

public interface IProductService
{
    Task<Product> CreateAsync(CreateProductCommand cmd, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
    Task<Product?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Product>> GetListAsync(int page, int pageSize, CancellationToken ct);
    Task<Product> UpdateAsync(Guid id, UpdateProductCommand cmd, CancellationToken ct);
}
