using Application.Commands;
using Application.Exceptions;
using Domain;

namespace Application;

public class ProductService(IProductRepository repository, IUnitOfWork unitOfWork) : IProductService
{
    private readonly IProductRepository _repository = repository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await _repository.GetByIdAsync(id, ct);
    }

    public async Task<IReadOnlyList<Product>> GetListAsync(int page, int pageSize, CancellationToken ct)
    {
        return await _repository.ListProductsAsync(page, pageSize, ct);
    }

    public async Task<Product> CreateAsync(CreateProductCommand cmd, CancellationToken ct)
    {
        var p = Product.Create(cmd.Name, cmd.Price, cmd.Description);
        await _repository.AddAsync(p, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return p;
    }

    public async Task<Product> UpdateAsync(Guid id, UpdateProductCommand cmd, CancellationToken ct)
    {
        var p = await _repository.GetByIdAsync(id, ct) ?? throw new NotFoundException($"Product {id} not found");

        p.Rename(cmd.Name);
        p.ChangePrice(cmd.Price);
        p.ChangeDescription(cmd.Description);
        await _unitOfWork.SaveChangesAsync(ct);

        return p;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var p = await _repository.GetByIdAsync(id, ct) ?? throw new NotFoundException($"Product {id} not found");
        _repository.Delete(p);
        await _unitOfWork.SaveChangesAsync(ct);
    }
}
