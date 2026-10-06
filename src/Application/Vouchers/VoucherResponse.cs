using Domain;

namespace Application.Vouchers;

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
