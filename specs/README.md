# Specifications — Neox Aspire

Feature index for this public repository of shared `Neox.Aspire.*` NuGet packages (hosting and other libraries). Each spec lives in `specs/features/<slug>.md`.

This repository is not the Events product, **Site overlay**, **Planner**, Events AppHost, or **Neox SaaS**. Sample/test AppHosts live under `tests/`. Terminology: [`features/domain-glossary.md`](features/domain-glossary.md) (`glossaryOwner` in `neox-rules.json`).

## Conventions

- **Slug**: kebab-case. Filename: `specs/features/<slug>.md`.
- **Language**: English.
- **Status values**: `draft` → `defined` → `implemented`.
- **Terminology**: [`features/domain-glossary.md`](features/domain-glossary.md).

## Mandatory feature template

Every **new** file in `specs/features/` must follow this structure:

```markdown
# Feature name

| Field | Value |
|-------|-------|
| Slug | `kebab-case-slug` |
| Status | draft \| defined \| implemented |
| Last code review | YYYY-MM-DD |

## Summary

One-paragraph description.

## User scenarios

1. **Actor does X** — outcome.

## Business rules

1. **Rule name** — invariant (omit this section for pure platform/vision specs if unused).

## Dependencies

- Links to other feature slugs (bidirectional where applicable)

## Out of scope

What this feature explicitly does NOT cover.

## Acceptance criteria

- [ ] Testable checklist item

## Terminology

See [`domain-glossary.md`](domain-glossary.md).

## Implementation notes

| Item | Path / note |
|------|-------------|
| … | … |
```

After creating or updating a feature, refresh the index below **and** the status table in the root [README.md](../README.md).

- **Acceptance criteria**: expected behavior in the present tense; `[x]` if already in code, `[ ]` for known gaps. The glossary (`domain-glossary`) has no acceptance-criteria section by design.
- **Terminology**: point to `domain-glossary`; do not redefine shared terms.

## Index

| Slug | Status | Description |
|------|--------|-------------|
| [`domain-glossary`](features/domain-glossary.md) | defined | Neox Aspire terms (repo, development host, sample AppHost, package vocabulary) |
| [`aspire-bootstrap`](features/aspire-bootstrap.md) | defined | Public repo identity, local specs, sample AppHosts, no GitHub Actions |
| [`arcade-bootstrap`](features/arcade-bootstrap.md) | implemented | Arcade clone-and-build on .NET 10; `Neox.Aspire.slnx`; MIT; no GHA |
| [`gitflow-ci`](features/gitflow-ci.md) | defined | GitFlow **branch convention**; Actions not in this tree |
| [`nuget-org`](features/nuget-org.md) | defined | Arcade local pack; nuget.org identity; publish workflows absent |
| [`efcore-migration-worker`](features/efcore-migration-worker.md) | implemented | EF Core migration worker + Aspire xUnit harnesses (SqlServer/Postgres/MySQL/Oracle) |
| [`azure-custom-domains`](features/azure-custom-domains.md) | implemented | ACA custom domain ops (OctoDNS-in-Docker, managed certs, `aspire do`) |
| [`azure-entra-id`](features/azure-entra-id.md) | implemented | Shipping `AddAzureAppRegistration` + `WithSupportedAccountType` + `AddScope` + `AddAppRole` + `AddWebApplication` + `AddSpaApplication` + `WithPermission` + `AddCertificate` / `WithKeyCredential` + `WithSecret` (`EntraIdPasswordCredentialResource`) + `EntraIdInstance` + `WithMicrosoftIdentityWebApplication` + `WithEntraIdSpaApplication` |
| [`keycloak-hosting`](features/keycloak-hosting.md) | implemented | Shipping `AddRealm` + `AddJwtClient` / `AddOidcClient` + `AddIdentityProvider` + `WithLocalRedirectUri` / `WithRedirectUrl` + `WithKeycloakJwtBearer` / `WithKeycloakSpa` on upstream `AddKeycloak`; harness under `tests/keycloak/` |
| [`keycloak-entraid`](features/keycloak-entraid.md) | implemented | Shipping bridge `AddEntraIdIdentityProvider` — Entra app registration as Keycloak OIDC identity provider (email claim mappers) |
| [`azure-provisioning-graph`](features/azure-provisioning-graph.md) | implemented | Shipping `Neox.Azure.Provisioning.Graph` — source-generated Graph Bicep constructs from msgraph-bicep-types v1.0/1.0.0 |
| [`keycloak-provisioning-realm`](features/keycloak-provisioning-realm.md) | implemented | Shipping `Neox.Keycloak.Provisioning.Realm` — source-generated realm POCOs from Keycloak Admin REST OpenAPI 26.2.5 |
