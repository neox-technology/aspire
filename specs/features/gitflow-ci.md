# GitFlow GitHub Actions

| Field | Value |
|-------|-------|
| Slug | `gitflow-ci` |
| Status | implemented |
| Last code review | 2026-08-02 |

## Summary

Neox GitFlow automation on GitHub Actions for this repo: auto-PR, finish (tag + sync), feature cleanup, and start-release from `eng/Versions.props`, plus CI on PRs to `develop`/`main`. Delivery remains NuGet publish to nuget.org from `main` only ([`nuget-org`](nuget-org.md)). No Aspire deploy workflow.

## User scenarios

- A contributor pushes `feature/**`; an automated PR opens to `develop` (squash when ready).
- After a feature merges to `develop`, the feature branch is deleted.
- An operator runs **Start release** from `develop`; version is read from `eng/Versions.props` (`VersionPrefix`, `PreReleaseVersionLabel`, `StabilizePackageVersion`). When not stabilized, a `release/x.y.z-preview.N` branch is cut (`N` from existing tags/branches); when stabilized, `release/x.y.z`. A PR to `main` opens.
- Push of `release/**` or `hotfix/**` opens (or reuses) an auto-PR to `main`.
- Squash-merge of release/hotfix into `main` tags `v` + branch version (e.g. `v1.0.0-preview.1`), creates a GitHub Release (`--prerelease` when the version has a prerelease suffix), opens a sync PR `main` → `develop`, and deletes the release/hotfix branch.
- A PR targeting `develop` or `main` runs Arcade build/test/pack CI (`*-ci` versions) with no NuGet push.
- Merge (push) to `main` publishes Shipping packages to nuget.org via Trusted Publishing.

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

## Acceptance criteria

- [x] `.github/workflows/gitflow-auto-pr.yml` opens PRs: `feature/**` → `develop`, `release/**` / `hotfix/**` → `main`.
- [x] `.github/workflows/gitflow-finish.yml` tags `v` + branch version (with `--prerelease` for preview suffixes), releases, syncs `main` → `develop`, and deletes the release/hotfix branch after squash-merge to `main`.
- [x] `.github/workflows/gitflow-cleanup-feature.yml` deletes `feature/**` after merge to `develop`.
- [x] `.github/workflows/gitflow-start-release.yml` runs from `develop` only and cuts `release/x.y.z` or `release/x.y.z-preview.N` from `eng/Versions.props`.
- [x] Git automation uses `actions/create-github-app-token` + org App secrets (not a PAT).
- [x] `.github/workflows/ci.yml` runs on `pull_request` to `develop` and `main` (build/test/pack, no push).
- [x] `.github/workflows/publish-nuget.yml` publishes only on push to `main` (+ `workflow_dispatch`).
- [x] README documents the GitFlow Actions trigger matrix.
- [x] No Aspire deploy workflow in this repo.

## Terminology

See [`domain-glossary`](domain-glossary.md).

## Implementation notes

| Item | Path |
|------|------|
| Auto-PR | `.github/workflows/gitflow-auto-pr.yml` |
| Finish release/hotfix | `.github/workflows/gitflow-finish.yml` |
| Feature cleanup | `.github/workflows/gitflow-cleanup-feature.yml` |
| Start release | `.github/workflows/gitflow-start-release.yml` (reads `eng/Versions.props`) |
| CI | `.github/workflows/ci.yml` |
| NuGet delivery | `.github/workflows/publish-nuget.yml` |
| Version source | `eng/Versions.props` (`VersionPrefix`, `PreReleaseVersionLabel`, `StabilizePackageVersion`) |
| Related packaging spec | [`nuget-org`](nuget-org.md) |

Reference shape: `neox-technology/lmh-unisson-by-lmh` (`.github/workflows/`). Org App runbook: `neox-technology/neox-github-org` → `docs/RUNBOOK.md` (`neox-gitflow`).
