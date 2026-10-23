using backend.Domain.Orders;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Backend.Tests;

internal static class TestDatabase
{
    private static readonly Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot Root = new();
    public static ApplicationDbContext Create(string? databaseName = null) => new MemoryContext(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString(), Root).Options);

    private sealed class MemoryContext(DbContextOptions<ApplicationDbContext> options)
        : ApplicationDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // jsonb is not read by these scenarios; only PostgreSQL can verify that mapping.
            modelBuilder.Entity<OrderItem>().Ignore(x => x.Meta);
        }
    }
}
