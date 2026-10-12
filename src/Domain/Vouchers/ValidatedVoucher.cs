namespace Domain.Vouchers;

public class ValidatedVoucher
{
    public Guid VoucherId { get; }
    public decimal Amount { get; }

    internal ValidatedVoucher(Guid voucherId, decimal amount)
    {
        VoucherId = voucherId;
        Amount = amount;
    }
}