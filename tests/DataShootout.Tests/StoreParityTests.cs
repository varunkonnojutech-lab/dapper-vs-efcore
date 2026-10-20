using DataShootout.Dapper;
using DataShootout.EfCore;
using DataShootout.Shared;
using DataShootout.Shared.Models;
using Xunit;

namespace DataShootout.Tests;

/// <summary>
/// Parity tests: both stores implement <see cref="IProductStore"/> against
/// the same deterministic seed data, so every read must return identical
/// results from EF Core and Dapper. If these fail, the benchmark comparison
/// is meaningless — fix the stores first.
/// </summary>
public sealed class StoreParityTests : IDisposable
{
    private readonly List<string> _dbFiles = [];

    private async Task<IProductStore> CreateStoreAsync(string kind, int seedCount = 200)
    {
        var dbFile = Path.Combine(Path.GetTempPath(), $"shootout-test-{Guid.NewGuid():N}.db");
        _dbFiles.Add(dbFile);
        var connectionString = $"Data Source={dbFile}";

        IProductStore store = kind == "Dapper"
            ? new DapperProductStore(DbProvider.Sqlite, connectionString)
            : new EfCoreProductStore(DbProvider.Sqlite, connectionString);

        await store.InitializeAsync(seedCount);
        return store;
    }

    public void Dispose()
    {
        foreach (var file in _dbFiles)
        {
            try { File.Delete(file); } catch { /* best effort */ }
        }
    }

    [Theory]
    [InlineData("EFCore")]
    [InlineData("Dapper")]
    public async Task GetById_ReturnsSeededProduct(string kind)
    {
        await using var store = await CreateStoreAsync(kind);

        var product = await store.GetByIdAsync(1);

        Assert.NotNull(product);
        Assert.Equal(1, product.Id);
        Assert.False(string.IsNullOrWhiteSpace(product.Name));
        Assert.Contains(product.Category, new HashSet<string>
            { "Electronics", "Books", "Clothing", "Grocery", "Toys" });
    }

    [Theory]
    [InlineData("EFCore")]
    [InlineData("Dapper")]
    public async Task GetById_ReturnsNullForMissingId(string kind)
    {
        await using var store = await CreateStoreAsync(kind);

        Assert.Null(await store.GetByIdAsync(999_999));
    }

    [Fact]
    public async Task BothStores_ReturnIdenticalQueryResults()
    {
        await using var efCore = await CreateStoreAsync("EFCore");
        await using var dapper = await CreateStoreAsync("Dapper");

        var efByCategory = await efCore.GetByCategoryAsync("Electronics");
        var dpByCategory = await dapper.GetByCategoryAsync("Electronics");
        Assert.Equal(40, efByCategory.Count); // 200 seeded / 5 categories
        // Prices compared at 2 decimals: SQLite has no decimal type, so the
        // two stacks may take slightly different double<->decimal paths.
        Assert.Equal(
            efByCategory.Select(p => (p.Name, Math.Round(p.Price, 2), p.Stock)),
            dpByCategory.Select(p => (p.Name, Math.Round(p.Price, 2), p.Stock)));

        var efPaged = await efCore.GetPagedAsync(2, 25);
        var dpPaged = await dapper.GetPagedAsync(2, 25);
        Assert.Equal(efPaged.Select(p => p.Id), dpPaged.Select(p => p.Id));

        var efSearch = await efCore.SearchByNameAsync("Widget");
        var dpSearch = await dapper.SearchByNameAsync("Widget");
        Assert.NotEmpty(efSearch);
        Assert.Equal(efSearch.Select(p => p.Id), dpSearch.Select(p => p.Id));

        var efSummaries = await efCore.GetCategorySummariesAsync();
        var dpSummaries = await dapper.GetCategorySummariesAsync();
        Assert.Equal(5, efSummaries.Count);
        Assert.Equal(
            efSummaries.Select(s => (s.Category, s.Count, Math.Round(s.AvgPrice, 2))),
            dpSummaries.Select(s => (s.Category, s.Count, Math.Round(s.AvgPrice, 2))));
    }

    [Theory]
    [InlineData("EFCore")]
    [InlineData("Dapper")]
    public async Task Crud_RoundTrip(string kind)
    {
        await using var store = await CreateStoreAsync(kind);

        var id = await store.InsertAsync(new Product
        {
            Name = "Roundtrip Widget",
            Category = "Toys",
            Price = 19.99m,
            Stock = 7
        });
        Assert.True(id > 0);

        var inserted = await store.GetByIdAsync(id);
        Assert.NotNull(inserted);
        Assert.Equal(19.99m, Math.Round(inserted.Price, 2));

        await store.UpdatePriceAsync(id, 24.99m);
        var updated = await store.GetByIdAsync(id);
        Assert.NotNull(updated);
        Assert.Equal(24.99m, Math.Round(updated.Price, 2));

        await store.DeleteAsync(id);
        Assert.Null(await store.GetByIdAsync(id));
    }
}
