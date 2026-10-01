using Application.Commands;
using Application.Exceptions;
using Domain;
using Moq;

namespace Application.Tests;

public class OrderServiceTests
{
    private readonly Mock<IOrderRepository> _repository = new();
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly OrderService _sut; // "system under test"

    public OrderServiceTests()
    {
        _sut = new OrderService(_repository.Object, _productRepository.Object, _unitOfWork.Object);
    }

    [Fact]
    public async Task CreateAsync_WithLines_AddsOrderAndSaves()
    {
        var p1 = Product.Create("Product 1", 10m);
        var p2 = Product.Create("Product 2", 5m);
        SetupProducts(p1, p2);
        var cmd = new CreateOrderCommand([
            new CreateOrderLine(p1.Id, 2),
            new CreateOrderLine(p2.Id, 1)
        ]);

        var order = await _sut.CreateAsync(cmd, CancellationToken.None);

        Assert.Equal(OrderStatus.Draft, order.Status);
        Assert.Equal(2, order.Lines.Count);
        Assert.Contains(order.Lines, l => l.ProductId == p1.Id && l.ProductNameSnapshot == "Product 1" && l.UnitPriceSnapshot == 10m);
        Assert.Contains(order.Lines, l => l.ProductId == p2.Id && l.ProductNameSnapshot == "Product 2" && l.UnitPriceSnapshot == 5m);
        Assert.Equal(25m, order.Total);
        _repository.Verify(r => r.AddAsync(order, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithNonExistentProduct_ThrowsNotFoundException()
    {
        var existing = Product.Create("Product 1", 10m);
        SetupProducts(existing);
        var cmd = new CreateOrderCommand([
            new CreateOrderLine(existing.Id, 1),
            new CreateOrderLine(Guid.NewGuid(), 1)
        ]);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.CreateAsync(cmd, CancellationToken.None));

        _repository.Verify(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
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
        var toUpdate = Product.Create("To update", 10m);
        var toDelete = Product.Create("To delete", 20m);
        var added = Product.Create("Added", 3m);
        var order = Order.Create()
            .AddLine(toUpdate.Id, toUpdate.Name, toUpdate.Price, 1)
            .AddLine(toDelete.Id, toDelete.Name, toDelete.Price, 1);
        var lineToUpdate = order.Lines.First(l => l.ProductId == toUpdate.Id);
        var lineToDelete = order.Lines.First(l => l.ProductId == toDelete.Id);
        toUpdate.ChangePrice(15m); // updated lines pick up the current product price
        SetupOrder(order);
        SetupProducts(toUpdate, toDelete, added);

        var cmd = new UpdateOrderCommand(
            [new CreateOrderLine(added.Id, 2)],
            [new UpdateOrderLine(lineToUpdate.Id, 3)],
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
    public async Task UpdateAsync_WithNonExistentLine_ThrowsNotFoundException()
    {
        var product = Product.Create("Product", 10m);
        var order = Order.Create().AddLine(product.Id, product.Name, product.Price, 1);
        SetupOrder(order);
        SetupProducts(product);
        var cmd = new UpdateOrderCommand([], [new UpdateOrderLine(Guid.NewGuid(), 2)], []);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.UpdateAsync(order.Id, cmd, CancellationToken.None));

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistentProductToAdd_ThrowsNotFoundException()
    {
        var order = Order.Create();
        SetupProducts();
        SetupOrder(order);
        var cmd = new UpdateOrderCommand([new CreateOrderLine(Guid.NewGuid(), 1)], [], []);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.UpdateAsync(order.Id, cmd, CancellationToken.None));

        Assert.Empty(order.Lines);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
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
        var product = Product.Create("Product", 10m);
        var order = Order.Create().AddLine(product.Id, product.Name, product.Price, 1);
        SetupOrder(order);
        SetupProducts(product);

        var result = await _sut.ConfirmAsync(order.Id, CancellationToken.None);

        Assert.Equal(OrderStatus.Confirmed, result.Status);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmAsync_WithStalePrices_ResyncsLinesBeforeConfirming()
    {
        var product = Product.Create("Product", 10m);
        var order = Order.Create().AddLine(product.Id, product.Name, product.Price, 2);
        product.ChangePrice(12m);
        SetupOrder(order);
        SetupProducts(product);

        var result = await _sut.ConfirmAsync(order.Id, CancellationToken.None);

        Assert.Equal(OrderStatus.Confirmed, result.Status);
        Assert.Equal(12m, result.Lines.Single().UnitPriceSnapshot);
        Assert.Equal(24m, result.Total);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmAsync_WithNonExistentProduct_ThrowsNotFoundException()
    {
        var order = Order.Create().AddLine(Guid.NewGuid(), "Deleted product", 10m, 1);
        SetupProducts();
        SetupOrder(order);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.ConfirmAsync(order.Id, CancellationToken.None));

        Assert.Equal(OrderStatus.Draft, order.Status);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
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

    private void SetupProducts(params Product[] products)
    {
        _productRepository
            .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Guid> ids, CancellationToken _) =>
                products.Where(p => ids.Contains(p.Id)).ToList());
    }

    private void SetupOrderNotFound(Guid id)
    {
        _repository
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);
    }
    #endregion
}
