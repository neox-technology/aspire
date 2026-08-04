# GitFlow GitHub Actions

| Field | Value |
|-------|-------|
| Slug | `gitflow-ci` |
| Status | defined |
| Last code review | 2026-08-04 |

## Summary

Neox GitFlow automation on GitHub Actions for this repo: auto-PR, finish (tag + sync), feature cleanup, and start-release bump (including `no-op` and preview prereleases), plus CI on PRs to `develop`/`main`. NuGet delivery ([`nuget-org`](nuget-org.md)): private **daily** packages to GitHub Packages on push to `release/**`, then public packages to nuget.org on merge/push to `main`. Start-release updates Arcade [`eng/Versions.props`](../../eng/Versions.props) so shipping package versions align with the release identity. No Aspire deploy workflow.

## User scenarios

- A contributor pushes `feature/**`; an automated PR opens to `develop` (squash when ready).
- After a feature merges to `develop`, the feature branch is deleted.
- An operator runs **Start release** from `develop` with `patch`/`minor`/`major`/`no-op` and optional **preview**; a `release/x.y.z` or `release/x.y.z-preview.N` branch is cut, `eng/Versions.props` is updated on that branch (when present), and a PR to `main` opens.
  - Semver bump without preview → next stable `X.Y.Z`; `VersionPrefix=X.Y.Z`, `StabilizePackageVersion=true`.
  - Semver bump with preview → next core + `-preview.1`; `VersionPrefix` = core, `StabilizePackageVersion=false`.
  - `no-op` with preview → same core; increment `-preview.N` (or start at `-preview.1` if the latest tag has no preview suffix); stabilize false.
  - `no-op` without preview → **promote**: strip `-preview.N`, keep core `X.Y.Z`; stabilize true.
- Push of `release/**` or `hotfix/**` opens (or reuses) an auto-PR to `main`.
- Push to `release/**` also publishes Shipping packages as `X.Y.Z-daily.{OfficialBuildId}` to private GitHub Packages (pre-ship validation; does not publish to nuget.org).
- CI on a `release/**` or `hotfix/**` PR to `main` fails if the branch name is not semver, or if `eng/Versions.props` (`VersionPrefix` / `StabilizePackageVersion`) does not match the branch identity (guards manual hotfixes).
- Squash-merge of release/hotfix into `main` tags `vX.Y.Z` (or `vX.Y.Z-preview.N`), creates a GitHub Release, opens a sync PR `main` → `develop`, and deletes the release/hotfix branch.
- A PR targeting `develop` or `main` runs Arcade build/test/pack CI (`*-ci` versions) with no NuGet push.
- Merge (push) to `main` publishes Shipping packages to nuget.org using the committed `Versions.props` (stable packages match the tag via `DotNetFinalVersionKind=release`; preview packages share `X.Y.Z` with Arcade date suffix).
- Hotfix: create `hotfix/X.Y.Z` from `main`, update `eng/Versions.props` to match (same rules as start-release), then open/merge the PR; CI enforces the props alignment.

## Routes (if UI)

_N/A — GitHub Actions / branching._

## Dependencies

- Org GitHub App **`neox-gitflow`** and org secrets `NEOX_GITFLOW_APP_ID` / `NEOX_GITFLOW_APP_PRIVATE_KEY`
- Org setting: allow GitHub Actions to create and approve pull requests
- Org ruleset: PR + linear history (squash merges)
- Arcade CI / publish ([`nuget-org`](nuget-org.md))

## Out of scope

- Aspire / Azure deploy (`aspire-deploy.yml`)
- Publishing NuGet from `develop`
- Long-lived PAT for Git automation (App token only)
- Changing org rulesets or inventing secret values
- Exact NuGet equality for preview tags (`1.2.3-preview.N` vs Arcade `1.2.3-preview.{date}.{r}`)

## Acceptance criteria

- [x] `.github/workflows/gitflow-auto-pr.yml` opens PRs: `feature/**` → `develop`, `release/**` / `hotfix/**` → `main`.
- [x] `.github/workflows/gitflow-finish.yml` tags, releases, syncs `main` → `develop`, and deletes the release/hotfix branch after squash-merge to `main` (including `vX.Y.Z-preview.N`).
- [x] `.github/workflows/gitflow-cleanup-feature.yml` deletes `feature/**` after merge to `develop`.
- [x] `.github/workflows/gitflow-start-release.yml` runs from `develop` only and cuts `release/x.y.z` or `release/x.y.z-preview.N` from latest `v*` tag + bump (`patch`/`minor`/`major`/`no-op`) and optional preview flag.
- [x] Start-release commits `eng/Versions.props` on the release branch when present (`VersionPrefix` = core; `StabilizePackageVersion` true iff not preview).
- [x] CI on `release/**` / `hotfix/**` PRs to `main` validates branch semver and `Versions.props` against the branch name.
- [x] Git automation uses `actions/create-github-app-token` + org App secrets (not a PAT).
- [x] `.github/workflows/ci.yml` runs on `pull_request` to `develop` and `main` (build/test/pack, no push).
- [x] `.github/workflows/publish-nuget.yml` publishes daily GitHub Packages on push to `release/**`, and nuget.org on push to `main` (+ `workflow_dispatch` per branch).
- [x] README documents the GitFlow Actions trigger matrix (including no-op / preview, daily GHP, nuget.org) and Versions.props alignment.
- [x] No Aspire deploy workflow in this repo.

## Terminology

See [`domain-glossary`](domain-glossary.md).

## Implementation notes

| Item | Path |
|------|------|
| Auto-PR | `.github/workflows/gitflow-auto-pr.yml` |
| Finish release/hotfix | `.github/workflows/gitflow-finish.yml` |
| Feature cleanup | `.github/workflows/gitflow-cleanup-feature.yml` |
| Start release | `.github/workflows/gitflow-start-release.yml` |
| CI (+ version guard) | `.github/workflows/ci.yml` |
| NuGet delivery | `.github/workflows/publish-nuget.yml` |
| Arcade version SoT | `eng/Versions.props` |
| Related packaging spec | [`nuget-org`](nuget-org.md) |

Start-release version matrix (latest `v*` tag = base):

| bump | preview | Branch / tag | `Versions.props` |
|------|---------|--------------|------------------|
| patch / minor / major | false | stable `X.Y.Z` | `VersionPrefix=X.Y.Z`, `StabilizePackageVersion=true` |
| patch / minor / major | true | core + `-preview.1` | `VersionPrefix`=core, `StabilizePackageVersion=false` |
| no-op | true | same core; `-preview.N+1` or start `-preview.1` | stabilize false |
| no-op | false | promote: strip `-preview.N`, keep `X.Y.Z` | stabilize true |

Unsupported tag suffixes other than optional `-preview.N` fail the start-release job.

On `release/**`, publish packs `X.Y.Z-daily.{OfficialBuildId}` to GitHub Packages before the public ship. After merge to `main`, packages with `StabilizePackageVersion=true` pack as exact `X.Y.Z` via `DotNetFinalVersionKind=release` (matches tag). Preview releases keep Arcade’s date-based suffix ([`nuget-org`](nuget-org.md)).

Reference shape: guideline-private `gitflow-setup` templates. Org App runbook: `neox-technology/neox-github-org` → `docs/RUNBOOK.md` (`neox-gitflow`).
