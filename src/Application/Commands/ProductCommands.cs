namespace Application.Commands;

public record CreateProductCommand(string Name, decimal Price, string? Description);
public record UpdateProductCommand(string Name, decimal Price, string? Description);
