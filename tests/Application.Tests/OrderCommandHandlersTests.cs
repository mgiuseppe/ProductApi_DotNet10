using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Orders;
using Domain;
using Domain.Orders;
using Domain.Products;
using Domain.Vouchers;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Application.Tests;

public class OrderCommandHandlersTests
{
    private readonly Mock<IOrderRepository> _repository = new();
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IVoucherRepository> _voucherRepository = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));
    private readonly Meter _meter = new(OrderMetrics.MeterName);

    [Fact]
    public async Task CreateOrder_WithLines_AddsOrderAndSaves()
    {
        var p1 = Product.Create("Product 1", 10m);
        var p2 = Product.Create("Product 2", 5m);
        SetupProducts(p1, p2);
        var cmd = new CreateOrderCommand([
            new CreateOrderLine(p1.Id, 2),
            new CreateOrderLine(p2.Id, 1)
        ]);
        Order? added = null;
        _repository
            .Setup(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Callback<Order, CancellationToken>((o, _) => added = o);
        var sut = new CreateOrderCommandHandler(_repository.Object, NewProductLookup(), _unitOfWork.Object); // "system under test"

        var result = await sut.Handle(cmd, CancellationToken.None);

        Assert.NotNull(added);
        Assert.Equal(added.Id, result.Id);
        Assert.Equal(OrderStatus.Draft, result.Status);
        Assert.Equal(2, result.Lines.Count);
        Assert.Contains(result.Lines, l => l.ProductId == p1.Id && l.ProductNameSnapshot == "Product 1" && l.UnitPriceSnapshot == 10m);
        Assert.Contains(result.Lines, l => l.ProductId == p2.Id && l.ProductNameSnapshot == "Product 2" && l.UnitPriceSnapshot == 5m);
        Assert.Equal(25m, result.Total);
        _repository.Verify(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateOrder_WithNonExistentProduct_ThrowsNotFoundException()
    {
        var existing = Product.Create("Product 1", 10m);
        SetupProducts(existing);
        var cmd = new CreateOrderCommand([
            new CreateOrderLine(existing.Id, 1),
            new CreateOrderLine(Guid.NewGuid(), 1)
        ]);
        var sut = new CreateOrderCommandHandler(_repository.Object, NewProductLookup(), _unitOfWork.Object);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sut.Handle(cmd, CancellationToken.None));

        _repository.Verify(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteOrder_WithNonExistentId_ThrowsNotFoundException()
    {
        var id = Guid.NewGuid();
        SetupOrderNotFound(id);
        var sut = new DeleteOrderCommandHandler(_repository.Object, _unitOfWork.Object);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sut.Handle(new DeleteOrderCommand(id), CancellationToken.None));

        _repository.Verify(r => r.Delete(It.IsAny<Order>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteOrder_WithExistingId_DeletesAndSaves()
    {
        var order = Order.Create();
        SetupOrder(order);
        var sut = new DeleteOrderCommandHandler(_repository.Object, _unitOfWork.Object);

        await sut.Handle(new DeleteOrderCommand(order.Id), CancellationToken.None);

        _repository.Verify(r => r.Delete(order), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateOrder_WithNonExistentId_ThrowsNotFoundException()
    {
        var id = Guid.NewGuid();
        var cmd = new UpdateOrderCommand(id, [], [], []);
        SetupOrderNotFound(id);
        var sut = new UpdateOrderCommandHandler(_repository.Object, NewProductLookup(), _unitOfWork.Object);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sut.Handle(cmd, CancellationToken.None));

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateOrder_WithExistingId_AppliesChangesAndSaves()
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
        var cmd = new UpdateOrderCommand(order.Id,
            [new CreateOrderLine(added.Id, 2)],
            [new UpdateOrderLine(lineToUpdate.Id, 3)],
            [lineToDelete.Id]);
        var sut = new UpdateOrderCommandHandler(_repository.Object, NewProductLookup(), _unitOfWork.Object);

        var result = await sut.Handle(cmd, CancellationToken.None);

        Assert.Equal(2, result.Lines.Count);
        Assert.DoesNotContain(result.Lines, l => l.Id == lineToDelete.Id);
        Assert.Contains(result.Lines, l => l.ProductNameSnapshot == "Added");
        var updatedLine = result.Lines.Single(l => l.Id == lineToUpdate.Id);
        Assert.Equal(15m, updatedLine.UnitPriceSnapshot);
        Assert.Equal(3, updatedLine.Quantity);
        Assert.Equal(51m, result.Total);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateOrder_WithNonExistentLine_ThrowsNotFoundException()
    {
        var product = Product.Create("Product", 10m);
        var order = Order.Create().AddLine(product.Id, product.Name, product.Price, 1);
        SetupOrder(order);
        SetupProducts(product);
        var cmd = new UpdateOrderCommand(order.Id, [], [new UpdateOrderLine(Guid.NewGuid(), 2)], []);
        var sut = new UpdateOrderCommandHandler(_repository.Object, NewProductLookup(), _unitOfWork.Object);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sut.Handle(cmd, CancellationToken.None));

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateOrder_WithNonExistentProductToAdd_ThrowsNotFoundException()
    {
        var order = Order.Create();
        SetupProducts();
        SetupOrder(order);
        var cmd = new UpdateOrderCommand(order.Id, [new CreateOrderLine(Guid.NewGuid(), 1)], [], []);
        var sut = new UpdateOrderCommandHandler(_repository.Object, NewProductLookup(), _unitOfWork.Object);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sut.Handle(cmd, CancellationToken.None));

        Assert.Empty(order.Lines);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmOrder_WithNonExistentId_ThrowsNotFoundException()
    {
        var id = Guid.NewGuid();
        SetupOrderNotFound(id);
        var sut = NewConfirmOrderHandler();

        await Assert.ThrowsAsync<NotFoundException>(
            () => sut.Handle(new ConfirmOrderCommand(id, null), CancellationToken.None));

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmOrder_WithDraftOrder_ConfirmsAndSaves()
    {
        var product = Product.Create("Product", 10m);
        var order = Order.Create().AddLine(product.Id, product.Name, product.Price, 1);
        SetupOrder(order);
        SetupProducts(product);
        var sut = NewConfirmOrderHandler();

        var result = await sut.Handle(new ConfirmOrderCommand(order.Id, null), CancellationToken.None);

        Assert.Equal(OrderStatus.Confirmed, result.Status);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmOrder_WithStalePrices_ResyncsLinesBeforeConfirming()
    {
        var product = Product.Create("Product", 10m);
        var order = Order.Create().AddLine(product.Id, product.Name, product.Price, 2);
        product.ChangePrice(12m);
        SetupOrder(order);
        SetupProducts(product);
        var sut = NewConfirmOrderHandler();

        var result = await sut.Handle(new ConfirmOrderCommand(order.Id, null), CancellationToken.None);

        Assert.Equal(OrderStatus.Confirmed, result.Status);
        Assert.Equal(12m, result.Lines.Single().UnitPriceSnapshot);
        Assert.Equal(24m, result.Total);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmOrder_WithNonExistentProduct_ThrowsNotFoundException()
    {
        var order = Order.Create().AddLine(Guid.NewGuid(), "Deleted product", 10m, 1);
        SetupProducts();
        SetupOrder(order);
        var sut = NewConfirmOrderHandler();

        await Assert.ThrowsAsync<NotFoundException>(
            () => sut.Handle(new ConfirmOrderCommand(order.Id, null), CancellationToken.None));

        Assert.Equal(OrderStatus.Draft, order.Status);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmOrder_WithNonExistentVoucher_ThrowsNotFoundException()
    {
        var product = Product.Create("Product", 10m);
        var order = Order.Create().AddLine(product.Id, product.Name, product.Price, 1);
        var voucherId = Guid.NewGuid();
        SetupOrder(order);
        SetupProducts(product);
        _voucherRepository
            .Setup(r => r.GetByIdAsync(voucherId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Voucher?)null);
        var sut = NewConfirmOrderHandler();

        await Assert.ThrowsAsync<NotFoundException>(
            () => sut.Handle(new ConfirmOrderCommand(order.Id, voucherId), CancellationToken.None));

        Assert.Equal(OrderStatus.Draft, order.Status);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmOrder_WithVoucher_AppliesDiscountRedeemsAtCurrentTimeAndSaves()
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var product = Product.Create("Product", 10m);
        var order = Order.Create().AddLine(product.Id, product.Name, product.Price, 2);
        var voucher = Voucher.Create(now.AddDays(-1), now.AddDays(1), 5m);
        SetupOrder(order);
        SetupProducts(product);
        _voucherRepository
            .Setup(r => r.GetByIdAsync(voucher.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(voucher);
        var sut = NewConfirmOrderHandler();

        var result = await sut.Handle(new ConfirmOrderCommand(order.Id, voucher.Id), CancellationToken.None);

        Assert.Equal(OrderStatus.Confirmed, result.Status);
        Assert.Equal(15m, result.Total);
        Assert.Equal(voucher.Id, order.VoucherId);
        Assert.Equal(now, voucher.RedeemedOn);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmOrder_WithVoucher_UsesResyncedPricesForDiscountCheck()
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var product = Product.Create("Product", 10m);
        var order = Order.Create().AddLine(product.Id, product.Name, product.Price, 1);
        product.ChangePrice(4m);
        var voucher = Voucher.Create(now.AddDays(-1), now.AddDays(1), 5m);
        SetupOrder(order);
        SetupProducts(product);
        _voucherRepository
            .Setup(r => r.GetByIdAsync(voucher.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(voucher);
        var sut = NewConfirmOrderHandler();

        await Assert.ThrowsAsync<Domain.Exceptions.DomainException>(
            () => sut.Handle(new ConfirmOrderCommand(order.Id, voucher.Id), CancellationToken.None));

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmOrder_WhenSaveThrowsConflict_RecordsConflictOnlyAndRethrows()
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var product = Product.Create("Product", 10m);
        var order = Order.Create().AddLine(product.Id, product.Name, product.Price, 2);
        var voucher = Voucher.Create(now.AddDays(-1), now.AddDays(1), 5m);
        SetupOrder(order);
        SetupProducts(product);
        _voucherRepository
            .Setup(r => r.GetByIdAsync(voucher.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(voucher);
        _unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException("Voucher already redeemed"));
        var sut = NewConfirmOrderHandler();
        using var conflicts = new MetricCollector<long>(_meter, "vouchers.redemption.concurrent_conflicts");
        using var confirmed = new MetricCollector<long>(_meter, "orders.confirmed");

        await Assert.ThrowsAsync<ConflictException>(
            () => sut.Handle(new ConfirmOrderCommand(order.Id, voucher.Id), CancellationToken.None));

        Assert.Equal(1, Assert.Single(conflicts.GetMeasurementSnapshot()).Value);
        Assert.Empty(confirmed.GetMeasurementSnapshot());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConfirmOrder_AfterSave_RecordsConfirmedMetricWithVoucherTag(bool withVoucher)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var product = Product.Create("Product", 10m);
        var order = Order.Create().AddLine(product.Id, product.Name, product.Price, 2);
        var voucher = Voucher.Create(now.AddDays(-1), now.AddDays(1), 5m);
        SetupOrder(order);
        SetupProducts(product);
        _voucherRepository
            .Setup(r => r.GetByIdAsync(voucher.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(voucher);
        var sut = NewConfirmOrderHandler();
        using var confirmed = new MetricCollector<long>(_meter, "orders.confirmed");

        await sut.Handle(new ConfirmOrderCommand(order.Id, withVoucher ? voucher.Id : null), CancellationToken.None);

        var measurement = Assert.Single(confirmed.GetMeasurementSnapshot());
        Assert.Equal(1, measurement.Value);
        Assert.Equal(withVoucher, measurement.Tags["with_voucher"]);
    }

    [Fact]
    public async Task ShipOrder_WithNonExistentId_ThrowsNotFoundException()
    {
        var id = Guid.NewGuid();
        SetupOrderNotFound(id);
        var sut = new ShipOrderCommandHandler(_repository.Object, _unitOfWork.Object);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sut.Handle(new ShipOrderCommand(id), CancellationToken.None));

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ShipOrder_WithConfirmedOrder_ShipsAndSaves()
    {
        var order = Order.Create().AddLine(Guid.NewGuid(), "Product", 10m, 1);
        order.Confirm();
        SetupOrder(order);
        var sut = new ShipOrderCommandHandler(_repository.Object, _unitOfWork.Object);

        var result = await sut.Handle(new ShipOrderCommand(order.Id), CancellationToken.None);

        Assert.Equal(OrderStatus.Shipped, result.Status);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelOrder_WithNonExistentId_ThrowsNotFoundException()
    {
        var id = Guid.NewGuid();
        SetupOrderNotFound(id);
        var sut = NewCancelOrderHandler();

        await Assert.ThrowsAsync<NotFoundException>(
            () => sut.Handle(new CancelOrderCommand(id), CancellationToken.None));

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelOrder_WithConfirmedOrder_CancelsAndSaves()
    {
        var order = Order.Create().AddLine(Guid.NewGuid(), "Product", 10m, 1);
        order.Confirm();
        SetupOrder(order);
        var sut = NewCancelOrderHandler();

        var result = await sut.Handle(new CancelOrderCommand(order.Id), CancellationToken.None);

        Assert.Equal(OrderStatus.Cancelled, result.Status);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #region private helpers
    private ProductLookup NewProductLookup() => new(_productRepository.Object);

    private OrderMetrics NewOrderMetrics()
    {
        // The factory hands out _meter, so tests can attach a MetricCollector to it
        var meterFactory = new Mock<IMeterFactory>();
        meterFactory
            .Setup(f => f.Create(It.IsAny<MeterOptions>()))
            .Returns(_meter);
        return new OrderMetrics(meterFactory.Object);
    }

    private ConfirmOrderCommandHandler NewConfirmOrderHandler() =>
        new(_repository.Object, _voucherRepository.Object, new VoucherRedemptionService(), _unitOfWork.Object, NewProductLookup(), _timeProvider, NewOrderMetrics());

    private CancelOrderCommandHandler NewCancelOrderHandler() =>
        new(_repository.Object, _unitOfWork.Object);

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
