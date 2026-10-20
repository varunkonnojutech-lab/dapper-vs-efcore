using DataShootout.Dapper;
using DataShootout.EfCore;
using DataShootout.Shared;
using DataShootout.Shared.Models;

// A tiny comparison API: every read endpoint exists twice, once backed by
// EF Core (/efcore/...) and once by Dapper (/dapper/...). Same contract,
// same data — hit both and compare the JSON and the response times.
//
// Run:  dotnet run --project src/DataShootout.Api
// Then: curl http://localhost:5000/efcore/products/1
//       curl http://localhost:5000/dapper/products/1

var provider = Environment.GetEnvironmentVariable("USE_SQLSERVER") == "1"
    ? DbProvider.SqlServer
    : DbProvider.Sqlite;
var connectionString = SeedData.ConnectionString(provider);

await using var efCore = new EfCoreProductStore(provider, connectionString);
await using var dapper = new DapperProductStore(provider, connectionString);
await efCore.InitializeAsync();
await dapper.InitializeAsync();

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () =>
    "EF Core vs Dapper shootout — try /efcore/products/1 and /dapper/products/1");

RouteGroupBuilder MapStore(string prefix, IProductStore store)
{
    var group = app.MapGroup(prefix);

    group.MapGet("/products/{id:int}", (int id) => store.GetByIdAsync(id));
    group.MapGet("/products/category/{category}", (string category) =>
        store.GetByCategoryAsync(category));
    group.MapGet("/products/paged", (int page = 1, int pageSize = 20) =>
        store.GetPagedAsync(page, pageSize));
    group.MapGet("/products/search", (string q) => store.SearchByNameAsync(q));
    group.MapGet("/products/summaries", () => store.GetCategorySummariesAsync());

    group.MapPost("/products", async (Product product) =>
    {
        var id = await store.InsertAsync(product);
        return Results.Created($"{prefix}/products/{id}", new { id });
    });

    group.MapPut("/products/{id:int}/price", async (int id, decimal price) =>
    {
        await store.UpdatePriceAsync(id, price);
        return Results.NoContent();
    });

    group.MapDelete("/products/{id:int}", async (int id) =>
    {
        await store.DeleteAsync(id);
        return Results.NoContent();
    });

    return group;
}

MapStore("/efcore", efCore);
MapStore("/dapper", dapper);

app.Run();
