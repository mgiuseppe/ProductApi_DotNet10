using Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class DependencyInjection
{
    const string ConnectionStringName = "DbConnection";

    // Extension method for IServiceCollection
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        var cs = configuration.GetConnectionString(ConnectionStringName);
        if(string.IsNullOrEmpty(cs))
            throw new InvalidOperationException($"Connection string '{ConnectionStringName}' cannot be empty");   

        services.AddDbContext<AppDbContext>(options => options.UseSqlite(cs));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IQueryContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IVoucherRepository, VoucherRepository>();
        return services;
    }
}