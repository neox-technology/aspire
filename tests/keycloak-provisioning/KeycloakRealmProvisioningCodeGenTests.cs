using Neox.Keycloak.Provisioning.Realm.Generators.Internal;
using Xunit;

namespace Neox.Keycloak.Provisioning.Realm.Tests;

public sealed class KeycloakRealmProvisioningCodeGenTests
{
    [Fact]
    public void ToPropertyName_pascal_cases_camel_json_names()
    {
        Assert.Equal("SsoSessionIdleTimeout", KeycloakRealmProvisioningCodeGen.ToPropertyName("ssoSessionIdleTimeout"));
        Assert.Equal("RedirectUris", KeycloakRealmProvisioningCodeGen.ToPropertyName("redirectUris"));
    }

    [Fact]
    public void Generate_emits_realm_client_and_flow_types()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "openapi.json"));
        var files = KeycloakRealmProvisioningCodeGen.Generate(json);
        var byHint = files.ToDictionary(f => f.HintName, f => f.Source, StringComparer.Ordinal);

        Assert.Contains("RealmRepresentation.g.cs", byHint.Keys);
        Assert.Contains("ClientRepresentation.g.cs", byHint.Keys);
        Assert.Contains("AuthenticationFlowRepresentation.g.cs", byHint.Keys);
        Assert.Contains("KeycloakRealmJsonOptions.g.cs", byHint.Keys);
        Assert.DoesNotContain("AccessToken.g.cs", byHint.Keys);

        var realm = byHint["RealmRepresentation.g.cs"];
        Assert.Contains("public partial class RealmRepresentation", realm, StringComparison.Ordinal);
        Assert.Contains("ConcurrentBag<ClientRepresentation>", realm, StringComparison.Ordinal);
        Assert.Contains("public int? SsoSessionIdleTimeout", realm, StringComparison.Ordinal);

        var client = byHint["ClientRepresentation.g.cs"];
        Assert.Contains("ConcurrentBag<string>", client, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_emits_polymorphic_interface_and_converter_for_oneOf()
    {
        const string openApi = """
            {
              "components": {
                "schemas": {
                  "RealmRepresentation": {
                    "type": "object",
                    "properties": {
                      "realm": { "type": "string" },
                      "policy": {
                        "oneOf": [
                          { "$ref": "#/components/schemas/RolePolicyRepresentation" },
                          { "$ref": "#/components/schemas/GroupPolicyRepresentation" }
                        ]
                      }
                    }
                  },
                  "RolePolicyRepresentation": {
                    "type": "object",
                    "properties": { "type": { "type": "string" }, "name": { "type": "string" } }
                  },
                  "GroupPolicyRepresentation": {
                    "type": "object",
                    "properties": { "type": { "type": "string" }, "groups": { "type": "array", "items": { "type": "string" } } }
                  }
                }
              }
            }
            """;

        var files = KeycloakRealmProvisioningCodeGen.Generate(openApi);
        var byHint = files.ToDictionary(f => f.HintName, f => f.Source, StringComparer.Ordinal);

        Assert.Contains("IRealmRepresentation_Policy.g.cs", byHint.Keys);
        Assert.Contains("IRealmRepresentation_PolicyJsonConverter.g.cs", byHint.Keys);

        var rolePolicy = byHint["RolePolicyRepresentation.g.cs"];
        Assert.Contains(": IRealmRepresentation_Policy", rolePolicy, StringComparison.Ordinal);

        var converter = byHint["IRealmRepresentation_PolicyJsonConverter.g.cs"];
        Assert.Contains("JsonConverter<IRealmRepresentation_Policy>", converter, StringComparison.Ordinal);
    }
}
