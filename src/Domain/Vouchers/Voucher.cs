using Domain.Exceptions;

namespace Domain.Vouchers;

public class Voucher
{
    public Guid Id { get; private set; }
    public DateTime ValidityStartDate { get; private set; }
    public DateTime ValidityEndDate { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime? RedeemedOn { get; private set; }
    public DateTime? CanceledOn { get; private set; }

    private Voucher()
    {
    }

    public static Voucher Create(DateTime validityStartDate, DateTime validityEndDate, decimal discountAmount)
    {
        validityStartDate = AsUtc(validityStartDate);
        validityEndDate = AsUtc(validityEndDate);
        if (validityStartDate >= validityEndDate)
            throw new DomainException("Validity start date must be before validity end date");
        if (discountAmount <= 0)
            throw new DomainException("Discount amount must be a positive value");

        return new Voucher
        {
            Id = Guid.NewGuid(),
            ValidityStartDate = validityStartDate,
            ValidityEndDate = validityEndDate,
            Amount = discountAmount,
            RedeemedOn = null,
            CanceledOn = null
        };
    }

    public void Redeem(DateTime now)
    {
        ValidateIsNotRedeemed();
        ValidateIsNotCanceled();
        if (now < ValidityStartDate || now > ValidityEndDate)
            throw new DomainException("Voucher is not valid at this date");

        RedeemedOn = now;
    }

    public void Cancel(DateTime now)
    {
        ValidateIsNotRedeemed();
        ValidateIsNotCanceled();
        CanceledOn = now;
    }

    #region private helpers
    // Dates are always UTC (Redeem/Cancel receive the UTC clock). A date without offset (e.g. JSON "2026-10-20") is assumed UTC.
    private static DateTime AsUtc(DateTime date) => date.Kind switch
    {
        DateTimeKind.Local => date.ToUniversalTime(),
        DateTimeKind.Unspecified => DateTime.SpecifyKind(date, DateTimeKind.Utc),
        _ => date
    };

    private void ValidateIsNotRedeemed()
    {
        if (RedeemedOn != null)
            throw new DomainException("Voucher has already been redeemed");
    }

    private void ValidateIsNotCanceled()
    {
        if (CanceledOn != null)
            throw new DomainException("Voucher has been canceled");
    }
    #endregion
}
