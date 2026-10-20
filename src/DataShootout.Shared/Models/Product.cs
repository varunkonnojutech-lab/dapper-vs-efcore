namespace DataShootout.Shared.Models;

/// <summary>
/// A single row in the Products table. Both the EF Core and Dapper
/// stores map to this exact same shape, so query results are comparable.
/// </summary>
public sealed class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
}
