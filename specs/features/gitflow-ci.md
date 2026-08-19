# GitFlow branching

| Field | Value |
|-------|-------|
| Slug | `gitflow-ci` |
| Status | defined |
| Last code review | 2026-08-16 |

## Summary

GitFlow **branch convention** for this repo: `feature/*` from `develop`; `release/*` / `hotfix/*` to `main`; `main` holds shipped releases. **GitHub Actions are not in this tree** (no auto-PR, finish, start-release, CI, or publish workflows). NuGet identity still lives in Arcade [`eng/Versions.props`](../../eng/Versions.props). Local pack is [`nuget-org`](nuget-org.md). No Aspire deploy workflow.

## User scenarios

1. **Contributor cuts a feature** — branches `feature/*` from `develop` and opens a PR to `develop` (squash when ready). No in-repo workflow opens that PR.
2. **Operator prepares a release** — cuts `release/x.y.z` (or preview) from `develop`, aligns `eng/Versions.props` by hand when needed, and opens a PR to `main`. No start-release Action.
3. **Operator finishes a release** — squash-merges to `main` and tags `vX.Y.Z` (or preview) by hand. No finish Action, no automated GitHub Release, no sync PR.
4. **Contributor packs locally** — Arcade pack on the current branch; no CI workflow on PRs.

## Business rules

1. **Branch model** — `feature/*` → `develop`; `release/*` / `hotfix/*` → `main`; `main` is stable releases only.
2. **No Actions** — `.github/workflows/` is absent. Do not document missing workflow files as present.
3. **Versions.props** — release identity (`VersionPrefix`, `StabilizePackageVersion`) is committed in `eng/Versions.props` when shipping; there is no start-release job to write it.
4. **Linear history** — prefer squash-merge when GitHub is used.

## Dependencies

- [`aspire-bootstrap`](aspire-bootstrap.md) — repository identity and no-GHA rule
- [`nuget-org`](nuget-org.md) — local pack; publish not wired
- [`domain-glossary`](domain-glossary.md) — **Neox Aspire**

## Out of scope

- Restoring `.github/workflows/*` in this change
- Aspire / Azure deploy
- Publishing NuGet from `develop`
- Changing org rulesets or inventing secret values

## Acceptance criteria

- [x] README and this spec describe GitFlow as a **branch convention**, not as in-repo Actions
- [x] No `.github/workflows/` in this tree
- [x] `eng/Versions.props` remains the Arcade version source of truth
- [ ] GitHub Actions for auto-PR, finish, cleanup, start-release, CI, and publish are restored (out of this bootstrap)

## Terminology

See [`domain-glossary`](domain-glossary.md).

## Implementation notes

| Item | Path / note |
|------|-------------|
| Workflows | **Absent** — do not add without a dedicated change |
| Arcade version SoT | `eng/Versions.props` |
| Related packaging spec | [`nuget-org`](nuget-org.md) |

Intended Versions.props mapping when a human cuts a release (latest `v*` tag = base):

| bump | preview | Branch / tag | `Versions.props` |
|------|---------|--------------|------------------|
| patch / minor / major | false | stable `X.Y.Z` | `VersionPrefix=X.Y.Z`, `StabilizePackageVersion=true` |
| patch / minor / major | true | core + `-preview.1` | `VersionPrefix`=core, `StabilizePackageVersion=false` |
| no-op | true | same core; `-preview.N+1` or start `-preview.1` | stabilize false |
| no-op | false | promote: strip `-preview.N`, keep `X.Y.Z` | stabilize true |
