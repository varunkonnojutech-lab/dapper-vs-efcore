using DataShootout.Shared.Models;

namespace DataShootout.Shared;

/// <summary>
/// The contract BOTH stores implement. Same method names, same shapes, same
/// semantics — so the only thing the benchmarks measure is the difference
/// between EF Core and Dapper themselves.
/// </summary>
public interface IProductStore : IAsyncDisposable
{
    /// <summary>Create the schema and seed data if the store is empty.</summary>
    Task InitializeAsync(int seedCount = 1000);

    Task<Product?> GetByIdAsync(int id);
    Task<IReadOnlyList<Product>> GetByCategoryAsync(string category);
    Task<IReadOnlyList<Product>> GetPagedAsync(int page, int pageSize);
    Task<IReadOnlyList<Product>> SearchByNameAsync(string term);
    Task<IReadOnlyList<CategorySummary>> GetCategorySummariesAsync();
    Task<int> InsertAsync(Product product);
    Task UpdatePriceAsync(int id, decimal price);
    Task DeleteAsync(int id);
}
