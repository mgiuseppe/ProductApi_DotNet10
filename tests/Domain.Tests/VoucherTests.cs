using Domain.Exceptions;

namespace Domain.Tests;

public class VoucherTests
{
    private static readonly DateTime Start = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Within = new(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_WithValidValues_CreatesActiveVoucher()
    {
        // Act
        var v = Voucher.Create(Start, End, 10m);
        // Assert
        Assert.NotEqual(Guid.Empty, v.Id);
        Assert.Equal(Start, v.ValidityStartDate);
        Assert.Equal(End, v.ValidityEndDate);
        Assert.Equal(10m, v.Amount);
        Assert.Null(v.RedeemedOn);
        Assert.Null(v.CanceledOn);
    }

    [Fact]
    public void Create_WithStartEqualToEnd_Throws()
    {
        Assert.Throws<DomainException>(() => Voucher.Create(Start, Start, 10m));
    }

    [Fact]
    public void Create_WithStartAfterEnd_Throws()
    {
        Assert.Throws<DomainException>(() => Voucher.Create(End, Start, 10m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithNonPositiveAmount_Throws(decimal amount)
    {
        Assert.Throws<DomainException>(() => Voucher.Create(Start, End, amount));
    }

    [Fact]
    public void Redeem_WithinValidity_SetsRedeemedOn()
    {
        // Arrange
        var v = CreateVoucher();
        // Act
        v.Redeem(Within);
        // Assert
        Assert.Equal(Within, v.RedeemedOn);
    }

    [Fact]
    public void Redeem_OnValidityBoundaries_IsAllowed()
    {
        var first = CreateVoucher();
        var last = CreateVoucher();

        first.Redeem(Start);
        last.Redeem(End);

        Assert.Equal(Start, first.RedeemedOn);
        Assert.Equal(End, last.RedeemedOn);
    }

    [Fact]
    public void Redeem_BeforeValidityStart_Throws()
    {
        var v = CreateVoucher();

        Assert.Throws<DomainException>(() => v.Redeem(Start.AddTicks(-1)));
        Assert.Null(v.RedeemedOn);
    }

    [Fact]
    public void Redeem_AfterValidityEnd_Throws()
    {
        var v = CreateVoucher();

        Assert.Throws<DomainException>(() => v.Redeem(End.AddTicks(1)));
        Assert.Null(v.RedeemedOn);
    }

    [Fact]
    public void Redeem_AlreadyRedeemed_Throws()
    {
        var v = CreateVoucher();
        v.Redeem(Within);

        Assert.Throws<DomainException>(() => v.Redeem(Within.AddDays(1)));
        Assert.Equal(Within, v.RedeemedOn);
    }

    [Fact]
    public void Redeem_Canceled_Throws()
    {
        var v = CreateVoucher();
        v.Cancel(Within);

        Assert.Throws<DomainException>(() => v.Redeem(Within));
        Assert.Null(v.RedeemedOn);
    }

    [Fact]
    public void Cancel_ActiveVoucher_SetsCanceledOn()
    {
        // Arrange
        var v = CreateVoucher();
        // Act
        v.Cancel(Within);
        // Assert
        Assert.Equal(Within, v.CanceledOn);
    }

    [Fact]
    public void Cancel_AfterValidityEnd_IsAllowed()
    {
        var v = CreateVoucher();
        var afterEnd = End.AddDays(1);

        v.Cancel(afterEnd);

        Assert.Equal(afterEnd, v.CanceledOn);
    }

    [Fact]
    public void Cancel_AlreadyCanceled_Throws()
    {
        var v = CreateVoucher();
        v.Cancel(Within);

        Assert.Throws<DomainException>(() => v.Cancel(Within.AddDays(1)));
        Assert.Equal(Within, v.CanceledOn);
    }

    [Fact]
    public void Cancel_Redeemed_Throws()
    {
        var v = CreateVoucher();
        v.Redeem(Within);

        Assert.Throws<DomainException>(() => v.Cancel(Within));
        Assert.Null(v.CanceledOn);
    }

    #region private helpers
    private static Voucher CreateVoucher() => Voucher.Create(Start, End, 10m);
    #endregion
}
