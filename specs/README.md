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
- **Acceptance criteria**: expected behavior in the present tense; `[x]` if already in code, `[ ]` for known gaps. The glossary (`domain-glossary`) has no acceptance-criteria section by design.
- **Terminology**: point to `domain-glossary`; do not redefine shared terms.

## Feature index

| Slug | Status | Description |
|------|--------|-------------|
| [`domain-glossary`](features/domain-glossary.md) | defined | Neox Aspire terminology (seed) |
| [`gitflow-ci`](features/gitflow-ci.md) | defined | GitFlow Actions (tag-bump start-release, Versions.props align, version guard CI) |
| [`nuget-org`](features/nuget-org.md) | defined | Arcade pack/publish to nuget.org (Trusted Publishing; DotNetFinalVersionKind) |
| [`efcore-migration-worker`](features/efcore-migration-worker.md) | implemented | EF Core migration worker + Aspire xUnit harnesses (SqlServer/Postgres/MySQL/Oracle) |
| [`azure-custom-domains`](features/azure-custom-domains.md) | implemented | ACA custom domain ops (OctoDNS-in-Docker, managed certs, `aspire do`) |
| [`auth-providers`](features/auth-providers.md) | implemented | AuthOps — Entra dashboard Waiting/Healthy status + provision command; `WithAuth` → `AzureAd__*`; samples WebAPI/Blazor/Ops |
| [`auth-provider-google`](features/auth-provider-google.md) | implemented | AuthOps Google — `GoogleAuthAppRegistrationResource` + provider `WithAuth` → `AUTH_GOOGLE_*` |
| [`auth-entra-graph-permissions`](features/auth-entra-graph-permissions.md) | implemented | EntraId source-generated Microsoft Graph delegated/application `WithApiPermission` binds |
