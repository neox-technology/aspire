# GitFlow branching and release Actions

| Field | Value |
|-------|-------|
| Slug | `gitflow-ci` |
| Status | implemented |
| Last code review | 2026-10-15 |

## Summary

GitFlow **branch convention** for this repo: `feature/*` from `develop`; `release/*` / `hotfix/*` to `main`; `main` holds shipped releases. Manual GitHub Actions cut a release from `develop`. Preview (`release/x.x.x-preview.x`) publishes a **daily** feed to GitHub Packages, squash-merges into `develop` only, tags a prerelease on `develop`, and deletes the release branch. Final (`release/x.x.x`) publishes to nuget.org, squash-merges into `main` and `develop`, and tags on `main`. NuGet identity lives in Arcade [`eng/Versions.props`](../../eng/Versions.props). Pack/publish details: [`nuget-org`](nuget-org.md). No Aspire deploy workflow.

## User scenarios

1. **Contributor cuts a feature** — branches `feature/*` from `develop` and opens a PR to `develop` (squash when ready).
2. **Operator prepares a release** — runs **start-release** on `develop`, selects `major` / `minor` / `patch` / `none` and optional **preview**, which computes the next version from the latest `v*` tag, updates `eng/Versions.props`, and pushes `release/<version>` (no PR).
3. **Operator publishes a preview** — runs **publish-preview-version** on `release/x.x.x-preview.x` (`environment: staging`): Arcade build/test/pack with the `daily` label → GitHub Packages, then squash-merge into `develop` only, prerelease tag and GitHub Release on `develop`, and delete the release branch. Does not update `main` or nuget.org.
4. **Operator publishes a final** — runs **publish-final-version** on `release/x.x.x` (`environment: production`): Arcade build/test/pack from `Versions.props` → nuget.org (Trusted Publishing), then squash-merge into `main` and `develop`, and tag plus GitHub Release on `main`. The release branch stays.

## Business rules

1. **Branch model** — `feature/*` → `develop`; `release/*` / `hotfix/*` → `main`; `main` is stable releases only.
2. **Manual Actions only** — release workflows use `workflow_dispatch`. They must be run from the correct ref (`develop` for **start-release**; `release/x.x.x-preview.x` for **publish-preview-version**; `release/x.x.x` for **publish-final-version**).
3. **Versions.props** — start-release commits `VersionPrefix` and `StabilizePackageVersion` on the release branch so shipping identity matches the branch/tag core. Preview sets `StabilizePackageVersion=false`; final sets `true`.
4. **Linear history** — preview squash-merges the release branch into `develop` only. Final squash-merges it into `main` and `develop` (no release PRs).
5. **Preview closes the branch** — **publish-preview-version** deletes the release branch after the squash. Final does not.
6. **Git automation identity** — branch/tag/push steps use org App **`neox-gitflow`** (`NEOX_GITFLOW_APP_ID` / `NEOX_GITFLOW_APP_PRIVATE_KEY`), not a long-lived PAT. Git finalize jobs have no GitHub environment.

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
- Exact NuGet equality for preview tags (`1.2.3-preview.N` vs Arcade `1.2.3-daily.{OfficialBuildId}`)

## Acceptance criteria

- [x] README and this spec describe GitFlow branches plus the three manual release workflows
- [x] `.github/workflows/start-release.yml` cuts `release/*` from `develop` (no PR) and commits `eng/Versions.props`
- [x] `.github/workflows/publish-preview-version.yml` builds/tests/packs and pushes daily packages to GitHub Packages from `release/x.x.x-preview.x`, squash-merges into `develop`, tags a prerelease on `develop`, and deletes the branch
- [x] `.github/workflows/publish-final-version.yml` builds/tests/packs, publishes to nuget.org, squash-merges into `main` and `develop`, and tags `v*` on `main` from `release/x.x.x`
- [x] `eng/Versions.props` remains the Arcade version source of truth (updated by start-release)

## Terminology

See [`domain-glossary`](domain-glossary.md).

## Implementation notes

| Item | Path / note |
|------|-------------|
| Start release | `.github/workflows/start-release.yml` |
| Preview / GitHub Packages | `.github/workflows/publish-preview-version.yml` (`environment: staging`) |
| Final / nuget.org | `.github/workflows/publish-final-version.yml` (`environment: production`) |
| Arcade version SoT | `eng/Versions.props` |
| Related packaging spec | [`nuget-org`](nuget-org.md) |
| Secrets | `NEOX_GITFLOW_APP_ID`, `NEOX_GITFLOW_APP_PRIVATE_KEY` (organization); `NUGET_USER` (projet, nuget.org profile name) |
| Preview push | Job `GITHUB_TOKEN` with `packages: write` |

Intended Versions.props mapping when start-release runs (latest `v*` tag = base):

| bump | preview | Branch / tag | `Versions.props` |
|------|---------|--------------|------------------|
| patch / minor / major | false | stable `X.Y.Z` | `VersionPrefix=X.Y.Z`, `StabilizePackageVersion=true` |
| patch / minor / major | true | core + `-preview.1` | `VersionPrefix`=core, `StabilizePackageVersion=false` |
| none | true | same core; `-preview.N+1` or start `-preview.1` | stabilize false |
| none | false | promote: strip `-preview.N`, keep `X.Y.Z` | stabilize true |
