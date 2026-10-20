using DataShootout.Shared;
using DataShootout.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace DataShootout.EfCore;

/// <summary>
/// Minimal EF Core context: one entity, explicit table mapping, and a small
/// index on (Category) since two of the benchmarked queries filter by it.
/// </summary>
public sealed class ShopContext : DbContext
{
    public DbSet<Product> Products => Set<Product>();

    public ShopContext(DbContextOptions<ShopContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("Products");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).HasMaxLength(200).IsRequired();
            entity.Property(p => p.Category).HasMaxLength(100).IsRequired();
            entity.Property(p => p.Price).HasPrecision(18, 2);
            entity.HasIndex(p => p.Category);
        });
    }

    /// <summary>
    /// Options for whichever provider backs the run. Used by the store, the
    /// API, the tests and the benchmarks — one factory, no duplication.
    /// </summary>
    public static DbContextOptions<ShopContext> CreateOptions(
        DbProvider provider, string connectionString) =>
        new DbContextOptionsBuilder<ShopContext>()
            .UseProvider(provider, connectionString)
            .Options;
}

internal static class DbContextOptionsBuilderExtensions
{
    internal static DbContextOptionsBuilder<ShopContext> UseProvider(
        this DbContextOptionsBuilder<ShopContext> builder,
        DbProvider provider,
        string connectionString) => provider switch
    {
        DbProvider.SqlServer => builder.UseSqlServer(connectionString),
        _ => builder.UseSqlite(connectionString)
    };
}
