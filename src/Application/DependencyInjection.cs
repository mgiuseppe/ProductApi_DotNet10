using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class DependencyInjection
{
    // Extension method for IServiceCollection
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IOrderService,OrderService>();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        return services;
    }
}