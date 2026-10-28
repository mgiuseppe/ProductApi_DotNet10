using System.Collections.Concurrent;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Respawn;
using Respawn.Graph;
using Testcontainers.MsSql;

namespace Infrastructure.Tests;

/// <summary>
/// An empty, migrated SQL Server database owned by one test at a time.
/// - One container serves the whole test run: it starts on first use and the Testcontainers
///   resource reaper (Ryuk) removes it when the test process exits, even if the run crashes.
/// - Databases are pooled: CreateAsync takes a free one (or creates and migrates a new one) and DisposeAsync
///   deletes its rows with Respawn and gives it back. SQL Server runs CREATE DATABASE and RESTORE one at a time,
///   so a fresh database per test made the run several times slower; the pool only grows to the number of
///   tests running in parallel.
/// </summary>
internal sealed class TestDatabase : IAsyncDisposable
{
    private static readonly MsSqlContainer Container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-latest").Build();
    private static readonly Lazy<Task> Started = new(() => Container.StartAsync());
    private static readonly ConcurrentBag<TestDatabase> Pool = [];

    private static readonly RespawnerOptions ResetOptions = new()
    {
        TablesToIgnore = [new Table("__EFMigrationsHistory")], // keep the database migrated
    };

    private readonly string _name;
    private readonly Respawner? _respawner; // null for a database with no tables: it is dropped, not reused
    public string ConnectionString { get; }

    private TestDatabase(string name, Respawner? respawner)
    {
        _name = name;
        _respawner = respawner;
        ConnectionString = ConnectionStringFor(name);
    }

    /// <param name="migrated">false creates a database with no tables</param>
    public static async Task<TestDatabase> CreateAsync(bool migrated = true)
    {
        await Started.Value;
        if (migrated && Pool.TryTake(out var pooled))
            return pooled;

        var name = $"test_{Guid.NewGuid():N}";
        await ExecuteAsync($"CREATE DATABASE [{name}]");
        if (!migrated)
            return new TestDatabase(name, respawner: null);

        var options = new DbContextOptionsBuilder<AppDbContext>().UseAppDatabase(ConnectionStringFor(name)).Options;
        await using (var db = new AppDbContext(options))
            await db.Database.MigrateAsync();
        await using var connection = new SqlConnection(ConnectionStringFor(name));
        await connection.OpenAsync();
        return new TestDatabase(name, await Respawner.CreateAsync(connection, ResetOptions)); // reads the schema once
    }

    public DbContextOptions<AppDbContext> Options() =>
        new DbContextOptionsBuilder<AppDbContext>().UseAppDatabase(ConnectionString).Options;

    public async ValueTask DisposeAsync()
    {
        if (_respawner is not null)
        {
            await using (var connection = new SqlConnection(ConnectionString))
            {
                await connection.OpenAsync();
                await _respawner.ResetAsync(connection);
            }
            Pool.Add(this);
            return;
        }

        using (var connection = new SqlConnection(ConnectionString))
            SqlConnection.ClearPool(connection);
        // SINGLE_USER ... ROLLBACK IMMEDIATE closes any connection still open before the drop
        await ExecuteAsync($"ALTER DATABASE [{_name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_name}]");
    }

    private static string ConnectionStringFor(string database) =>
        new SqlConnectionStringBuilder(Container.GetConnectionString()) { InitialCatalog = database }.ConnectionString;

    // Runs on the default "master" database: CREATE/DROP DATABASE cannot target the database you are connected to
    private static async Task ExecuteAsync(string sql)
    {
        await using var connection = new SqlConnection(Container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
