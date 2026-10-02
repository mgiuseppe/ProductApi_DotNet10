using Application.Exceptions;
using MediatR;

namespace Application.Products;

public record DeleteProductCommand(Guid Id) : IRequest;

public class DeleteProductCommandHandler(IProductRepository repository, IUnitOfWork unitOfWork) : IRequestHandler<DeleteProductCommand>
{
    public async Task Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        var product = await repository.GetByIdAsync(request.Id, cancellationToken) ?? throw new NotFoundException($"Product {request.Id} not found");
        
        repository.Delete(product);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
