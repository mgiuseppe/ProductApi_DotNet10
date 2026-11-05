using Application.Common;
using Application.Common.Exceptions;

namespace Application.Tests;

public class PagingTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, Paging.MaxPageSize)]
    [InlineData(5, 10)]
    public void Validate_ValidPageAndPageSize_DoesNotThrow(int page, int pageSize)
    {
        // Arrange (nothing to arrange)

        // Act
        var exception = Record.Exception(() => Paging.Validate(page, pageSize));

        // Assert
        Assert.Null(exception);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_PageLowerThanOne_ThrowsValidationException(int page)
    {
        // Arrange (nothing to arrange)

        // Act
        var exception = Record.Exception(() => Paging.Validate(page, pageSize: 10));

        // Assert
        Assert.IsType<ValidationException>(exception);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(Paging.MaxPageSize + 1)]
    public void Validate_PageSizeOutOfRange_ThrowsValidationException(int pageSize)
    {
        // Arrange (nothing to arrange)

        // Act
        var exception = Record.Exception(() => Paging.Validate(page: 1, pageSize));

        // Assert
        Assert.IsType<ValidationException>(exception);
    }
}
