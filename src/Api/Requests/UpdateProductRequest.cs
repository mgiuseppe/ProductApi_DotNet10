namespace Api.Requests;

public record UpdateProductRequest(string Name, decimal Price, string? Description);