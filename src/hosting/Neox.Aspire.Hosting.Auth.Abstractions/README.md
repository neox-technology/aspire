# Neox.Aspire.Hosting.Auth.Abstractions

Core **AuthOps** types for Aspire hosting: `AuthOpsResourceBase` / shared `AuthOpsResource`, abstract `AuthAppRegistrationResource`, `prereq-providers-auth` and `deploy-auth` gates, and `prereq|plan|provision-{app}-auth` step name helpers.

**`WithAuth` is not in this package** — each provider package owns typed binding (`EntraAuthAppRegistrationResource`, `GoogleAuthAppRegistrationResource`).

Consumers typically reference a provider package such as [`Neox.Aspire.Hosting.Auth.EntraId`](../Neox.Aspire.Hosting.Auth.EntraId/README.md) or [`Neox.Aspire.Hosting.Auth.Google`](../Neox.Aspire.Hosting.Auth.Google/README.md), which depend on this package.

Flat redirect desired-state lives here (`WithLocalhostRedirectUri` / `WithRedirectUri` without platform buckets). `WithLocalhostRedirectUri` accepts `LocalhostRedirectScheme` (`Https` default, `Http`, or `Both`). Entra Graph Web/Spa/Native buckets use typed overloads in the EntraId package (`AuthApplicationType`).

Dashboard status helpers (`AuthDashboardStatus`, worst-wins `AuthStatusAggregator`, `AuthDashboardStatusPublisher`) live here; EntraId owns the Graph probe lifecycle and provision command.

## Spec

See [`specs/features/auth-providers.md`](../../../specs/features/auth-providers.md).
