using BenchmarkDotNet.Attributes;
using DataShootout.Dapper;
using DataShootout.EfCore;
using DataShootout.Shared;
using DataShootout.Shared.Models;

namespace DataShootout.Benchmarks;

/// <summary>
/// The heart of the repo: identical workloads against both stores, measured
/// with BenchmarkDotNet (statistics + allocated memory per operation).
/// Both stores hit their own freshly seeded 5,000-row SQLite database, so
/// the only variable is EF Core vs Dapper.
/// </summary>
[MemoryDiagnoser]
public class StoreBenchmarks
{
    [Params("EFCore", "Dapper")]
    public string StoreKind { get; set; } = "EFCore";

    private IProductStore _store = null!;
    private string _dbFile = null!;
    private int _insertCounter;

    [GlobalSetup]
    public void Setup()
    {
        _dbFile = Path.Combine(
            Path.GetTempPath(), $"shootout-bench-{StoreKind}-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={_dbFile}";

        _store = StoreKind == "Dapper"
            ? new DapperProductStore(DbProvider.Sqlite, connectionString)
            : new EfCoreProductStore(DbProvider.Sqlite, connectionString);

        _store.InitializeAsync(seedCount: 5000).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _store.DisposeAsync().GetAwaiter().GetResult();
        if (File.Exists(_dbFile))
        {
            File.Delete(_dbFile);
        }
    }

    [Benchmark(Description = "Point lookup by primary key")]
    public Task<Product?> GetById() => _store.GetByIdAsync(1234);

    [Benchmark(Description = "Filter by category (~1,000 rows)")]
    public Task<IReadOnlyList<Product>> GetByCategory() =>
        _store.GetByCategoryAsync("Electronics");

    [Benchmark(Description = "Paginated read (page 3, 50 rows)")]
    public Task<IReadOnlyList<Product>> GetPaged() =>
        _store.GetPagedAsync(page: 3, pageSize: 50);

    [Benchmark(Description = "LIKE search on name")]
    public Task<IReadOnlyList<Product>> SearchByName() =>
        _store.SearchByNameAsync("Pro");

    [Benchmark(Description = "GROUP BY category with COUNT + AVG")]
    public Task<IReadOnlyList<CategorySummary>> CategorySummaries() =>
        _store.GetCategorySummariesAsync();

    [Benchmark(Description = "Single-row INSERT")]
    public Task<int> Insert()
    {
        var n = Interlocked.Increment(ref _insertCounter);
        return _store.InsertAsync(new Product
        {
            Name = $"Benchmark Widget {n}",
            Category = "Electronics",
            Price = 99.99m,
            Stock = 10
        });
    }
}
