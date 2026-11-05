namespace Application.Common.Exceptions;

/// <summary>
/// The input of a use case is not valid (not a domain rule). Carries every error found, by field, so the client
/// gets them all at once: mapped to a 400 ValidationProblemDetails by the API.
/// </summary>
public class ValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("One or more validation errors occurred.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
