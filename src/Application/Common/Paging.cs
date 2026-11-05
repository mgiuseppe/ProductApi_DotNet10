using Application.Common.Exceptions;

namespace Application.Common;

/// <summary>Paging rules shared by the list queries.</summary>
public static class Paging
{
    /// <summary>Upper bound on the page size, so that one request cannot load a whole table.</summary>
    public const int MaxPageSize = 100;

    public static void Validate(int page, int pageSize)
    {
        if (page < 1)
            throw new ValidationException($"page must be at least 1, was {page}");
        if (pageSize is < 1 or > MaxPageSize)
            throw new ValidationException($"pageSize must be between 1 and {MaxPageSize}, was {pageSize}");
    }
}
