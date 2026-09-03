# Neox.Azure.Provisioning.Graph

`Azure.Provisioning` constructs for **Microsoft Graph Bicep** resources (`Microsoft.Graph/*@v1.0`). Types are source-generated from a checked-in snapshot of [msgraph-bicep-types](https://github.com/microsoftgraph/msgraph-bicep-types) (Graph v1.0 types **1.0.0**).

This package is **Shipping**. It is not an Aspire hosting library; AppHosts typically consume it via [`Neox.Aspire.Hosting.Azure.EntraId`](../../hosting/Neox.Aspire.Hosting.Azure.EntraId/README.md) (`AddAzureAppRegistration`).

## Install

```bash
dotnet add package Neox.Azure.Provisioning.Graph
```

## Usage

```csharp
using Azure.Provisioning;
using Neox.Azure.Provisioning.Graph;

var app = new GraphApplication("app")
{
    UniqueName = "my-api",
    DisplayName = "My API",
    SignInAudience = "AzureADMyOrg",
    Web = new GraphWebApplication()
};

var existing = GraphApplication.FromExisting("app", uniqueName: "already-registered");
```

Identifier properties come from the official types (`uniqueName` on applications/groups, `appId` on service principals, `userPrincipalName` on users). Graph tenant objects do not use ARM `name` except federated identity credentials.

## Refresh types

```bash
dotnet run --project tools/msgraph-bicep-types-catalog
```

Builds stay offline from the checked-in `generated/types.json`.

## Spec

See [`specs/features/azure-provisioning-graph.md`](../../../specs/features/azure-provisioning-graph.md).
