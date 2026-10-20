using System.Data;
using System.Globalization;
using Dapper;

namespace DataShootout.Dapper;

/// <summary>
/// SQLite has no DECIMAL type and is dynamically typed: the same column can
/// yield <see cref="double"/> for 315.12 and <see cref="long"/> for 420, row
/// by row. Dapper generates one deserializer per result shape (based on the
/// first row), so a direct mapping to <see cref="decimal"/> blows up with
/// <c>InvalidCastException</c> as soon as the row types disagree.
/// <para/>
/// This handler takes the conversion over explicitly, per value instead of
/// per shape: whatever SQLite hands back becomes a decimal, deterministically.
/// </summary>
public sealed class SqliteDecimalHandler : SqlMapper.TypeHandler<decimal>
{
    public override decimal Parse(object value) => value switch
    {
        decimal d => d,
        double dbl => (decimal)dbl,
        float f => (decimal)f,
        long l => l,
        int i => i,
        string s => decimal.Parse(s, CultureInfo.InvariantCulture),
        _ => Convert.ToDecimal(value, CultureInfo.InvariantCulture)
    };

    public override void SetValue(IDbDataParameter parameter, decimal value)
    {
        // Same as Dapper's default for decimal: the SQLite provider binds it
        // as TEXT, which our TEXT-affinity Price column stores verbatim, so
        // decimals round-trip exactly (no REAL/double in between).
        parameter.DbType = DbType.Decimal;
        parameter.Value = value;
    }

    internal static void Register() =>
        SqlMapper.AddTypeHandler(typeof(decimal), new SqliteDecimalHandler());
}
