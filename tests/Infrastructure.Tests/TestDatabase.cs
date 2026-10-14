using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Infrastructure.Tests;

/// <summary>
/// A throwaway Postgres database, created by a test and dropped when the test ends.
/// - One container serves the whole test run: it starts on first use and the Testcontainers
///   resource reaper (Ryuk) removes it when the test process exits, even if the run crashes.
/// - Migrations are applied once to a template database; each test clones it (CREATE DATABASE ... TEMPLATE),
///   which is much faster than migrating every time and lets test classes run in parallel.
/// </summary>
internal sealed class TestDatabase : IAsyncDisposable
{
    private const string TemplateName = "productapi_template";
    private static readonly PostgreSqlContainer Container = new PostgreSqlBuilder("postgres:18").Build();
    private static readonly Lazy<Task> Ready = new(StartAndMigrateTemplateAsync);

    private readonly string _name;
    public string ConnectionString { get; }

    private TestDatabase(string name)
    {
        _name = name;
        ConnectionString = ConnectionStringFor(name);
    }

    /// <param name="migrated">false creates a database with no tables</param>
    public static async Task<TestDatabase> CreateAsync(bool migrated = true)
    {
        await Ready.Value;
        var name = $"test_{Guid.NewGuid():N}";
        await ExecuteAsync(migrated ? $"CREATE DATABASE {name} TEMPLATE {TemplateName}" : $"CREATE DATABASE {name}");
        return new TestDatabase(name);
    }

    public DbContextOptions<AppDbContext> Options() =>
        new DbContextOptionsBuilder<AppDbContext>().UseAppDatabase(ConnectionString).Options;

    public async ValueTask DisposeAsync()
    {
        using (var connection = new NpgsqlConnection(ConnectionString))
            NpgsqlConnection.ClearPool(connection); // idle pooled connections would be killed by the drop anyway
        await ExecuteAsync($"DROP DATABASE {_name} WITH (FORCE)");
    }

    private static async Task StartAndMigrateTemplateAsync()
    {
        await Container.StartAsync();
        await ExecuteAsync($"CREATE DATABASE {TemplateName}");
        var options = new DbContextOptionsBuilder<AppDbContext>().UseAppDatabase(ConnectionStringFor(TemplateName)).Options;
        await using (var db = new AppDbContext(options))
            await db.Database.MigrateAsync();
        NpgsqlConnection.ClearAllPools(); // a template cannot be cloned while a connection to it is open
    }

    private static string ConnectionStringFor(string database) =>
        new NpgsqlConnectionStringBuilder(Container.GetConnectionString()) { Database = database }.ConnectionString;

    // Runs on the default "postgres" database: CREATE/DROP DATABASE cannot target the database you are connected to
    private static async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(Container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
