using Application.Exceptions;
using Application.Products;
using Domain;
using Moq;

namespace Application.Tests;

public class ProductCommandHandlersTests
{
    private readonly Mock<IProductRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    [Fact]
    public async Task DeleteProduct_WithNonExistentId_ThrowsNotFoundException()
    {
        var id = Guid.NewGuid();
        _repository
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);
        var sut = new DeleteProductCommandHandler(_repository.Object, _unitOfWork.Object); // "system under test"

        // Tests throws
        await Assert.ThrowsAsync<NotFoundException>(
            () => sut.Handle(new DeleteProductCommand(id), CancellationToken.None));

        // Tests that nobody calls uow.SaveChangesAsync
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateProduct_WithNonExistentId_ThrowsNotFoundException()
    {
        var id = Guid.NewGuid();
        var cmd = new UpdateProductCommand(id, "Name", 10m, "Description");
        _repository
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);
        var sut = new UpdateProductCommandHandler(_repository.Object, _unitOfWork.Object);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sut.Handle(cmd, CancellationToken.None));

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
