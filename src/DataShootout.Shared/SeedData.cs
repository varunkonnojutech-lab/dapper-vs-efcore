using DataShootout.Shared.Models;

namespace DataShootout.Shared;

/// <summary>
/// Deterministic seed data: same input for both stores, so parity tests and
/// benchmarks compare apples to apples. Seed is fixed (42) so results are
/// reproducible across machines.
/// </summary>
public static class SeedData
{
    private static readonly string[] Categories =
        ["Electronics", "Books", "Clothing", "Grocery", "Toys"];

    private static readonly string[] Adjectives =
        ["Pro", "Max", "Ultra", "Lite", "Prime", "Eco", "Smart", "Classic"];

    private static readonly string[] Nouns =
        ["Widget", "Gadget", "Device", "Kit", "Pack", "Tool", "Set", "Bundle"];

    public static IReadOnlyList<Product> Products(int count)
    {
        var random = new Random(42);
        var products = new List<Product>(count);

        for (var i = 0; i < count; i++)
        {
            var category = Categories[i % Categories.Length];
            products.Add(new Product
            {
                Name = $"{Adjectives[random.Next(Adjectives.Length)]} " +
                       $"{Nouns[random.Next(Nouns.Length)]} {i + 1}",
                Category = category,
                Price = Math.Round((decimal)(random.NextDouble() * 990 + 10), 2),
                Stock = random.Next(0, 500)
            });
        }

        return products;
    }

    public static string ConnectionString(DbProvider provider) => provider switch
    {
        DbProvider.SqlServer =>
            Environment.GetEnvironmentVariable("SQLSERVER_CONNECTION") ??
            "Server=localhost,1433;Database=Shootout;User Id=sa;Password=SqlShootout!2026;TrustServerCertificate=True",
        _ => "Data Source=shootout.db"
    };
}
