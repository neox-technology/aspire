---
name: create-entra-certificate
description: >-
  Create a self-signed certificate and wire it to a Microsoft Entra ID app
  registration via AddCertificate and WithKeyCredential. Use when the user
  mentions Entra certificate, app registration certificate, keyCredentials,
  thumbprint, AddCertificate, or WithKeyCredential.
---

# Create Entra certificate

## Windows
$cert = New-SelfSignedCertificate `
  -Subject "CN=neox-entra-auth-api" `
  -CertStoreLocation "Cert:\CurrentUser\My" `
  -KeyExportPolicy Exportable `
  -KeySpec Signature `
  -KeyLength 2048 `
  -KeyAlgorithm RSA `
  -HashAlgorithm SHA256 `
  -NotAfter (Get-Date).AddYears(1)

$thumbprint = $cert.Thumbprint
Write-Host "Thumbprint: $thumbprint"

## Linux
openssl req -x509 -newkey rsa:2048 -sha256 -days 365 \
  -keyout app.key -out app.cer -nodes \
  -subj "/CN=neox-entra-auth-api"

openssl pkcs12 -export -inkey app.key -in app.cer -out app.pfx

## Wire in the AppHost

`AddCertificate` reads the platform X.509 store (`StoreName.My` at `StoreLocation.CurrentUser` or `LocalMachine`). It does not create the cert. After the Linux openssl commands, import `app.cer` into CurrentUser\My before calling `AddCertificate`. Never upload the PFX or private key to Entra; Graph `keyCredentials` take the public CER only.

```csharp
var thumbprint = builder.AddParameter("api-cert-thumbprint", secret: true);
var cert = builder.AddCertificate("api-cert", thumbprint, StoreLocation.CurrentUser);

var api = builder.AddAzureAppRegistration("api")
    .WithKeyCredential(cert);
```

Then set the secret (do not commit it):

```bash
dotnet user-secrets set "Parameters:api-cert-thumbprint" "<Thumbprint>"
```

- `AddCertificate(name, thumbprintParameter, storeLocation)` returns `AsymmetricX509CertResource` (store check only). Missing cert or empty parameter → `FailedToStart`.
- `WithKeyCredential` `WaitFor`s the cert, then emits Graph `keyCredentials` (`AsymmetricX509Cert` / `Verify`) on the create path.
