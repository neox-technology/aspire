# Glossary — Neox Aspire

| Field | Value |
|-------|-------|
| Slug | `domain-glossary` |
| Status | draft |
| Last code review | 2026-07-28 |

## Summary

Terminology authority for Neox Aspire packages in this **public** repository (`aspire`). Seed terms only; expand as features land.

## Terminology

| Term | Definition |
|------|------------|
| **Neox Aspire packages** | Packable NuGet libraries under the `Neox.Aspire.*` root namespace (hosting and non-hosting), published from this repo to GitHub Packages (not NuGet.org). MIT-licensed. |
| **hosting package** | A `Neox.Aspire.Hosting.*` library that AppHosts reference for Aspire resource/extension helpers. Lives under `src/hosting/` when present. |
| **Shipping** | Arcade package output bucket for packages intended for consumers (`artifacts/packages/<Configuration>/Shipping/`). Distinct from non-shipping / internal artifacts. |
| **migration worker** | Reusable non-hosting DI helper (`Neox.Aspire.EntityFrameworkCore.MigrationWorker`) that runs EF Core `MigrateAsync` in a one-shot `BackgroundService`, then stops the host via `AddEfCoreMigrationService<TDbContext>()`. |

## Out of scope

Product domain glossaries (Unisson, etc.) — outside this repository. Feature-specific API names belong in their feature specs until promoted here.
