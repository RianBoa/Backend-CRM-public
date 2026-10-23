using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Backend.IntegrationTests;

[CollectionDefinition("PostgreSQL")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;

public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? container;
    private LocalPostgres? local;
    private string connectionString = null!;

    public async Task InitializeAsync()
    {
        var binaries = Environment.GetEnvironmentVariable("FFA_TEST_POSTGRES_BIN");
        if (!string.IsNullOrWhiteSpace(binaries))
        {
            local = new LocalPostgres(binaries);
            await local.StartAsync();
            connectionString = local.ConnectionString;
            return;
        }
        container = new PostgreSqlBuilder().WithImage("postgres:17-alpine")
            .WithDatabase("postgres").WithUsername("postgres")
            .WithPassword(Guid.NewGuid().ToString("N")).Build();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        await container.StartAsync(timeout.Token);
        connectionString = container.GetConnectionString();
    }

    public async Task<PostgresDatabase> CreateDatabaseAsync()
    {
        // Names are generated here, never taken from configuration or an application database.
        var name = $"ffa_it_{Guid.NewGuid():N}";
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
        await command.ExecuteNonQueryAsync();
        var testConnection = new NpgsqlConnectionStringBuilder(connectionString) { Database = name }.ConnectionString;
        var database = new PostgresDatabase(testConnection);
        await using var db = database.CreateContext();
        await db.Database.MigrateAsync();
        return database;
    }

    // Disposing the owned container removes all per-test databases as well.
    public async Task DisposeAsync()
    {
        if (container is not null) await container.DisposeAsync();
        if (local is not null) await local.DisposeAsync();
    }
}

public sealed class PostgresDatabase(string connectionString)
{
    public ApplicationDbContext CreateContext(params Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor[] interceptors)
        => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connectionString).AddInterceptors(interceptors).Options);
}
