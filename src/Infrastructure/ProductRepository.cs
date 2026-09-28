using System.Security.AccessControl;
using Application;
using Domain;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure;

internal class ProductRepository : IProductRepository
{
    private readonly AppDbContext _db;

    public ProductRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await _db.FindAsync<Product>([id], cancellationToken: ct);
    }

    public async Task<IReadOnlyList<Product>> ListProductsAsync(int page, int pageSize, CancellationToken ct)
    {
        var query = _db.Products.AsNoTracking()
                                .OrderBy((p) => p.Name)
                                .Skip((page-1) * pageSize)
                                .Take(pageSize);

        return await query.ToListAsync(ct);
    }

    public async Task AddAsync(Product product, CancellationToken ct)
    {
        await _db.Products.AddAsync(product, ct);
    }

    public void Delete(Product product, CancellationToken ct)
    {
        _db.Products.Remove(product);
    }
}
