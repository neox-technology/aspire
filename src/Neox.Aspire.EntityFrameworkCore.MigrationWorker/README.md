# Neox.Aspire.EntityFrameworkCore.MigrationWorker

One-shot EF Core migration helper for .NET worker apps. Registers a `BackgroundService` that applies pending migrations for a registered `DbContext`, then stops the host.

## Install

Package feed: [GitHub Packages](https://nuget.pkg.github.com/neox-technology/index.json) (see the [repository README](https://github.com/neox-technology/aspire) for auth).

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

On startup the hosted service:

1. Creates a DI scope and resolves `TDbContext`
2. Calls `Database.MigrateAsync`
3. Stops the application host via `IHostApplicationLifetime.StopApplication()`

## Aspire AppHost pattern

Use a dedicated worker project as a migration service and add it from the AppHost (for example with `AddProject` and a wait-for relationship on the database). This package is **not** an Aspire hosting package: it has no `Add*` / `With*` extensions on `IDistributedApplicationBuilder`.

## Notes

- The package does **not** register or configure the `DbContext` or database provider — that remains the consumer’s responsibility.
- Namespace: `Neox.Aspire.EntityFrameworkCore` (package id differs from the namespace).
- License: MIT.
