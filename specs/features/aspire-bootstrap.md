# Aspire bootstrap

| Field | Value |
|-------|-------|
| Slug | `aspire-bootstrap` |
| Status | defined |
| Last code review | 2026-09-03 |

## Summary

Identifies the **Neox Aspire** repository (`neox-technology/aspire`): a public MIT tree of shared `Neox.Aspire.*` NuGet packages. Feature specs live here. Clone-and-build uses this repo’s Arcade toolset (`Neox.Aspire.slnx`). Sample/test AppHosts live under `tests/` (rule `.cursor/rules/aspire-apphost.mdc`). Manual GitHub Actions cover release cut, private daily feed, and nuget.org publish ([`gitflow-ci`](gitflow-ci.md), [`nuget-org`](nuget-org.md)). Git versioning is by tags (see `.skli/skli.json`).

## User scenarios

1. **Consumer clones this repository** — README and `specs/` describe the packages, local Arcade build/pack, and the manual release Actions.
2. **Contributor works from a development host** — edits land here via **submodule contribution** (saas mounts this repo at `aspire/`). This repo does not own the Events AppHost or the saas **test AppHost**.
3. **Agent documents a Neox Aspire feature** — reads `neox-rules.json` and every file under `specs/features/` before writing, per `.cursor/rules/specs-documentation.mdc`.

## Business rules

1. **Public identity** — remote is `neox-technology/aspire`; `{rootNamespace}` is `Neox.Aspire`; `{solutionSlug}` is `aspire`.
2. **Local specs** — feature specs live in this repository (`specs/features/`); a **development host** does not own the Neox Aspire feature catalog.
3. **Independent stack** — library projects use this repo’s Arcade SDK, `Neox.Aspire.slnx`, and TFM/defaults documented in [`arcade-bootstrap`](arcade-bootstrap.md). Do not copy Events or saas TFM, license, or hosting packages onto these libraries.
4. **Sample AppHosts only** — Aspire AppHosts under `tests/` are isolated **sample AppHosts** (harnesses). They are not a product runtime, not the Events delivery AppHost, and not the saas **test AppHost** (`saas/tests/aspire`). Layout follows `.cursor/rules/aspire-apphost.mdc`.
5. **Manual release Actions** — `.github/workflows/` holds dispatch-only release workflows; local pack still uses Arcade ([`nuget-org`](nuget-org.md), [`gitflow-ci`](gitflow-ci.md)).
6. **No secrets in tree** — no credentials or Events métier internals checked in unless they are intentionally the public package surface. CI secrets live in GitHub (App + `NUGET_USER`).
7. **Versioning** — project versioning is git tags (`versioning`: `tag` in `.skli/skli.json`).
8. **Host listing** — saas and Events must not add this repo’s `.csproj` files to `Neox.Saas.slnx` or `Neox.Events.slnx`. Build stays `Build.cmd` / `Neox.Aspire.slnx` in this repository.

## Dependencies

- [`domain-glossary`](domain-glossary.md) — **Neox Aspire**, **development host**, **submodule contribution**, **sample AppHost**
- [`arcade-bootstrap`](arcade-bootstrap.md) — clone-and-build toolset
- [`gitflow-ci`](gitflow-ci.md) — GitFlow branch convention and release Actions
- [`nuget-org`](nuget-org.md) — local pack and publish workflows
- [`.cursor/rules/neox-rules.json`](../../.cursor/rules/neox-rules.json) — `{rootNamespace}`, `{solutionSlug}`, `{specs.*}`, `{paths.*}`, `{aspire.packages}`
- [`.cursor/rules/aspire-apphost.mdc`](../../.cursor/rules/aspire-apphost.mdc) — harness AppHost layout

## Out of scope

- Recabling Arcade `eng/common`
- Events AppHost, Site overlay, Planner, or métier features
- The saas **test AppHost** under `saas/tests/aspire/`
- Listing projects in `Neox.Saas.slnx` or `Neox.Events.slnx`
- ReUI / `neox-ide/neox-guidelines` plugin

## Acceptance criteria

- [x] Repository identity (`aspire` / `neox-technology/aspire`) is documented
- [x] Local `specs/` and `neox-rules.json` exist
- [x] Independent-stack, sample-AppHost-only, no-secrets-in-tree, and manual-release-Actions rules are stated
- [x] Git tag versioning is recorded
- [x] Shipping library `{paths}` and `{aspire.packages}` are listed in `neox-rules.json`
- [ ] Every future Shipping `.csproj` is listed in `{paths}` / `{aspire.packages}` and registered in `Neox.Aspire.slnx`

## Terminology

See [`domain-glossary.md`](domain-glossary.md).

## Implementation notes

| Item | Path / note |
|------|-------------|
| Remote | `git@github.com:neox-technology/aspire.git` |
| Rules | `.cursor/rules/neox-rules.json`, `.cursor/rules/specs-documentation.mdc`, `.cursor/rules/aspire-apphost.mdc` |
| Specs index | `specs/README.md` |
| Skli | `.skli/skli.json` (`versioning`: `tag`) |
| Development host | Git submodule `aspire/` in `neox-technology/saas` |
| Solution | `Neox.Aspire.slnx` |
| License | `LICENSE` (MIT) |
