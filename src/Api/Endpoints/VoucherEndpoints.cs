using Application.Vouchers;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Api.Endpoints;

public static class VoucherEndpoints
{
    public static WebApplication MapVoucherEndpoints(this WebApplication app)
    {
        var vouchers = app.MapGroup("/vouchers").WithTags("Vouchers");

        vouchers.MapGet("/{id:guid}", GetVoucherById).WithName(nameof(GetVoucherById));
        vouchers.MapPost("/", CreateVoucher).WithName(nameof(CreateVoucher))
            .ProducesProblem(StatusCodes.Status400BadRequest);
        vouchers.MapPost("/{id:guid}/cancel", CancelVoucher).WithName(nameof(CancelVoucher))
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    /// <summary>Get a voucher</summary>
    /// <response code="200">The voucher.</response>
    /// <response code="404">No voucher has the given id.</response>
    internal static async Task<Results<Ok<VoucherResponse>, NotFound>> GetVoucherById(
        Guid id, IMediator mediator, CancellationToken ct)
    {
        var voucher = await mediator.Send(new GetVoucherByIdQuery(id), ct);
        return voucher != null ? TypedResults.Ok(voucher) : TypedResults.NotFound();
    }

    /// <summary>Create a voucher</summary>
    /// <remarks>A voucher worth a fixed amount, usable once between the validity dates (UTC).</remarks>
    /// <response code="201">The voucher created; the Location header is its URL.</response>
    /// <response code="400">The start date is not before the end date, or the amount is not positive.</response>
    internal static async Task<CreatedAtRoute<VoucherResponse>> CreateVoucher(
        CreateVoucherCommand cmd, IMediator mediator, CancellationToken ct)
    {
        var voucher = await mediator.Send(cmd, ct);
        return TypedResults.CreatedAtRoute(voucher, nameof(GetVoucherById), new { id = voucher.Id });
    }

    /// <summary>Cancel a voucher</summary>
    /// <remarks>A canceled voucher can no longer be redeemed.</remarks>
    /// <response code="200">The voucher canceled.</response>
    /// <response code="400">The voucher has already been redeemed or canceled.</response>
    /// <response code="404">The voucher does not exist.</response>
    internal static async Task<Ok<VoucherResponse>> CancelVoucher(Guid id, IMediator mediator, CancellationToken ct)
    {
        return TypedResults.Ok(await mediator.Send(new CancelVoucherCommand(id), ct));
    }
}
