using Domain.Exceptions;

namespace Domain.Tests;

public class VoucherRedemptionServiceTests
{
    private static readonly DateTime Start = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Within = new(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc);

    private readonly VoucherRedemptionService _sut = new();

    [Fact]
    public void ConfirmWithVoucher_WithValidVoucher_ConfirmsOrderAndRedeemsVoucher()
    {
        // Arrange
        var o = CreateOrderWithSubTotal(20m);
        var v = Voucher.Create(Start, End, 5m);
        // Act
        _sut.ConfirmWithVoucher(o, v, Within);
        // Assert
        Assert.Equal(OrderStatus.Confirmed, o.Status);
        Assert.Equal(v.Id, o.VoucherId);
        Assert.Equal(5m, o.DiscountAmount);
        Assert.Equal(20m, o.SubTotal);
        Assert.Equal(15m, o.Total);
        Assert.Equal(Within, v.RedeemedOn);
    }

    [Fact]
    public void ConfirmWithVoucher_WithAmountEqualToSubTotal_ConfirmsWithZeroTotal()
    {
        var o = CreateOrderWithSubTotal(20m);
        var v = Voucher.Create(Start, End, 20m);

        _sut.ConfirmWithVoucher(o, v, Within);

        Assert.Equal(OrderStatus.Confirmed, o.Status);
        Assert.Equal(0m, o.Total);
    }

    [Fact]
    public void ConfirmWithVoucher_WithAmountExceedingSubTotal_ThrowsWithoutRedeemingOrConfirming()
    {
        var o = CreateOrderWithSubTotal(20m);
        var v = Voucher.Create(Start, End, 20.01m);

        Assert.Throws<DomainException>(() => _sut.ConfirmWithVoucher(o, v, Within));

        Assert.Equal(OrderStatus.Draft, o.Status);
        Assert.Null(o.VoucherId);
        Assert.Null(v.RedeemedOn);
    }

    [Fact]
    public void ConfirmWithVoucher_WithVoucherNotRedeemable_ThrowsWithoutConfirming()
    {
        var o = CreateOrderWithSubTotal(20m);
        var v = Voucher.Create(Start, End, 5m);

        Assert.Throws<DomainException>(() => _sut.ConfirmWithVoucher(o, v, End.AddDays(1)));

        Assert.Equal(OrderStatus.Draft, o.Status);
        Assert.Null(o.VoucherId);
        Assert.Equal(0m, o.DiscountAmount);
    }

    #region private helpers
    private static Order CreateOrderWithSubTotal(decimal subTotal) => Order.Create().AddLine(Guid.NewGuid(), "Test", subTotal, 1);
    #endregion
}
