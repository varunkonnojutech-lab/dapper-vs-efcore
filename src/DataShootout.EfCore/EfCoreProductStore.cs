using DataShootout.Shared;
using DataShootout.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace DataShootout.EfCore;

/// <summary>
/// The EF Core implementation of <see cref="IProductStore"/>.
/// Idiomatic LINQ, short-lived contexts (one per operation — the recommended
/// pattern for web apps), <c>AsNoTracking</c> on every read-only query so we
/// measure query execution, not change-tracker overhead.
/// </summary>
public sealed class EfCoreProductStore : IProductStore
{
    private readonly DbContextOptions<ShopContext> _options;

    public EfCoreProductStore(DbProvider provider, string connectionString) =>
        _options = ShopContext.CreateOptions(provider, connectionString);

    public async Task InitializeAsync(int seedCount = 1000)
    {
        await using var db = new ShopContext(_options);
        await db.Database.EnsureCreatedAsync();

        if (!await db.Products.AnyAsync())
        {
            db.Products.AddRange(SeedData.Products(seedCount));
            await db.SaveChangesAsync();
        }
    }

    public async Task<Product?> GetByIdAsync(int id)
    {
        await using var db = new ShopContext(_options);
        return await db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<IReadOnlyList<Product>> GetByCategoryAsync(string category)
    {
        await using var db = new ShopContext(_options);
        return await db.Products
            .AsNoTracking()
            .Where(p => p.Category == category)
            .OrderBy(p => p.Id)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Product>> GetPagedAsync(int page, int pageSize)
    {
        await using var db = new ShopContext(_options);
        return await db.Products
            .AsNoTracking()
            .OrderBy(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Product>> SearchByNameAsync(string term)
    {
        await using var db = new ShopContext(_options);
        // EF.Functions.Like keeps the LIKE server-side on both providers.
        return await db.Products
            .AsNoTracking()
            .Where(p => EF.Functions.Like(p.Name, $"%{term}%"))
            .OrderBy(p => p.Id)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<CategorySummary>> GetCategorySummariesAsync()
    {
        await using var db = new ShopContext(_options);
        // SQLite can't AVG() a decimal server-side, so average as double —
        // which is what SQL AVG() returns natively anyway — and convert back.
        // (Dapper's side sees the same double via its decimal handler.)
        var rows = await db.Products
            .AsNoTracking()
            .GroupBy(p => p.Category)
            .Select(g => new
            {
                Category = g.Key,
                Count = g.Count(),
                AvgPrice = g.Average(p => (double)p.Price)
            })
            .OrderBy(x => x.Category)
            .ToListAsync();

        return rows
            .Select(x => new CategorySummary
            {
                Category = x.Category,
                Count = x.Count,
                AvgPrice = (decimal)x.AvgPrice
            })
            .ToList();
    }

    public async Task<int> InsertAsync(Product product)
    {
        await using var db = new ShopContext(_options);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product.Id;
    }

    public async Task UpdatePriceAsync(int id, decimal price)
    {
        await using var db = new ShopContext(_options);
        // ExecuteUpdate: one UPDATE statement, no entity load — the fast path.
        await db.Products
            .Where(p => p.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Price, price));
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = new ShopContext(_options);
        await db.Products
            .Where(p => p.Id == id)
            .ExecuteDeleteAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
