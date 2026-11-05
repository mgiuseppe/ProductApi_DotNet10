using Application.Common.Exceptions;

namespace Application.Common;

/// <summary>Paging rules shared by the list queries.</summary>
public static class Paging
{
    /// <summary>Upper bound on the page size, so that one request cannot load a whole table.</summary>
    public const int MaxPageSize = 100;

    /// <summary>Throws a <see cref="ValidationException"/> with every invalid parameter, not only the first.</summary>
    public static void Validate(int page, int pageSize)
    {
        var errors = new Dictionary<string, string[]>();
        if (page < 1)
            errors["page"] = [$"must be at least 1, was {page}"];
        if (pageSize is < 1 or > MaxPageSize)
            errors["pageSize"] = [$"must be between 1 and {MaxPageSize}, was {pageSize}"];

        if (errors.Count > 0)
            throw new ValidationException(errors);
    }
}
