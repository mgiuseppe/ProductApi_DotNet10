using Application.Exceptions;
using MediatR;

namespace Application.Products;

public record UpdateProductCommand(Guid Id, string Name, decimal Price, string? Description) : IRequest<ProductResponse>;

public class UpdateProductCommandHandler(IProductRepository repository, IUnitOfWork unitOfWork) : IRequestHandler<UpdateProductCommand, ProductResponse>
{
    public async Task<ProductResponse> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var p = await repository.GetByIdAsync(request.Id, cancellationToken) ?? throw new NotFoundException($"Product {request.Id} not found");

        p.Rename(request.Name);
        p.ChangePrice(request.Price);
        p.ChangeDescription(request.Description);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ProductResponse.FromDomain(p);
    }
}