using Application.Commands;
using Application.Exceptions;
using Domain;
using Moq;

namespace Application.Tests;

public class ProductServiceTests
{
    private readonly Mock<IProductRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly ProductService _sut; // "system under test"

    public ProductServiceTests()
    {
        _sut = new ProductService(_repository.Object, _unitOfWork.Object);
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentId_ThrowsNotFoundException()
    {
        var id = Guid.NewGuid();
        _repository
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        // Tests throws
        await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.DeleteAsync(id, CancellationToken.None));

        // Tests that nobody calls uow.SaveChangesAsync
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistentId_ThrowsNotFoundException()
    {
        var id = Guid.NewGuid();
        var cmd = new UpdateProductCommand("Name", 10m, "Description");
        _repository
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.UpdateAsync(id, cmd, CancellationToken.None));

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
