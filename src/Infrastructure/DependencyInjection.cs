using Application.Common.Interfaces;
using Infrastructure.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB.Driver.Core.Configuration;
using OpenAI;

namespace Infrastructure;

public static class DependencyInjection
{
    const string EFConnectionStringName = "DbConnection";
    const string MongoConnectionStringName = "MongoDb";

    // Extension method for IServiceCollection
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        AddEntityFrameworkAndRepositories(services, configuration);
        AddMongo(services, configuration);
        AddAI(services, configuration);

        // Health Checks
        services.AddHealthChecks()
                .AddDbContextCheck<AppDbContext>("database", tags: ["ready"])
                .AddMongoDb(dbFactory: sp => sp.GetRequiredService<IMongoDatabase>(),
                            name: "mongodb",
                            tags: ["ready"],
                            timeout: TimeSpan.FromSeconds(5));

        return services;
    }

    private static void AddEntityFrameworkAndRepositories(IServiceCollection services, IConfiguration configuration)
    {
        var csEF = configuration.GetConnectionString(EFConnectionStringName);
        if (string.IsNullOrEmpty(csEF))
            throw new InvalidOperationException($"Connection string '{EFConnectionStringName}' cannot be empty");

        services.AddScoped<DispatchDomainEventsInterceptor>();
        services.AddDbContext<AppDbContext>((sp, options) =>
                        options.UseAppDatabase(csEF)
                               .AddInterceptors(sp.GetRequiredService<DispatchDomainEventsInterceptor>()));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IQueryContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IVoucherRepository, VoucherRepository>();
    }

    private static void AddMongo(IServiceCollection services, IConfiguration configuration)
    {
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
    }

    private static void AddAI(IServiceCollection services, IConfiguration configuration)
    {
        var openAIConfig = configuration.GetSection("OpenAI");
        // Built on first use: OpenAIClient throws without a key, and hosts that never chat (e.g. the migrator bundle) don't have one
        services.AddChatClient(_ => new OpenAIClient(openAIConfig["ApiKey"])
                                        .GetChatClient(openAIConfig["Model"])
                                        .AsIChatClient())
                .UseOpenTelemetry(sourceName: AITelemetry.SourceName);
    }

    // Shared with the integration tests, so they build the same model (provider + naming convention) as the app
    internal static TBuilder UseAppDatabase<TBuilder>(this TBuilder options, string connectionString)
        where TBuilder : DbContextOptionsBuilder
    {
        options.UseNpgsql(connectionString)
               .UseSnakeCaseNamingConvention(); // order_lines.unit_price_snapshot: no quoting needed in raw SQL
        return options;
    }

    /// <summary>
    /// Initialize MongoDB indexes
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static async Task InitializeInfrastructureAsync(this IServiceProvider services)
    {
        var database = services.GetRequiredService<IMongoDatabase>();
        await OrderHistoryEntriesConfiguration.CreateIndexesAsync(database, CancellationToken.None);
    }
}