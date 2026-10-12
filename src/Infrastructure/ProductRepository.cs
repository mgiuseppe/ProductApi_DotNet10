using System.Security.AccessControl;
using Application.Common.Interfaces;
using Domain.Products;
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

    public async Task<IReadOnlyList<Product>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        return await _db.Products.Where(p => ids.Contains(p.Id)).ToListAsync(ct);
    }

    public async Task AddAsync(Product product, CancellationToken ct)
    {
        await _db.Products.AddAsync(product, ct);
    }

    public void Delete(Product product)
    {
        _db.Products.Remove(product);
    }
}
