using Application.Orders;
using Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class DependencyInjection
{
    // Extension method for IServiceCollection
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ProductLookup>();
        services.AddSingleton<VoucherRedemptionService>();
        services.AddSingleton(TimeProvider.System);
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        return services;
    }
}