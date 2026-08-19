# Keycloak realm provisioning CDK

| Field | Value |
|-------|-------|
| Slug | `keycloak-provisioning-realm` |
| Status | implemented |
| Last code review | 2026-08-19 |

## Summary

Shipping CDK package **`Neox.Keycloak.Provisioning.Realm`** under `src/provisioning/Neox.Keycloak.Provisioning.Realm/` exposes Keycloak Admin REST realm representation types as **source-generated** JSON POCOs (`RealmRepresentation`, `ClientRepresentation`, …). Types are generated at build time from a checked-in snapshot of the official Keycloak Admin REST OpenAPI (`openapi.json`, pin **26.2.5** from [Keycloak docs-api](https://www.keycloak.org/docs-api/26.2.5/rest-api/openapi.json)). The generator project is not packable. Builds do not fetch the network; refreshing the snapshot is a manual tool run.

This package is not an Aspire hosting library. [`keycloak-hosting`](keycloak-hosting.md) consumes it via `AddRealm` for realm JSON generation and upstream `WithRealmImport`.

## User scenarios

1. **Consumer packs locally** — Arcade pack emits `Neox.Keycloak.Provisioning.Realm` under Shipping; the generator project is not packed.
2. **AppHost / CDK author builds realm JSON** — `new RealmRepresentation { Realm = "neox", Clients = … }` serializes to `{realm}-realm.json` for Keycloak import.
3. **Maintainer refreshes OpenAPI snapshot** — `tools/keycloak-realm-openapi-catalog` rewrites `generated/openapi.json` / `index.json`; CI stays offline-deterministic.
4. **Contributor runs unit tests** — codegen and round-trip serialization tests; no live Keycloak.

## Business rules

1. **One Shipping realm CDK package** — packable id `Neox.Keycloak.Provisioning.Realm` (`net10.0`, `RootNamespace=Neox.Keycloak.Provisioning.Realm`). Generator `Neox.Keycloak.Provisioning.Realm.Generators.Internal` is `IsPackable=false`, `netstandard2.0`, referenced as an analyzer.
2. **Official OpenAPI is the source of truth** — snapshot `generated/openapi.json` (+ `index.json` for the pin) from Keycloak docs-api Admin REST OpenAPI for version **26.2.5**. Generated types cover the **transitive closure** of `RealmRepresentation` in `components.schemas` (full realm schema, not the entire Admin API surface).
3. **C# naming** — preserve OpenAPI schema names (`RealmRepresentation`, `ClientRepresentation`) for JSON 1:1 compatibility with Keycloak import/export.
4. **Thread-safe collections** — OpenAPI `array` → `ConcurrentBag<T>?`; maps with `additionalProperties` → `ConcurrentDictionary<string, T>?`. AppHost builders use `ConcurrentBagExtensions.GetOrAdd(predicate, value)` on bags and `ConcurrentDictionary.GetOrAdd` on maps.
5. **Polymorphism** — OpenAPI `oneOf` / `anyOf` → interface `I{Name}` + concrete implementations per `$ref` branch + generated `JsonConverter<I{Name}>` registered in `KeycloakRealmJsonOptions`.
6. **Offline builds** — AdditionalFiles catalogue; no network during `dotnet build`. Refresh tool may download from keycloak.org when a maintainer runs it.
7. **Tests** — xUnit under `tests/keycloak-provisioning/`; no live Keycloak.

## Dependencies

- Arcade pack ([`nuget-org`](nuget-org.md)), terminology ([`domain-glossary`](domain-glossary.md)), repo identity ([`aspire-bootstrap`](aspire-bootstrap.md))
- [`keycloak-hosting`](keycloak-hosting.md) — consumer `AddRealm` hosting API
- Official OpenAPI: [Keycloak Admin REST API](https://www.keycloak.org/docs-api/26.2.5/rest-api/) (generated from [keycloak/keycloak](https://github.com/keycloak/keycloak))

## Out of scope

- Aspire hosting env projection (`WithKeycloakJwtBearer` / `WithKeycloakSpa` — owned by [`keycloak-hosting`](keycloak-hosting.md))
- Full Admin REST client / HTTP calls
- Keycloak **beta** or undocumented endpoints
- Automatic catalogue refresh in CI
- Git submodule of keycloak/keycloak (snapshot + refresh tool only)

## Acceptance criteria

- [x] Package `Neox.Keycloak.Provisioning.Realm` under `src/provisioning/Neox.Keycloak.Provisioning.Realm/` (`IsPackable=true`, `net10.0`).
- [x] Generator `Neox.Keycloak.Provisioning.Realm.Generators.Internal` (`IsPackable=false`) emits POCOs from AdditionalFiles `openapi.json`.
- [x] Projects listed in `Neox.Aspire.slnx` only.
- [x] `{paths.keycloakProvisioningRealm}` / `{aspire.packages.keycloakProvisioningRealm}` in `.cursor/rules/neox-rules.json`.
- [x] Checked-in `openapi.json` / `index.json` pin Keycloak **26.2.5**.
- [x] Public types include `RealmRepresentation`, `ClientRepresentation` with `ConcurrentBag` / `ConcurrentDictionary` collections.
- [x] `ConcurrentBagExtensions.GetOrAdd` provides idempotent list mutation for AppHost builders.
- [x] `oneOf` unions emit interface + `JsonConverter` with round-trip tests.
- [x] Refresh tool `tools/keycloak-realm-openapi-catalog` can rewrite the snapshot.
- [x] xUnit tests under `tests/keycloak-provisioning/` (no live Keycloak).

## Terminology

See [`domain-glossary.md`](domain-glossary.md).

## Implementation notes

| Item | Path / note |
|------|-------------|
| Shipping package | `src/provisioning/Neox.Keycloak.Provisioning.Realm/` |
| Package id | `Neox.Keycloak.Provisioning.Realm` |
| Namespace | `Neox.Keycloak.Provisioning.Realm` |
| Generator | `src/provisioning/Neox.Keycloak.Provisioning.Realm.Generators.Internal/` |
| SG class | `KeycloakRealmProvisioningGenerator` |
| Catalogue | `src/provisioning/Neox.Keycloak.Provisioning.Realm/generated/openapi.json` |
| Catalogue refresh | `tools/keycloak-realm-openapi-catalog/` |
| OpenAPI pin | Keycloak docs-api **26.2.5** |
| Unit tests | `tests/keycloak-provisioning/` |
