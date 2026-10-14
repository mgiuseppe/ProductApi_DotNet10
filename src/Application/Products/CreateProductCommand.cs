using Application.Common.Interfaces;
using Domain.Products;
using MediatR;

namespace Application.Products;

public record CreateProductCommand(string Name, decimal Price, string? Description) : IRequest<ProductResponse>;

public class CreateProductCommandHandler(IProductRepository repository, IUnitOfWork uow) : IRequestHandler<CreateProductCommand, ProductResponse>
{
    public async Task<ProductResponse> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var product = Product.Create(request.Name, request.Price, request.Description);
        await repository.AddAsync(product, cancellationToken);
        await uow.SaveChangesAsync(cancellationToken);

        return ProductResponse.FromDomain(product);
    }
}
