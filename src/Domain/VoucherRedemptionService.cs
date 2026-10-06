using Domain.Exceptions;

namespace Domain;

/// <summary>
/// Single entry point for applying a voucher to an order: redeeming the voucher and confirming the order
/// happen together, so neither aggregate can end updated without the other.
/// </summary>
public class VoucherRedemptionService
{
    public void ConfirmWithVoucher(Order order, Voucher voucher, DateTime now)
    {
        // Order.Confirm enforces this too; checking here fails before the voucher is redeemed
        if (voucher.Amount > order.SubTotal)
            throw new DomainException("Voucher amount exceeds the order subtotal");

        voucher.Redeem(now);
        order.Confirm(new ValidatedVoucher(voucher.Id, voucher.Amount));
    }
}
