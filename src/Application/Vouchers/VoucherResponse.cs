using Domain.Vouchers;

namespace Application.Vouchers;

/// <param name="ValidityStartDate">First moment the voucher can be redeemed, included (UTC).</param>
/// <param name="ValidityEndDate">Last moment the voucher can be redeemed, included (UTC).</param>
/// <param name="Amount">Fixed discount subtracted from the order subtotal, not a percentage.</param>
/// <param name="RedeemedOn">When an order redeemed the voucher (UTC); null if not redeemed.</param>
/// <param name="CanceledOn">When the voucher was canceled (UTC); null if not canceled.</param>
public record VoucherResponse(Guid Id,
                              DateTime ValidityStartDate,
                              DateTime ValidityEndDate,
                              decimal Amount,
                              DateTime? RedeemedOn,
                              DateTime? CanceledOn)
{
    public static VoucherResponse FromDomain(Voucher v)
    {
        return new VoucherResponse(v.Id,
                                   v.ValidityStartDate,
                                   v.ValidityEndDate,
                                   v.Amount,
                                   v.RedeemedOn,
                                   v.CanceledOn);
    }
}
