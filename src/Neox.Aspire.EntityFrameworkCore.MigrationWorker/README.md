# Neox.Aspire.EntityFrameworkCore.MigrationWorker

[![NuGet](https://img.shields.io/nuget/vpre/Neox.Aspire.EntityFrameworkCore.MigrationWorker.svg?label=NuGet)](https://www.nuget.org/packages/Neox.Aspire.EntityFrameworkCore.MigrationWorker)

One-shot EF Core migration helper for .NET worker apps. Registers a single `BackgroundService` that applies pending migrations for one or more registered `DbContext` types (in call order), then stops the host once.

## Install

```bash
dotnet add package Neox.Aspire.EntityFrameworkCore.MigrationWorker
```

```xml
<PackageReference Include="Neox.Aspire.EntityFrameworkCore.MigrationWorker" Version="1.0.0-preview.*" />
```

## Usage

Register your `DbContext` first (provider-specific), then call `AddEfCoreMigrationService`:

```csharp
using Neox.Aspire.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

// Register your DbContext first (provider-specific).
builder.Services.AddDbContext<ApplicationDbContext>(...);

builder.Services.AddEfCoreMigrationService<ApplicationDbContext>();

builder.Build().Run();
```

### Multiple DbContext types (same process)

Call the extension once per type. The hosted service is registered only once; migrations run sequentially, then `StopApplication` runs once:

```csharp
builder.Services.AddDbContext<ApplicationDbContext>(...);
builder.Services.AddDbContext<SecondaryDbContext>(...);

builder.Services.AddEfCoreMigrationService<ApplicationDbContext>();
builder.Services.AddEfCoreMigrationService<SecondaryDbContext>();
```

On startup the hosted service:

1. For each registered type (registration order): creates a DI scope, resolves the `DbContext`, calls `Database.MigrateAsync`
2. Stops the application host via `IHostApplicationLifetime.StopApplication()` once

## Aspire AppHost pattern

Use a dedicated worker project as a migration service and add it from the AppHost (for example with `AddProject` and a wait-for relationship on the database). This package is **not** an Aspire hosting package: it has no `Add*` / `With*` extensions on `IDistributedApplicationBuilder`.

## Notes

- The package does **not** register or configure the `DbContext` or database provider — that remains the consumer’s responsibility.
- Duplicate `AddEfCoreMigrationService<T>()` calls for the same type are ignored.
- Namespace: `Neox.Aspire.EntityFrameworkCore` (package id differs from the namespace).
- License: MIT.
