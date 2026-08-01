# Agent notes — Neox Aspire

This public repo ships shared `Neox.Aspire.*` NuGet packages. Follow Neox hub workflow; do not invent a parallel process.

## Shared guidelines

- Cursor plugin **`neox-ide/neox-guidelines`** is enabled in [`.cursor/settings.json`](.cursor/settings.json) (`git-flow`, `specs-before-code`).
- Before non-trivial work, run the **`apply-neox-guidelines`** skill checklist.
- Product paths and package ids: [`.cursor/rules/neox-rules.json`](.cursor/rules/neox-rules.json).
- Local product checklist: [`.cursor/rules/neox-workflow.mdc`](.cursor/rules/neox-workflow.mdc).

## Specs before code

- Non-trivial features need a spec under [`specs/features/<slug>.md`](specs/README.md) with status at least `draft`, and **`defined` before implementation**.
- Lifecycle: `draft` → `defined` → `implemented`. Update [`specs/README.md`](specs/README.md) when adding or renaming a slug.
- Terminology authority: [`specs/features/domain-glossary.md`](specs/features/domain-glossary.md) (`glossaryOwner` in `neox-rules.json`).
- Specs are requirements for agents and contributors; published consumer docs (package READMEs, Learn) stay separate.

## Git-flow

- Feature work on `feature/*` branched from `develop`. `main` holds stable releases only.
- Prefer small, reviewable changes. Do not force-push `main`/`develop` or bypass hooks without agreement.

## Do not

- Copy shared `neox-guidelines` / `cursor-rules` `.mdc` packs into this repo.
- Add a `guidelines/` tree here (hub text lives in `guideline-private`).
- Commit secrets (`.env`, credentials).
