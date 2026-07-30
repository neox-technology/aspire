# Specifications — Neox Aspire

Feature index for this public repository of shared `Neox.Aspire.*` NuGet packages (hosting and other libraries). Each spec lives in `specs/features/<slug>.md`.

**Terminology authority:** [`domain-glossary.md`](features/domain-glossary.md) (`glossaryOwner` in `neox-rules.json`).

## Feature template

Each `specs/features/<slug>.md` file follows this skeleton:

```markdown
# {Title}

| Field | Value |
|-------|-------|
| Slug | `{slug}` |
| Status | draft |
| Last code review | 2026-07-28 |

## Summary
## User scenarios
## Routes (if UI)
## Dependencies
## Out of scope
## Acceptance criteria
## Terminology
## Implementation notes
```

- **Status**: `draft` → `defined` → `implemented` (Neox workflow).
- **Acceptance criteria**: expected behavior in the present tense; `[x]` if already in code, `[ ]` for known gaps.
- **Terminology**: point to `domain-glossary`; do not redefine shared terms.

## Feature index

| Slug | Status | Description |
|------|--------|-------------|
| [`domain-glossary`](features/domain-glossary.md) | draft | Neox Aspire terminology (seed) |
| [`gitflow-ci`](features/gitflow-ci.md) | defined | GitFlow Actions (auto-PR, finish, cleanup, start-release) + CI on develop/main |
| [`nuget-org`](features/nuget-org.md) | defined | Arcade pack/publish to nuget.org (Trusted Publishing) |
| [`efcore-migration-worker`](features/efcore-migration-worker.md) | implemented | EF Core migration worker + Aspire xUnit harnesses (SqlServer/Postgres/MySQL/Oracle) |
| [`azure-custom-domains`](features/azure-custom-domains.md) | implemented | ACA custom domain ops hosting package (split plan/provision DomainOps pipeline, OctoDNS-in-Docker, ARM cert inventory/create/bind) |
