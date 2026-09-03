# GitFlow branching and release Actions

| Field | Value |
|-------|-------|
| Slug | `gitflow-ci` |
| Status | implemented |
| Last code review | 2026-09-03 |

## Summary

GitFlow **branch convention** for this repo: `feature/*` from `develop`; `release/*` / `hotfix/*` to `main`; `main` holds shipped releases. Manual GitHub Actions cut a release from `develop`, publish a private daily feed from `release/*`, and finalize (squash-merge PR, tag `v*`, publish nuget.org). NuGet identity lives in Arcade [`eng/Versions.props`](../../eng/Versions.props). Pack/publish details: [`nuget-org`](nuget-org.md). No Aspire deploy workflow.

## User scenarios

1. **Contributor cuts a feature** — branches `feature/*` from `develop` and opens a PR to `develop` (squash when ready).
2. **Operator prepares a release** — runs **release-start** on `develop`, selects `major` / `minor` / `patch` / `preview`, which computes the next version from the latest `v*` tag, updates `eng/Versions.props`, pushes `release/<version>`, and opens a PR to `main`.
3. **Operator validates on the private feed** — runs **release-private-publish** on `release/*` (Arcade build/test/pack with `daily` label → GitHub Packages).
4. **Operator finishes a release** — runs **release-finalize** on `release/*`: squash-merges the open PR to `main`, tags `v<version>`, creates a GitHub Release, Arcade build/test/pack, Trusted Publishing to nuget.org, optional sync PR `main` → `develop`.

## Business rules

1. **Branch model** — `feature/*` → `develop`; `release/*` / `hotfix/*` → `main`; `main` is stable releases only.
2. **Manual Actions only** — release workflows use `workflow_dispatch`. They must be run from the correct ref (`develop` for start; `release/*` for private publish and finalize).
3. **Versions.props** — start-release commits `VersionPrefix` and `StabilizePackageVersion` on the release branch so shipping identity matches the branch/tag core.
4. **Linear history** — prefer squash-merge (finalize squash-merges the release PR).
5. **Git automation identity** — branch/PR/tag steps use org App **`neox-gitflow`** (`NEOX_GITFLOW_APP_ID` / `NEOX_GITFLOW_APP_PRIVATE_KEY`), not a long-lived PAT.

## Dependencies

- [`aspire-bootstrap`](aspire-bootstrap.md) — repository identity
- [`nuget-org`](nuget-org.md) — pack and publish targets
- [`domain-glossary`](domain-glossary.md) — **Neox Aspire**, **Shipping**, **daily**

## Out of scope

- PR CI workflow on every pull request
- Auto-PR for `feature/*`
- Aspire / Azure deploy
- Publishing NuGet from `develop`
- Changing org rulesets or inventing secret values
- Exact NuGet equality for preview tags (`1.2.3-preview.N` vs Arcade `1.2.3-preview.{date}.{r}`)

## Acceptance criteria

- [x] README and this spec describe GitFlow branches plus the three manual release workflows
- [x] `.github/workflows/release-start.yml` cuts `release/*` from `develop` and opens PR → `main`
- [x] `.github/workflows/release-private-publish.yml` builds/tests/packs and pushes daily packages to GitHub Packages from `release/*`
- [x] `.github/workflows/release-finalize.yml` squash-merges the release PR, tags `v*`, and publishes to nuget.org
- [x] `eng/Versions.props` remains the Arcade version source of truth (updated by start-release)

## Terminology

See [`domain-glossary`](domain-glossary.md).

## Implementation notes

| Item | Path / note |
|------|-------------|
| Start release | `.github/workflows/release-start.yml` |
| Private feed | `.github/workflows/release-private-publish.yml` |
| Finalize / nuget.org | `.github/workflows/release-finalize.yml` |
| Arcade version SoT | `eng/Versions.props` |
| Related packaging spec | [`nuget-org`](nuget-org.md) |
| Secrets | `NEOX_GITFLOW_APP_ID`, `NEOX_GITFLOW_APP_PRIVATE_KEY`, `NUGET_USER` |

Intended Versions.props mapping when start-release runs (latest `v*` tag = base):

| bump | Branch / tag | `Versions.props` |
|------|--------------|------------------|
| patch / minor / major | stable `X.Y.Z` | `VersionPrefix=X.Y.Z`, `StabilizePackageVersion=true` |
| preview (last tag already `-preview.N`) | same core + `-preview.N+1` | `VersionPrefix`=core, `StabilizePackageVersion=false` |
| preview (last tag stable) | next patch core + `-preview.1` | `VersionPrefix`=core, `StabilizePackageVersion=false` |
