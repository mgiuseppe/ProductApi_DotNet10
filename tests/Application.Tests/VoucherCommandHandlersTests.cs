using Application.Exceptions;
using Application.Vouchers;
using Domain;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Application.Tests;

public class VoucherCommandHandlersTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IVoucherRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeTimeProvider _timeProvider = new(Now);

    [Fact]
    public async Task CreateVoucher_AddsVoucherAndSaves()
    {
        var cmd = new CreateVoucherCommand(Now, Now.AddDays(30), 10m);
        Voucher? added = null;
        _repository
            .Setup(r => r.AddAsync(It.IsAny<Voucher>(), It.IsAny<CancellationToken>()))
            .Callback<Voucher, CancellationToken>((v, _) => added = v);
        var sut = new CreateVoucherCommandHandler(_repository.Object, _unitOfWork.Object);

        var result = await sut.Handle(cmd, CancellationToken.None);

        Assert.NotNull(added);
        Assert.Equal(added.Id, result.Id);
        Assert.Equal(cmd.ValidityStartDate, result.ValidityStartDate);
        Assert.Equal(cmd.ValidityEndDate, result.ValidityEndDate);
        Assert.Equal(10m, result.Amount);
        Assert.Null(result.RedeemedOn);
        Assert.Null(result.CanceledOn);
        _repository.Verify(r => r.AddAsync(It.IsAny<Voucher>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelVoucher_WithNonExistentId_ThrowsNotFoundException()
    {
        var id = Guid.NewGuid();
        _repository
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Voucher?)null);
        var sut = new CancelVoucherCommandHandler(_repository.Object, _unitOfWork.Object, _timeProvider);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sut.Handle(new CancelVoucherCommand(id), CancellationToken.None));

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelVoucher_WithExistingVoucher_CancelsAtCurrentTimeAndSaves()
    {
        var voucher = Voucher.Create(Now.AddDays(-1), Now.AddDays(30), 10m);
        _repository
            .Setup(r => r.GetByIdAsync(voucher.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(voucher);
        var sut = new CancelVoucherCommandHandler(_repository.Object, _unitOfWork.Object, _timeProvider);

        var result = await sut.Handle(new CancelVoucherCommand(voucher.Id), CancellationToken.None);

        Assert.Equal(Now, result.CanceledOn);
        Assert.Equal(Now, voucher.CanceledOn);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
