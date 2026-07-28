# EF Core migration worker hosting package

| Field | Value |
|-------|-------|
| Slug | `efcore-migration-worker` |
| Status | draft |
| Last code review | 2026-07-28 |

## Summary

Intent: ship a reusable Aspire hosting package (`Neox.Aspire.Hosting.EntityFrameworkCore.MigrationWorker`) that helps AppHosts run EF Core database migrations as a dedicated worker/resource. This draft covers bootstrap only — project skeleton and packability — not the product API.

## User scenarios

- A Neox contributor restores/builds this repo and obtains a Shipping nupkg for the MigrationWorker project.
- Consumers will later reference the package from GitHub Packages (see [`nuget-github-packages`](nuget-github-packages.md)); wiring details are deferred.

## Routes (if UI)

_N/A — hosting library._

## Dependencies

- Arcade pack/publish ([`nuget-github-packages`](nuget-github-packages.md))
- Terminology ([`domain-glossary`](domain-glossary.md))

## Out of scope

- Aspire AppHost extension API (`Add*` / `With*` surface)
- EF Core / Aspire package references and runtime behavior
- Worker base types, DI, health checks, or deployment wiring
- Copying product code from consuming apps (e.g. business-plan MigrationService)

## Acceptance criteria

- [ ] Project skeleton exists at `src/hosting/Neox.Aspire.Hosting.EntityFrameworkCore.MigrationWorker/` and is included in the solution so Arcade pack produces a Shipping nupkg.

## Terminology

See [`domain-glossary`](domain-glossary.md) (`migration worker`, `Shipping`, `Neox Aspire packages`).

## Implementation notes

| Item | Path |
|------|------|
| Project (planned) | `src/hosting/Neox.Aspire.Hosting.EntityFrameworkCore.MigrationWorker/` |
| Package id | `Neox.Aspire.Hosting.EntityFrameworkCore.MigrationWorker` |

Feature API and acceptance criteria beyond the skeleton will be added when this spec moves to `defined`.
