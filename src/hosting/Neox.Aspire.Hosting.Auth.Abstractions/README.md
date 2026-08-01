# Neox.Aspire.Hosting.Auth.Abstractions

Core **AuthOps** types for Aspire hosting: `AuthOpsResourceBase` / shared `AuthOpsResource`, `prereq-providers-auth` gate, plan/provision/prereq step name helpers, and generic `WithAuth` environment injection (`AUTH_*`).

Consumers typically reference [`Neox.Aspire.Hosting.Auth.EntraId`](../Neox.Aspire.Hosting.Auth.EntraId/README.md), which depends on this package.

## Spec

See [`specs/features/auth-providers.md`](../../../specs/features/auth-providers.md).
