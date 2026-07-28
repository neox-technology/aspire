# Glossary — Neox Aspire

| Field | Value |
|-------|-------|
| Slug | `domain-glossary` |
| Status | draft |
| Last code review | 2026-07-28 |

## Summary

Terminology authority for Neox Aspire packages in this repository (`aspire`). Seed terms only; expand as features land.

## Terminology

| Term | Definition |
|------|------------|
| **Neox Aspire packages** | Packable NuGet libraries under the `Neox.Aspire.*` root namespace, published from this repo to GitHub Packages (not NuGet.org). |
| **Shipping** | Arcade package output bucket for packages intended for consumers (`artifacts/packages/<Configuration>/Shipping/`). Distinct from non-shipping / internal artifacts. |
| **migration worker** | Reusable Aspire hosting helpers for running EF Core database migrations as a dedicated resource/process. Product API is out of scope until the related feature spec leaves draft. |

## Out of scope

Product domain glossaries (Unisson, etc.) — outside this repository. Feature-specific API names belong in their feature specs until promoted here.
