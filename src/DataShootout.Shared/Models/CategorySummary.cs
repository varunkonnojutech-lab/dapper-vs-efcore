namespace DataShootout.Shared.Models;

/// <summary>
/// Result of the "aggregate per category" query: GROUP BY Category with
/// COUNT(*) and AVG(Price). Implemented with LINQ on the EF Core side and
/// raw SQL on the Dapper side.
/// <para/>
/// A class with settable properties (not a positional record) on purpose:
/// Dapper only constructor-maps when parameter types match the column types
/// <em>exactly</em> (SQLite reports COUNT(*) as long and AVG() as double),
/// while property setters get its flexible per-value conversion.
/// </summary>
public sealed class CategorySummary
{
    public string Category { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal AvgPrice { get; set; }
}
