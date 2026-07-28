# EF Core migration worker package

| Field | Value |
|-------|-------|
| Slug | `efcore-migration-worker` |
| Status | defined |
| Last code review | 2026-07-28 |

## Summary

Ship a reusable **non-hosting** NuGet library (`Neox.Aspire.EntityFrameworkCore.MigrationWorker`) that registers a one-shot `BackgroundService` to apply EF Core migrations for a registered `DbContext`, then stops the host. Consumers call `IServiceCollection.AddEfCoreMigrationService<TDbContext>()`. This is not an Aspire AppHost / `Neox.Aspire.Hosting.*` package.

## User scenarios

- A contributor builds this repo and obtains a Shipping nupkg for `Neox.Aspire.EntityFrameworkCore.MigrationWorker`.
- A consumer worker app references the package, registers a `DbContext`, and calls `services.AddEfCoreMigrationService<TDbContext>()` so migrations run on startup then the process exits.
- Consumers obtain the package from GitHub Packages (see [`nuget-github-packages`](nuget-github-packages.md)).

## Routes (if UI)

_N/A — DI / worker library._

## Dependencies

- Arcade pack/publish ([`nuget-github-packages`](nuget-github-packages.md))
- Terminology ([`domain-glossary`](domain-glossary.md))
- `Microsoft.EntityFrameworkCore.Relational` (`MigrateAsync`)
- `Microsoft.NET.Sdk.Worker` (library `OutputType`)

## Out of scope

- Aspire AppHost extension API (`Add*` / `With*` on `IDistributedApplicationBuilder`)
- Registering or configuring the `DbContext` / database provider (consumer responsibility)
- ServiceDefaults, health checks, or deployment wiring
- Product-specific `DbContext` types from consuming apps

## Acceptance criteria

- [ ] Package id is `Neox.Aspire.EntityFrameworkCore.MigrationWorker` at `src/Neox.Aspire.EntityFrameworkCore.MigrationWorker/` (not under `src/hosting/`).
- [ ] `AddEfCoreMigrationService<TDbContext>()` registers a hosted service that creates a DI scope, resolves `TDbContext`, calls `Database.MigrateAsync`, then `IHostApplicationLifetime.StopApplication()`.
- [ ] Arcade pack produces a Shipping nupkg for the project.

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
