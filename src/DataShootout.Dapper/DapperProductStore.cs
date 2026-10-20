using System.Data;
using Dapper;
using DataShootout.Shared;
using DataShootout.Shared.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;

namespace DataShootout.Dapper;

/// <summary>
/// The Dapper implementation of <see cref="IProductStore"/>.
/// Hand-written, parameterized SQL — the thing Dapper is for. One connection
/// per operation (opened late, closed early), which mirrors how Dapper is
/// used in real services. The SQL is written to run on BOTH SQLite and SQL
/// Server; the two places the dialects genuinely differ (DDL and
/// last-insert-id) branch on <see cref="DbProvider"/>.
/// </summary>
public sealed class DapperProductStore : IProductStore
{
    private readonly DbProvider _provider;
    private readonly string _connectionString;

    // SQLite's dynamic typing vs Dapper's per-shape deserializers: register
    // the decimal bridge once per process (see SqliteDecimalHandler).
    static DapperProductStore() => SqliteDecimalHandler.Register();

    public DapperProductStore(DbProvider provider, string connectionString)
    {
        _provider = provider;
        _connectionString = connectionString;
    }

    private IDbConnection OpenConnection()
    {
        IDbConnection connection = _provider switch
        {
            DbProvider.SqlServer => new SqlConnection(_connectionString),
            _ => new SqliteConnection(_connectionString)
        };
        connection.Open();
        return connection;
    }

    public async Task InitializeAsync(int seedCount = 1000)
    {
        using var connection = OpenConnection();

        var ddl = _provider switch
        {
            DbProvider.SqlServer => """
                IF OBJECT_ID('dbo.Products', 'U') IS NULL
                CREATE TABLE dbo.Products (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Name NVARCHAR(200) NOT NULL,
                    Category NVARCHAR(100) NOT NULL,
                    Price DECIMAL(18,2) NOT NULL,
                    Stock INT NOT NULL
                );
                """,
            _ => """
                CREATE TABLE IF NOT EXISTS Products (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Category TEXT NOT NULL,
                    -- TEXT, not NUMERIC: matches EF Core's SQLite decimal mapping.
                    -- NUMERIC affinity would coerce '315.12' to REAL and the
                    -- exact decimal round-trip would be lost.
                    Price TEXT NOT NULL,
                    Stock INTEGER NOT NULL
                );
                """
        };
        await connection.ExecuteAsync(ddl);

        var count = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Products");
        if (count == 0)
        {
            const string insert =
                "INSERT INTO Products (Name, Category, Price, Stock) " +
                "VALUES (@Name, @Category, @Price, @Stock);";
            await connection.ExecuteAsync(insert, SeedData.Products(seedCount));
        }
    }

    public async Task<Product?> GetByIdAsync(int id)
    {
        using var connection = OpenConnection();
        return await connection.QuerySingleOrDefaultAsync<Product>(
            "SELECT Id, Name, Category, Price, Stock FROM Products WHERE Id = @Id;",
            new { Id = id });
    }

    public async Task<IReadOnlyList<Product>> GetByCategoryAsync(string category)
    {
        using var connection = OpenConnection();
        var rows = await connection.QueryAsync<Product>(
            """
            SELECT Id, Name, Category, Price, Stock FROM Products
            WHERE Category = @Category ORDER BY Id;
            """,
            new { Category = category });
        return rows.AsList();
    }

    public async Task<IReadOnlyList<Product>> GetPagedAsync(int page, int pageSize)
    {
        using var connection = OpenConnection();
        // Pagination syntax differs: T-SQL uses OFFSET/FETCH, SQLite uses
        // LIMIT/OFFSET (it does NOT understand the FETCH NEXT syntax).
        var sql = _provider switch
        {
            DbProvider.SqlServer => """
                SELECT Id, Name, Category, Price, Stock FROM Products
                ORDER BY Id OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;
                """,
            _ => """
                SELECT Id, Name, Category, Price, Stock FROM Products
                ORDER BY Id LIMIT @Take OFFSET @Skip;
                """
        };
        var rows = await connection.QueryAsync<Product>(
            sql, new { Skip = (page - 1) * pageSize, Take = pageSize });
        return rows.AsList();
    }

    public async Task<IReadOnlyList<Product>> SearchByNameAsync(string term)
    {
        using var connection = OpenConnection();
        // Wildcards go in the parameter, not the SQL: same text works on
        // SQLite and SQL Server, and stays safely parameterized.
        var rows = await connection.QueryAsync<Product>(
            """
            SELECT Id, Name, Category, Price, Stock FROM Products
            WHERE Name LIKE @Pattern ORDER BY Id;
            """,
            new { Pattern = $"%{term}%" });
        return rows.AsList();
    }

    public async Task<IReadOnlyList<CategorySummary>> GetCategorySummariesAsync()
    {
        using var connection = OpenConnection();
        var rows = await connection.QueryAsync<CategorySummary>(
            """
            SELECT Category, COUNT(*) AS Count, AVG(Price) AS AvgPrice
            FROM Products GROUP BY Category ORDER BY Category;
            """);
        return rows.AsList();
    }

    public async Task<int> InsertAsync(Product product)
    {
        using var connection = OpenConnection();
        var sql = _provider switch
        {
            DbProvider.SqlServer =>
                "INSERT INTO Products (Name, Category, Price, Stock) " +
                "VALUES (@Name, @Category, @Price, @Stock); " +
                "SELECT CAST(SCOPE_IDENTITY() AS INT);",
            _ =>
                "INSERT INTO Products (Name, Category, Price, Stock) " +
                "VALUES (@Name, @Category, @Price, @Stock); " +
                "SELECT last_insert_rowid();"
        };
        return await connection.ExecuteScalarAsync<int>(sql, product);
    }

    public async Task UpdatePriceAsync(int id, decimal price)
    {
        using var connection = OpenConnection();
        await connection.ExecuteAsync(
            "UPDATE Products SET Price = @Price WHERE Id = @Id;",
            new { Id = id, Price = price });
    }

    public async Task DeleteAsync(int id)
    {
        using var connection = OpenConnection();
        await connection.ExecuteAsync(
            "DELETE FROM Products WHERE Id = @Id;",
            new { Id = id });
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
