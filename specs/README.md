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
| [`nuget-github-packages`](features/nuget-github-packages.md) | defined | Arcade pack/publish to repo GitHub Packages |
| [`efcore-migration-worker`](features/efcore-migration-worker.md) | defined | EF Core migration worker + Aspire MSTest harnesses (SqlServer/Postgres/MySql) |
