# Arcade bootstrap — GitHub Actions templates

Derived from Neox `aspire-private` (`.github/workflows/ci.yml` + `publish-nuget.yml`).

Replace `<GITHUB_OWNER>` with the GitHub org/user from `git remote get-url origin` (e.g. `neox-technology`).

Triggers are **`main` only** (not `develop`). Use `/p:Sign=false` — MicroBuild signing is out of scope.

## `.github/workflows/ci.yml`

Pack on PRs to `main`. Upload Shipping artifacts. **Do not push** NuGet.

```yaml
name: CI

on:
  pull_request:
    branches: [main]

permissions:
  contents: read

jobs:
  build:
    name: Build and pack
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Build and pack
        run: ./eng/common/build.sh --ci --restore --build --pack --configuration Release /p:Sign=false

      - name: Upload Shipping packages
        uses: actions/upload-artifact@v4
        with:
          name: Shipping
          path: artifacts/packages/Release/Shipping/
          if-no-files-found: error
```

## `.github/workflows/publish-nuget.yml`

Pack on merge to `main` (and optional `workflow_dispatch`) with Arcade `OfficialBuildId`, then push Shipping `*.nupkg` to GitHub Packages for this owner.

```yaml
name: Publish NuGet

on:
  push:
    branches: [main]
  workflow_dispatch:

permissions:
  contents: read
  packages: write

jobs:
  publish:
    name: Pack and push
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Compute OfficialBuildId
        id: version
        run: echo "official_build_id=$(date -u +%Y%m%d).${{ github.run_number }}" >> "$GITHUB_OUTPUT"

      - name: Build and pack
        run: >
          ./eng/common/build.sh
          --ci --restore --build --pack
          --configuration Release
          /p:Sign=false
          /p:OfficialBuildId=${{ steps.version.outputs.official_build_id }}

      - name: Push packages to GitHub Packages
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
        run: |
          set -euo pipefail
          shopt -s nullglob
          packages=(artifacts/packages/Release/Shipping/*.nupkg)
          if [ ${#packages[@]} -eq 0 ]; then
            echo "No Shipping packages found under artifacts/packages/Release/Shipping/"
            exit 1
          fi
          dotnet nuget push "${packages[@]}" \
            --source "https://nuget.pkg.github.com/<GITHUB_OWNER>/index.json" \
            --api-key "${GITHUB_TOKEN}" \
            --skip-duplicate
```

## Notes

- **Versions**: PRs get `*-ci` versions (no `OfficialBuildId`). Official builds use `PreReleaseVersionLabel` (`preview`) until `StabilizePackageVersion=true`.
- **Feed**: owner-namespaced (`nuget.pkg.github.com/<GITHUB_OWNER>`); packages associate with the repo via Actions publish + `RepositoryUrl` in `Directory.Build.props`.
- **Symbols**: this push selects `*.nupkg` only. If `.snupkg` files must land on the feed, push them in a separate step (or broaden the glob carefully — do not push `.symbols.nupkg` duplicates unintentionally).
- **Permissions**: publish needs `packages: write`; `GITHUB_TOKEN` is enough for repo-linked packages.
- **Artifact path**: Shipping packages live under `artifacts/packages/Release/Shipping/` after Arcade pack.
