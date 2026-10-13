using System.Diagnostics.Metrics;

namespace Application.Orders;

/// <summary>
/// Example of custom metrics exported with Open Telemetry
/// Note: 
///     These metrics should NOT be used to count the exact number of orders confirmed / voucher conflicts, you should use the database for that.
///     These metrics should be Used to spot trends and anomalies (unusually high number of conflicts or abnormal number of confirmations).
///     These counters live in-process and are exported periodically to the OpenTelemetry backend (e.g. azure monitor). 
///     History is kept by the backend and a restart resets the in-process value.
/// </summary>
public sealed class OrderMetrics
{
    public const string MeterName = "ProductApi.Orders";

    private readonly Counter<long> _confirmed;
    private readonly Counter<long> _voucherConflicts;

    public OrderMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _confirmed = meter.CreateCounter<long>(
            "orders.confirmed",
            unit: "{order}",
            description: "Orders successfully confirmed"
        );
        _voucherConflicts = meter.CreateCounter<long>(
            "vouchers.redemption.concurrent_conflicts",
            unit: "{conflict}",
            description: "Voucher redemptions casued 409 (unique index) because two orders tried to redeem the same voucher at the same time"
        );
    }

    public void OrderConfirmed(bool withVoucher) =>
        _confirmed.Add(1, new KeyValuePair<string, object?>("with_voucher", withVoucher));

    public void VoucherRedemptionConcurrentConflict() => _voucherConflicts.Add(1);
}