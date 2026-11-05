namespace Application.Common.Exceptions;

/// <summary>The input of a use case is not valid (not a domain rule): mapped to 400 by the API.</summary>
public class ValidationException(string message) : Exception(message);
