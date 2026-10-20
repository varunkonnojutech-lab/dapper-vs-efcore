namespace DataShootout.Shared;

/// <summary>
/// Which database engine backs a store. SQLite keeps the repo zero-setup
/// (tests, API and benchmarks all run on it by default); SQL Server is the
/// production-grade option — spin it up with <c>docker compose up -d</c> and
/// set <c>USE_SQLSERVER=1</c>.
/// </summary>
public enum DbProvider
{
    Sqlite,
    SqlServer
}
