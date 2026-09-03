using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// Aspire resource that verifies an X.509 certificate exists in the platform store.
/// Not a provisioning resource; Graph <c>keyCredentials</c> are emitted by
/// <see cref="AzureEntraIdAppRegistrationResource"/> via <c>WithKeyCredential</c>.
/// </summary>
public sealed class AsymmetricX509CertResource : Resource
{
    /// <summary>
    /// Initializes a new <see cref="AsymmetricX509CertResource"/>.
    /// </summary>
    /// <param name="name">Aspire resource name.</param>
    /// <param name="thumbprint">Parameter whose value is the certificate thumbprint (hex; spaces ignored).</param>
    /// <param name="storeLocation">CurrentUser or LocalMachine.</param>
    public AsymmetricX509CertResource(string name, ParameterResource thumbprint, StoreLocation storeLocation)
        : base(name)
    {
        ArgumentNullException.ThrowIfNull(thumbprint);

        ThumbprintParameter = thumbprint;
        StoreLocation = storeLocation;
    }

    /// <summary>
    /// Parameter that supplies the certificate thumbprint.
    /// </summary>
    public ParameterResource ThumbprintParameter { get; }

    /// <summary>
    /// Normalized certificate thumbprint. Resolves <see cref="ThumbprintParameter"/> on demand.
    /// </summary>
    public string Thumbprint => NormalizeThumbprint(ResolveThumbprint());

    /// <summary>
    /// Store location searched by <c>AddCertificate</c>.
    /// </summary>
    public StoreLocation StoreLocation { get; }

    internal static string NormalizeThumbprint(string thumbprint)
    {
        return thumbprint.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
    }

    internal static string KeyCredentialParameterName(string certName)
    {
        ArgumentException.ThrowIfNullOrEmpty(certName);
        return "keyCredential_" + certName.Replace("-", "_", StringComparison.Ordinal);
    }

    internal async Task<string?> TryResolveNormalizedThumbprintAsync(CancellationToken cancellationToken)
    {
        try
        {
            var value = await ThumbprintParameter.GetValueAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return NormalizeThumbprint(value);
        }
        catch (Exception)
        {
            return null;
        }
    }

    internal bool TryFind()
    {
        if (!TryFind(Thumbprint, StoreLocation, out var certificate))
        {
            return false;
        }

        certificate?.Dispose();
        return true;
    }

    internal static bool TryFind(string thumbprint, StoreLocation storeLocation, out X509Certificate2? certificate)
    {
        certificate = null;
        using var store = new X509Store(StoreName.My, storeLocation);
        try
        {
            store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
        }
        catch (CryptographicException)
        {
            return false;
        }

        var found = store.Certificates.Find(
            X509FindType.FindByThumbprint,
            NormalizeThumbprint(thumbprint),
            validOnly: false);
        if (found.Count == 0)
        {
            return false;
        }

        certificate = new X509Certificate2(found[0]);
        return true;
    }

    internal string ExportPublicKeyBase64()
    {
        if (!TryFind(Thumbprint, StoreLocation, out var certificate) || certificate is null)
        {
            throw new InvalidOperationException(
                $"Certificate '{Thumbprint}' was not found in StoreName.My at {StoreLocation}.");
        }

        using (certificate)
        {
            return Convert.ToBase64String(certificate.Export(X509ContentType.Cert));
        }
    }

    private string ResolveThumbprint()
    {
        var value = ThumbprintParameter.GetValueAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();
        ArgumentException.ThrowIfNullOrEmpty(value, ThumbprintParameter.Name);
        return value;
    }
}
