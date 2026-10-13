using Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB.Driver.Core.Configuration;

namespace Infrastructure;

public static class DependencyInjection
{
    const string EFConnectionStringName = "DbConnection";
    const string MongoConnectionStringName = "MongoDb";

    // Extension method for IServiceCollection
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        // EF
        var csEF = configuration.GetConnectionString(EFConnectionStringName);
        if(string.IsNullOrEmpty(csEF))
            throw new InvalidOperationException($"Connection string '{EFConnectionStringName}' cannot be empty");   

        services.AddDbContext<AppDbContext>(options => options.UseSqlite(csEF));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IQueryContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IVoucherRepository, VoucherRepository>();

        // MONGO (MongoClient is thread safe and it is suggested to be injected as singleton)
        var csMongo = configuration.GetConnectionString(MongoConnectionStringName);
        if (string.IsNullOrEmpty(csMongo))
            throw new InvalidOperationException($"Connection string '{MongoConnectionStringName}' cannot be empty");
        var mongoUrl = MongoUrl.Create(csMongo);

        services.AddSingleton<IMongoClient>(sp =>
        {
            var settings = MongoClientSettings.FromUrl(mongoUrl);
            // Driver logs (commands, connections, server selection) go through the app ILoggerFactory, hence OpenTelemetry
            settings.LoggingSettings = new LoggingSettings(sp.GetRequiredService<ILoggerFactory>());
            return new MongoClient(settings);
        });
        services.AddSingleton<IMongoDatabase>(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(mongoUrl.DatabaseName ?? "productapi"));
        services.AddSingleton<IOrderHistoryLog, OrderHistoryLog>();
        OrderHistoryEntriesConfiguration.Register();

        // Health Checks
        services.AddHealthChecks()
                .AddDbContextCheck<AppDbContext>("database", tags: ["ready"])
                .AddMongoDb(dbFactory: sp => sp.GetRequiredService<IMongoDatabase>(), 
                            name: "mongodb", 
                            tags: ["ready"], 
                            failureStatus: HealthStatus.Degraded, // Not critic
                            timeout: TimeSpan.FromSeconds(5));
        return services;
    }

    public static async Task InitializeInfrastructureAsync(this IServiceProvider services)
    {
        var database = services.GetRequiredService<IMongoDatabase>();
        await OrderHistoryEntriesConfiguration.CreateIndexesAsync(database, CancellationToken.None);
    }
}