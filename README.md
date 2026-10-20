# Dapper vs EF Core — a data-access shootout with real numbers

[![Build](https://github.com/varunkonnojutech-lab/dapper-vs-efcore/actions/workflows/ci.yml/badge.svg)](https://github.com/varunkonnojutech-lab/dapper-vs-efcore/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/varunkonnojutech-lab/dapper-vs-efcore)

Every .NET team eventually argues about this: **Entity Framework Core** (full ORM, LINQ, change tracking) vs **Dapper** (micro-ORM, hand-written SQL, near-zero overhead). Opinions are cheap — this repo settles it with the same workload run against both, measured by BenchmarkDotNet.

## Who is this for

Developers who use EF Core daily but have never measured what it costs — and anyone choosing a data-access strategy for a new service who wants evidence instead of folklore.

## What you'll learn

- How the two tools actually differ: LINQ-to-SQL translation + change tracker vs raw parameterized SQL + direct materialization
- Writing a fair benchmark: identical schema, identical seed data, identical queries — the only variable is the ORM
- Reading BenchmarkDotNet output: mean latency, allocation per operation, and why allocations matter more than you think
- The honest trade-off: where Dapper wins (hot read paths), where EF Core wins (productivity, migrations, complex graphs), and the hybrid approach most senior teams land on
- Provider-agnostic data access: every store runs on SQLite (zero setup) **and** SQL Server (Docker Compose included)

## Quick start

```bash
# Build + run the parity tests (both stores must return identical results)
dotnet test

# Run the API: every endpoint exists twice — /efcore/... and /dapper/...
dotnet run --project src/DataShootout.Api
curl http://localhost:5000/efcore/products/1
curl http://localhost:5000/dapper/products/1

# Run the shootout (always Release — Debug numbers are meaningless)
dotnet run -c Release --project src/DataShootout.Benchmarks
```

## What's inside

```
src/
  DataShootout.Shared/       Product model, IProductStore contract, deterministic seed data
  DataShootout.EfCore/       EF Core store: LINQ, AsNoTracking reads, ExecuteUpdate/ExecuteDelete
  DataShootout.Dapper/       Dapper store: parameterized SQL, one connection per operation
  DataShootout.Api/          Minimal API — /efcore/* and /dapper/* side by side
  DataShootout.Benchmarks/   BenchmarkDotNet: 6 identical workloads × 2 stores
tests/
  DataShootout.Tests/        xUnit parity tests — both stores must agree, or the
                             benchmark comparison is meaningless
```

The benchmark workloads:

| Workload | What it measures |
|---|---|
| `GetById` | Point lookup by primary key |
| `GetByCategory` | Filter returning ~1,000 rows (materialization cost) |
| `GetPaged` | `OFFSET/FETCH` pagination |
| `SearchByName` | `LIKE` search |
| `CategorySummaries` | `GROUP BY` with `COUNT` + `AVG` |
| `Insert` | Single-row insert returning the new id |

## Running against SQL Server

SQLite is the default so everything works with zero setup. For the real-world comparison:

```bash
docker compose up -d
USE_SQLSERVER=1 dotnet run -c Release --project src/DataShootout.Benchmarks
```

Both stores speak both dialects — the only SQL that branches per provider is the DDL, the pagination syntax, and the last-insert-id query (clearly marked in `DapperProductStore.cs`).

## Real-world gotcha: SQLite has no decimal type

This bit us during development, so it's worth knowing. SQLite is dynamically typed: the same `Price` column can hand back a `double` for `315.12` and a `long` for `420` — row by row. Dapper generates one deserializer per result shape (from the first row's types), so mapping straight to `decimal` throws `InvalidCastException` as soon as the row types disagree.

The fix in this repo (`SqliteDecimalHandler.cs`): a three-line Dapper `TypeHandler<decimal>` that converts per value instead of per shape. The DDL also declares `Price` as `TEXT` — matching EF Core's own SQLite decimal mapping — so decimals round-trip exactly instead of detouring through `REAL`. On SQL Server the handler is a harmless pass-through (`decimal` in, `decimal` out).

## Reading the results (what to expect)

Run it yourself — numbers depend on your machine — but the *shape* is consistent:

- **Dapper is faster on reads**, typically 1.5–3× on single-row lookups, because there's no query translation and no object materialization pipeline beyond mapping columns to properties.
- **The gap shrinks on big result sets**, where database I/O dominates both.
- **EF Core's `AsNoTracking` matters**: without it, change-tracker overhead widens the gap noticeably. Every read query here uses it — that's the production-correct pattern for read-only queries.
- **`ExecuteUpdate`/`ExecuteDelete` close the write gap**: EF Core 7+ can issue set-based updates without loading entities first, which is what this repo uses.

The takeaway most teams reach: **EF Core by default, Dapper for the hot paths** — usually behind the same repository interface, exactly like `IProductStore` here. That's not a compromise; it's the senior answer.

## Try it yourself

1. Remove `AsNoTracking()` from one EF Core query and re-run — watch the allocation column.
2. Add a new benchmark: a join or a multi-mapping Dapper query vs an EF Core `Include`.
3. Point both stores at SQL Server and compare — does the gap change when the database does real work?
4. Add a third contestant: raw ADO.NET. How much of Dapper's overhead is actually Dapper?

## Next steps

- [`sqlserver-efcore-pro`](https://github.com/varunkonnojutech-lab/sqlserver-efcore-pro) *(coming soon)* — migrations, stored procedures, indexing and query tuning on SQL Server
- [`redis-caching-dotnet`](https://github.com/varunkonnojutech-lab/redis-caching-dotnet) *(coming soon)* — when the database is still too slow
- [`dotnet-diagnostics`](https://github.com/varunkonnojutech-lab/dotnet-diagnostics) *(coming soon)* — profiling the numbers you just measured
