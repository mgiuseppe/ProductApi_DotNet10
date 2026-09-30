using Application.Commands;
using Application.Exceptions;
using Domain;
using Moq;

namespace Application.Tests;

public class OrderServiceTests
{
    private readonly Mock<IOrderRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly OrderService _sut; // "system under test"

    public OrderServiceTests()
    {
        _sut = new OrderService(_repository.Object, _unitOfWork.Object);
    }

    [Fact]
    public async Task CreateAsync_WithLines_AddsOrderAndSaves()
    {
        var cmd = new CreateOrderCommand([
            new CreateOrderLine(Guid.NewGuid(), "Product 1", 10m, 2),
            new CreateOrderLine(Guid.NewGuid(), "Product 2", 5m, 1)
        ]);

        var order = await _sut.CreateAsync(cmd, CancellationToken.None);

        Assert.Equal(OrderStatus.Draft, order.Status);
        Assert.Equal(2, order.Lines.Count);
        Assert.Equal(25m, order.Total);
        _repository.Verify(r => r.AddAsync(order, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentId_ThrowsNotFoundException()
    {
        var id = Guid.NewGuid();
        SetupOrderNotFound(id);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.DeleteAsync(id, CancellationToken.None));

        _repository.Verify(r => r.Delete(It.IsAny<Order>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_WithExistingId_DeletesAndSaves()
    {
        var order = Order.Create();
        SetupOrder(order);

        await _sut.DeleteAsync(order.Id, CancellationToken.None);

        _repository.Verify(r => r.Delete(order), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistentId_ThrowsNotFoundException()
    {
        var id = Guid.NewGuid();
        var cmd = new UpdateOrderCommand([], [], []);
        SetupOrderNotFound(id);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.UpdateAsync(id, cmd, CancellationToken.None));

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WithExistingId_AppliesChangesAndSaves()
    {
        var order = Order.Create()
            .AddLine(Guid.NewGuid(), "To update", 10m, 1)
            .AddLine(Guid.NewGuid(), "To delete", 20m, 1);
        var lineToUpdate = order.Lines.First(l => l.ProductNameSnapshot == "To update");
        var lineToDelete = order.Lines.First(l => l.ProductNameSnapshot == "To delete");
        SetupOrder(order);

        var cmd = new UpdateOrderCommand(
            [new CreateOrderLine(Guid.NewGuid(), "Added", 3m, 2)],
            [new UpdateOrderLine(lineToUpdate.Id, 15m, 3)],
            [lineToDelete.Id]);

        var result = await _sut.UpdateAsync(order.Id, cmd, CancellationToken.None);

        Assert.Equal(2, result.Lines.Count);
        Assert.DoesNotContain(result.Lines, l => l.Id == lineToDelete.Id);
        Assert.Contains(result.Lines, l => l.ProductNameSnapshot == "Added");
        Assert.Equal(15m, lineToUpdate.UnitPriceSnapshot);
        Assert.Equal(3, lineToUpdate.Quantity);
        Assert.Equal(51m, result.Total);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmAsync_WithNonExistentId_ThrowsNotFoundException()
    {
        var id = Guid.NewGuid();
        SetupOrderNotFound(id);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.ConfirmAsync(id, CancellationToken.None));

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmAsync_WithDraftOrder_ConfirmsAndSaves()
    {
        var order = Order.Create().AddLine(Guid.NewGuid(), "Product", 10m, 1);
        SetupOrder(order);

        var result = await _sut.ConfirmAsync(order.Id, CancellationToken.None);

        Assert.Equal(OrderStatus.Confirmed, result.Status);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ShipAsync_WithNonExistentId_ThrowsNotFoundException()
    {
        var id = Guid.NewGuid();
        SetupOrderNotFound(id);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.ShipAsync(id, CancellationToken.None));

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ShipAsync_WithConfirmedOrder_ShipsAndSaves()
    {
        var order = Order.Create().AddLine(Guid.NewGuid(), "Product", 10m, 1);
        order.Confirm();
        SetupOrder(order);

        var result = await _sut.ShipAsync(order.Id, CancellationToken.None);

        Assert.Equal(OrderStatus.Shipped, result.Status);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelAsync_WithNonExistentId_ThrowsNotFoundException()
    {
        var id = Guid.NewGuid();
        SetupOrderNotFound(id);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.CancelAsync(id, CancellationToken.None));

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelAsync_WithConfirmedOrder_CancelsAndSaves()
    {
        var order = Order.Create().AddLine(Guid.NewGuid(), "Product", 10m, 1);
        order.Confirm();
        SetupOrder(order);

        var result = await _sut.CancelAsync(order.Id, CancellationToken.None);

        Assert.Equal(OrderStatus.Cancelled, result.Status);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #region private helpers
    private void SetupOrder(Order order)
    {
        _repository
            .Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
    }

    private void SetupOrderNotFound(Guid id)
    {
        _repository
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);
    }
    #endregion
}
