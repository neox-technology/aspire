# EF Core migration worker package

| Field | Value |
|-------|-------|
| Slug | `efcore-migration-worker` |
| Status | implemented |
| Last code review | 2026-07-28 |

## Summary

Ship a reusable **non-hosting** NuGet library (`Neox.Aspire.EntityFrameworkCore.MigrationWorker`) that registers a one-shot `BackgroundService` to apply EF Core migrations for a registered `DbContext`, then stops the host. Consumers call `IServiceCollection.AddEfCoreMigrationService<TDbContext>()`.

Integration tests under `tests/efcore-migration-worker/{sqlserver|postgresql|mysql|oracle}/` follow Neox Aspire naming (kebab folders, `ServiceNames`, AppHost split) and use **xUnit** + `Aspire.Hosting.Testing` with **one AppHost per database type**.

## User scenarios

- A contributor builds this repo and obtains a Shipping nupkg for `Neox.Aspire.EntityFrameworkCore.MigrationWorker`.
- A consumer worker app references the package, registers a `DbContext`, and calls `services.AddEfCoreMigrationService<TDbContext>()` so migrations run on startup then the process exits.
- A contributor runs xUnit integration tests via `Build.cmd -configuration Release -test` (Docker required) that start an Aspire AppHost per provider and assert migrations were applied for one and multiple databases of that type.
- Consumers obtain the package from GitHub Packages (see [`nuget-github-packages`](nuget-github-packages.md)).

## Routes (if UI)

_N/A — DI / worker library._

## Dependencies

- Arcade pack/publish ([`nuget-github-packages`](nuget-github-packages.md))
- Terminology ([`domain-glossary`](domain-glossary.md))
- `Microsoft.EntityFrameworkCore.Relational` (`MigrateAsync`)
- `Microsoft.NET.Sdk.Worker` (library `OutputType`)
- Aspire **13.4.x** hosting/testing + EF client integrations (integration tests)
- Docker (integration tests / CI)

## Out of scope

- Aspire AppHost extension API (`Add*` / `With*` on `IDistributedApplicationBuilder`) in the Shipping package
- Registering or configuring the `DbContext` / database provider (consumer responsibility)
- Product-specific `DbContext` types from consuming apps
- MSTest (xUnit only for this feature’s tests)
- Azure SQL / Azure Database for PostgreSQL harnesses (same EF providers as SqlServer / PostgreSql)
- SQLite, Cosmos DB, MariaDB (no first-class Aspire hosting + relational `MigrateAsync` pair in this matrix)

## Acceptance criteria

- [x] Package id is `Neox.Aspire.EntityFrameworkCore.MigrationWorker` at `src/Neox.Aspire.EntityFrameworkCore.MigrationWorker/` (not under `src/hosting/`).
- [x] `AddEfCoreMigrationService<TDbContext>()` registers a hosted service that creates a DI scope, resolves `TDbContext`, calls `Database.MigrateAsync`, then `IHostApplicationLifetime.StopApplication()`.
- [x] Arcade pack produces a Shipping nupkg for the project.
- [x] Integration harness lives under `tests/efcore-migration-worker/{sqlserver|postgresql|mysql|oracle}/` with Neox folders `apphost/`, `migration-service/`, `service-defaults/`, `data/`, `tests/`.
- [x] One AppHost per database type; resource names via `ServiceNames` (kebab + short prefix).
- [x] xUnit waits for migrators in `KnownResourceStates.Finished`, then asserts `GetAppliedMigrationsAsync` non-empty and `GetPendingMigrationsAsync` empty.
- [x] Tests cover single-database and multiple-databases (same provider) scenarios.
- [x] CI runs Arcade `-test` for SqlServer, PostgreSql, MySQL, and Oracle (8 Facts); Docker is documented as a prerequisite.
- [x] MySQL harness uses `MySql.EntityFrameworkCore` (Pomelo has no EF Core 10 release yet); Oracle uses `Aspire.Hosting.Oracle` + `Aspire.Oracle.EntityFrameworkCore` with `FREEPDB1` (Oracle Free does not create named PDBs from `AddDatabase`).
- [x] Oracle multi-database scenario uses two Oracle Free containers (each exposing `FREEPDB1`), because a single Free instance only provides one PDB by default.

## Terminology

See [`domain-glossary`](domain-glossary.md) (`migration worker`, `Shipping`, `Neox Aspire packages`).

## Implementation notes

| Item | Path |
|------|------|
| Project | `src/Neox.Aspire.EntityFrameworkCore.MigrationWorker/` |
| Package id | `Neox.Aspire.EntityFrameworkCore.MigrationWorker` |
| Namespace | `Neox.Aspire.EntityFrameworkCore` |
| Extension | `AddEfCoreMigrationService<TDbContext>(this IServiceCollection)` |
| Worker | `EfCoreMigrationWorker<TDbContext> : BackgroundService` |
| SQL Server tests | `tests/efcore-migration-worker/sqlserver/` |
| PostgreSQL tests | `tests/efcore-migration-worker/postgresql/` |
| MySQL tests | `tests/efcore-migration-worker/mysql/` |
| Oracle tests | `tests/efcore-migration-worker/oracle/` |

### Test project naming (Neox Aspire pattern)

| Folder | Example project (SQL Server) |
|--------|------------------------------|
| `apphost/` | `….Tests.SqlServer.AppHost` |
| `migration-service/` | `….Tests.SqlServer.MigrationService` |
| `service-defaults/` | `….Tests.SqlServer.ServiceDefaults` (`ServiceNames`) |
| `data/` | `….Tests.SqlServer.Data` |
| `tests/` | `….Tests.SqlServer` (xUnit) |
