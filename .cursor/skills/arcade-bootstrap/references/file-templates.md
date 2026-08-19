# Arcade bootstrap — file templates

Use these as starting points. Replace version placeholders with values resolved at bootstrap time from [dotnet/arcade `global.json`](https://raw.githubusercontent.com/dotnet/arcade/main/global.json). Replace `<REPO_URL>` / `<GITHUB_OWNER>` from `git remote get-url origin`.

## global.json

```json
{
  "sdk": {
    "version": "<DOTNET_SDK_VERSION>",
    "rollForward": "latestFeature",
    "paths": [
      ".dotnet",
      "$host$"
    ],
    "errorMessage": "The required .NET SDK wasn't found. Please run ./eng/common/dotnet.cmd/sh to install it."
  },
  "tools": {
    "dotnet": "<DOTNET_SDK_VERSION>"
  },
  "msbuild-sdks": {
    "Microsoft.DotNet.Arcade.Sdk": "<ARCADE_SDK_VERSION>"
  }
}
```

Keep `sdk.version` and `tools.dotnet` identical.

## NuGet.config

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="dotnet-eng" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-eng/nuget/v3/index.json" />
    <add key="dotnet-tools" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-tools/nuget/v3/index.json" />
    <add key="dotnet-public" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json" />
    <!-- Replace N with the major .NET version in use (e.g. dotnet9, dotnet10) -->
    <add key="dotnetN" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnetN/nuget/v3/index.json" />
  </packageSources>
  <disabledPackageSources>
    <clear />
  </disabledPackageSources>
</configuration>
```

Add other feeds the repo already requires after these entries.

## Directory.Build.props — proprietary license (Neox default)

```xml
<Project>
  <Import Project="Sdk.props" Sdk="Microsoft.DotNet.Arcade.Sdk" />

  <PropertyGroup>
    <PackageLicenseFile>LICENSE.txt</PackageLicenseFile>
    <PackageLicenseFullPath>$(MSBuildThisFileDirectory)LICENSE.txt</PackageLicenseFullPath>
    <PackageProjectUrl><REPO_URL></PackageProjectUrl>
    <RepositoryUrl><REPO_URL></RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
    <EmbedUntrackedSources>true</EmbedUntrackedSources>
    <IncludeSymbols>true</IncludeSymbols>
    <!-- Prefer false here; set IsPackable=true only on Shipping projects -->
    <IsPackable>false</IsPackable>
  </PropertyGroup>
</Project>
```

Put customizations **after** the Arcade import. Optional: `PackageReadmeFile` when packaging a README into the nupkg.

## Directory.Build.props — OSS license

```xml
<Project>
  <Import Project="Sdk.props" Sdk="Microsoft.DotNet.Arcade.Sdk" />

  <PropertyGroup>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <!-- Or Apache-2.0 -->
    <PackageProjectUrl><REPO_URL></PackageProjectUrl>
    <RepositoryUrl><REPO_URL></RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
    <EmbedUntrackedSources>true</EmbedUntrackedSources>
    <IncludeSymbols>true</IncludeSymbols>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
</Project>
```

## Directory.Build.targets — proprietary (authors + pack LICENSE)

```xml
<Project>
  <Import Project="Sdk.targets" Sdk="Microsoft.DotNet.Arcade.Sdk" />

  <!-- Override Arcade Microsoft defaults — must be AFTER Sdk.targets -->
  <PropertyGroup>
    <Company>Neox Technology</Company>
    <Authors>Neox Technology</Authors>
    <Copyright>© Neox Technology. All rights reserved.</Copyright>
  </PropertyGroup>

  <!-- Pack proprietary LICENSE into nupkg -->
  <ItemGroup Condition="'$(IsPackable)' == 'true'">
    <None Include="$(PackageLicenseFullPath)" Pack="true" PackagePath="$(PackageLicenseFile)" Visible="false" />
  </ItemGroup>
</Project>
```

## Directory.Build.targets — OSS (authors only)

```xml
<Project>
  <Import Project="Sdk.targets" Sdk="Microsoft.DotNet.Arcade.Sdk" />

  <!-- Override Arcade Microsoft defaults — must be AFTER Sdk.targets -->
  <PropertyGroup>
    <Company>Neox Technology</Company>
    <Authors>Neox Technology</Authors>
    <Copyright>© Neox Technology. All rights reserved.</Copyright>
  </PropertyGroup>
</Project>
```

## LICENSE.txt (proprietary Neox)

```text
Copyright (c) Neox Technology. All rights reserved.

This software and associated documentation files (the "Software") are the
confidential and proprietary information of Neox Technology.

The Software is licensed for use only by authorized Neox Technology personnel
and contractors, and only in connection with Neox Technology projects, subject
to applicable agreements. You may not copy, modify, distribute, sublicense,
or create derivative works of the Software except as expressly permitted in
writing by Neox Technology.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND.
```

For OSS, use a standard MIT or Apache-2.0 `LICENSE` file and `PackageLicenseExpression` instead.

## eng/Versions.props

```xml
<Project>
  <PropertyGroup>
    <VersionPrefix>1.0.0</VersionPrefix>
    <PreReleaseVersionLabel>preview</PreReleaseVersionLabel>
    <!-- Set to true for repo-wide GA (stable versions). Leave false while preview. -->
    <!-- Packages with SuppressFinalPackageVersion stay prerelease after stabilize. -->
    <StabilizePackageVersion>false</StabilizePackageVersion>
    <!-- Opt-in/out Arcade tools as needed, e.g. <UsingToolXliff>false</UsingToolXliff> -->
  </PropertyGroup>
</Project>
```

Add package version properties only for dependencies this repo references.

Per-project opt-out (stay preview after GA):

```xml
<SuppressFinalPackageVersion>true</SuppressFinalPackageVersion>
```

## eng/Version.Details.xml

Minimal stub (expand when dependency flow is enabled later):

```xml
<?xml version="1.0" encoding="utf-8"?>
<Dependencies>
  <ProductDependencies>
  </ProductDependencies>
  <ToolsetDependencies>
  </ToolsetDependencies>
</Dependencies>
```

When consuming Arcade packages via Maestro later, ToolsetDependencies entries map to properties in `Versions.props`. That flow is out of scope for this skill.

## eng/Build.props (optional)

Only if default “build root `.sln`” is wrong:

```xml
<Project>
  <ItemGroup>
    <ProjectToBuild Include="$(RepoRoot)MySolution.sln" />
  </ItemGroup>
</Project>
```

## Build.cmd

```bat
@echo off
powershell -ExecutionPolicy ByPass -NoProfile -command "& ""%~dp0eng\common\Build.ps1"" -restore -build %*"
```

## build.sh

```bash
#!/usr/bin/env bash
set -euo pipefail
$(dirname "$0")/eng/common/build.sh --restore --build "$@"
```

Mark executable: `git add --chmod=+x build.sh`.

## Source project (Shipping / packable)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <!-- Use the TFM appropriate for the repo -->
    <IsPackable>true</IsPackable>
    <Description>Short package description.</Description>
  </PropertyGroup>
</Project>
```

Place under `src/`. Prefer `Microsoft.NET.Sdk` (not Arcade as the project SDK).

## README consume snippet (GitHub Packages)

Document when publishing:

- Feed: `https://nuget.pkg.github.com/<GITHUB_OWNER>/index.json`
- Authenticate with a PAT that has `read:packages` (and access to this private repo)
- Sample reference:

```xml
<PackageReference Include="Your.Package.Id" Version="1.0.0-preview.*" />
```

## GitHub Actions

See [github-workflows.md](github-workflows.md) for `ci.yml` and `publish-nuget.yml`.
