using System.Text;
using YamlDotNet.Serialization;

namespace Neox.Aspire.Hosting.Azure.Dns;

/// <summary>
/// Generates an OctoDNS config file (<c>octodns.yaml</c>) with <c>env/VAR</c> auth refs (never raw secrets).
/// </summary>
public sealed class OctoDnsConfigWriter
{
    private static readonly ISerializer Serializer = new SerializerBuilder()
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    public const string YamlSourceProviderId = "config";

    /// <summary>
    /// Builds the in-memory config document for the given provider and zones.
    /// </summary>
    public OctoDnsConfigDocument Build(
        DomainOpsProviderResource provider,
        IEnumerable<string> zoneNames,
        string zoneDirectoryRelativeToConfig = "./zones")
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(zoneNames);

        var document = new OctoDnsConfigDocument();

        document.Providers[YamlSourceProviderId] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["class"] = "octodns.provider.yaml.YamlProvider",
            ["directory"] = zoneDirectoryRelativeToConfig
        };

        var target = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["class"] = provider.ProviderClass
        };

        foreach (var (yamlProperty, envVar) in provider.AuthEnvBindings)
        {
            target[yamlProperty] = $"env/{envVar}";
        }

        foreach (var (yamlProperty, literal) in provider.LiteralSettings)
        {
            target[yamlProperty] = literal;
        }

        document.Providers[provider.Name] = target;

        foreach (var zoneName in zoneNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(zoneName))
            {
                continue;
            }

            var fqdn = zoneName.Trim().TrimEnd('.') + ".";
            document.Zones[fqdn] = new OctoDnsZoneTarget
            {
                Sources = [YamlSourceProviderId],
                Targets = [provider.Name]
            };
        }

        return document;
    }

    /// <summary>
    /// Serializes the config document to YAML text.
    /// </summary>
    public string WriteConfigYaml(
        DomainOpsProviderResource provider,
        IEnumerable<string> zoneNames,
        string zoneDirectoryRelativeToConfig = "./zones")
    {
        var document = Build(provider, zoneNames, zoneDirectoryRelativeToConfig);
        return "---" + Environment.NewLine + Serializer.Serialize(document);
    }

    /// <summary>
    /// Writes the config YAML to <paramref name="configPath"/>.
    /// </summary>
    public string WriteToFile(
        DomainOpsProviderResource provider,
        IEnumerable<string> zoneNames,
        string configPath,
        string zoneDirectoryRelativeToConfig = "./zones")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configPath);

        var directory = Path.GetDirectoryName(Path.GetFullPath(configPath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var yaml = WriteConfigYaml(provider, zoneNames, zoneDirectoryRelativeToConfig);
        File.WriteAllText(configPath, yaml, Encoding.UTF8);
        return configPath;
    }
}
