using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Neox.Azure.Provisioning.Graph.Generators.Internal;

/// <summary>
/// Parses official msgraph-bicep-types <c>types.json</c> and emits Azure.Provisioning constructs.
/// </summary>
public static class GraphProvisioningCodeGen
{
    private const int FlagRequired = 1;
    private const int FlagReadOnly = 2;
    private const int FlagIdentifier = 16;

    private static readonly Dictionary<string, string> ResourceClassNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Microsoft.Graph/applications"] = "GraphApplication",
        ["Microsoft.Graph/servicePrincipals"] = "GraphServicePrincipal",
        ["Microsoft.Graph/groups"] = "GraphGroup",
        ["Microsoft.Graph/users"] = "GraphUser",
        ["Microsoft.Graph/oauth2PermissionGrants"] = "GraphOauth2PermissionGrant",
        ["Microsoft.Graph/appRoleAssignedTo"] = "GraphAppRoleAssignedTo",
        ["Microsoft.Graph/applications/federatedIdentityCredentials"] = "GraphFederatedIdentityCredential",
    };

    public static IReadOnlyList<(string HintName, string Source)> Generate(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("types.json root must be an array.");
        }

        var types = new List<JsonElement>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            types.Add(element);
        }

        var files = new List<(string HintName, string Source)>();
        var emittedModels = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < types.Count; i++)
        {
            if (!TryGetTypeKind(types[i], out var kind) || kind != "ObjectType")
            {
                continue;
            }

            var name = GetString(types[i], "name");
            if (string.IsNullOrEmpty(name) || name.Contains('/') || name == "MicrosoftGraphBicepExtensionConfig")
            {
                continue;
            }

            var className = ToModelClassName(name);
            if (!emittedModels.Add(className))
            {
                continue;
            }

            files.Add(($"{className}.g.cs", GenerateModel(types, i, className, name)));
        }

        for (var i = 0; i < types.Count; i++)
        {
            if (!TryGetTypeKind(types[i], out var kind) || kind != "ResourceType")
            {
                continue;
            }

            var resourceName = GetString(types[i], "name");
            if (string.IsNullOrEmpty(resourceName) || !TrySplitResource(resourceName, out var typeName, out var apiVersion))
            {
                continue;
            }

            if (!ResourceClassNames.TryGetValue(typeName, out var className))
            {
                continue;
            }

            files.Add(($"{className}.g.cs", GenerateResource(types, i, className, typeName, apiVersion)));
        }

        return files;
    }

    public static string ToModelClassName(string jsonName)
    {
        const string prefix = "MicrosoftGraph";
        if (jsonName.StartsWith(prefix, StringComparison.Ordinal))
        {
            return "Graph" + jsonName.Substring(prefix.Length);
        }

        return jsonName;
    }

    public static string ToPropertyName(string jsonName)
    {
        if (string.IsNullOrEmpty(jsonName))
        {
            return "Value";
        }

        return char.ToUpperInvariant(jsonName[0]) + jsonName.Substring(1);
    }

    private static string GenerateModel(List<JsonElement> types, int index, string className, string jsonName)
    {
        var properties = ReadProperties(types, types[index]);
        var sb = new StringBuilder();
        AppendFileHeader(sb);
        sb.AppendLine("/// <summary>");
        sb.Append("/// Graph construct <c>").Append(EscapeXml(jsonName)).AppendLine("</c>.");
        sb.AppendLine("/// </summary>");
        sb.Append("public partial class ").Append(className).AppendLine(" : ProvisionableConstruct");
        sb.AppendLine("{");
        AppendFields(sb, properties);
        sb.AppendLine();
        AppendPropertyMembers(sb, properties);
        sb.AppendLine();
        sb.Append("    /// <summary>Creates a new ").Append(EscapeXml(className)).AppendLine(".</summary>");
        sb.Append("    public ").Append(className).AppendLine("()");
        sb.AppendLine("    {");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <inheritdoc />");
        sb.AppendLine("    protected override void DefineProvisionableProperties()");
        sb.AppendLine("    {");
        sb.AppendLine("        base.DefineProvisionableProperties();");
        AppendDefineCalls(sb, properties);
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateResource(
        List<JsonElement> types,
        int resourceIndex,
        string className,
        string typeName,
        string apiVersion)
    {
        var resource = types[resourceIndex];
        var bodyRef = resource.GetProperty("body");
        var body = Resolve(types, bodyRef);
        var properties = ReadProperties(types, body);
        var identifier = properties.FirstOrDefault(p => p.IsIdentifier && p.Kind is PropKind.String);
        var resourceFlags = resource.TryGetProperty("flags", out var flagsEl) && flagsEl.TryGetInt32(out var rf) ? rf : 0;
        var isReadOnlyResource = (resourceFlags & FlagReadOnly) != 0;

        var sb = new StringBuilder();
        AppendFileHeader(sb);
        sb.AppendLine("/// <summary>");
        sb.Append("/// Microsoft Graph resource <c>").Append(EscapeXml(typeName)).Append('@').Append(EscapeXml(apiVersion)).AppendLine("</c>.");
        sb.AppendLine("/// </summary>");
        sb.Append("public partial class ").Append(className).AppendLine(" : ProvisionableResource");
        sb.AppendLine("{");
        sb.Append("    /// <summary>ARM/Graph resource type name.</summary>").AppendLine();
        sb.Append("    public const string ResourceTypeName = \"").Append(EscapeCSharp(typeName)).AppendLine("\";");
        sb.Append("    /// <summary>Default API version.</summary>").AppendLine();
        sb.Append("    public const string ResourceApiVersion = \"").Append(EscapeCSharp(apiVersion)).AppendLine("\";");
        sb.AppendLine();
        AppendFields(sb, properties);
        sb.AppendLine();
        AppendPropertyMembers(sb, properties);
        sb.AppendLine();
        sb.Append("    /// <summary>Creates a ").Append(EscapeXml(className)).AppendLine(" construct.</summary>");
        sb.Append("    public ").Append(className).AppendLine("(string bicepIdentifier, string? resourceVersion = null)");
        sb.Append("        : base(bicepIdentifier, ResourceTypeName, resourceVersion ?? ResourceApiVersion)").AppendLine();
        sb.AppendLine("    {");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <inheritdoc />");
        sb.AppendLine("    protected override void DefineProvisionableProperties()");
        sb.AppendLine("    {");
        AppendDefineCalls(sb, properties);
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Creates a reference to an existing resource.</summary>");
        sb.Append("    public static ").Append(className).AppendLine(" FromExisting(string bicepIdentifier) =>");
        sb.Append("        new(bicepIdentifier) { IsExistingResource = true };").AppendLine();

        if (identifier is not null)
        {
            var param = identifier.JsonName;
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.Append("    /// Creates a reference to an existing resource identified by <paramref name=\"").Append(param).AppendLine("\"/>.");
            sb.AppendLine("    /// The identifier must be assigned before the resource is marked existing.");
            sb.AppendLine("    /// </summary>");
            sb.Append("    public static ").Append(className).Append(" FromExisting(string bicepIdentifier, BicepValue<string> ").Append(param).AppendLine(", string? resourceVersion = default)");
            sb.AppendLine("    {");
            sb.Append("        var resource = new ").Append(className).AppendLine("(bicepIdentifier, resourceVersion);");
            sb.Append("        resource.").Append(identifier.PropertyName).Append(" = ").Append(param).AppendLine(";");
            sb.AppendLine("        resource.IsExistingResource = true;");
            sb.AppendLine("        return resource;");
            sb.AppendLine("    }");
        }

        if (isReadOnlyResource)
        {
            // keep FromExisting factories; all non-identifier properties are already outputs
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void AppendFileHeader(StringBuilder sb)
    {
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using Azure.Provisioning;");
        sb.AppendLine("using Azure.Provisioning.Primitives;");
        sb.AppendLine();
        sb.AppendLine("namespace Neox.Azure.Provisioning.Graph;");
        sb.AppendLine();
    }

    private static void AppendFields(StringBuilder sb, List<PropertyModel> properties)
    {
        foreach (var property in properties)
        {
            sb.Append("    private ").Append(property.FieldClrType).Append("? ").Append(property.FieldName).AppendLine(";");
        }
    }

    private static void AppendPropertyMembers(StringBuilder sb, List<PropertyModel> properties)
    {
        for (var i = 0; i < properties.Count; i++)
        {
            var property = properties[i];
            if (i > 0)
            {
                sb.AppendLine();
            }

            if (!string.IsNullOrEmpty(property.Description))
            {
                sb.AppendLine("    /// <summary>");
                sb.Append("    /// ").Append(EscapeXml(property.Description ?? "")).AppendLine();
                sb.AppendLine("    /// </summary>");
            }

            sb.Append("    public ").Append(property.PublicClrType).Append(' ').Append(property.PropertyName).AppendLine();
            sb.AppendLine("    {");
            sb.Append("        get { Initialize(); return ").Append(property.FieldName).AppendLine("!; }");
            if (!property.IsOutputOnly)
            {
                if (property.Kind is PropKind.Model)
                {
                    sb.Append("        set { Initialize(); AssignOrReplace(ref ").Append(property.FieldName).AppendLine(", value); }");
                }
                else
                {
                    sb.Append("        set { Initialize(); ").Append(property.FieldName).AppendLine("!.Assign(value); }");
                }
            }

            sb.AppendLine("    }");
        }
    }

    private static void AppendDefineCalls(StringBuilder sb, List<PropertyModel> properties)
    {
        foreach (var property in properties)
        {
            sb.Append("        ").Append(property.FieldName).Append(" = ");
            switch (property.Kind)
            {
                case PropKind.Model:
                    sb.Append("DefineModelProperty<").Append(property.ItemClrType).Append(">(nameof(").Append(property.PropertyName).Append("), [\"").Append(property.JsonName).Append("\"]");
                    AppendDefineFlags(sb, property);
                    sb.AppendLine(");");
                    break;
                case PropKind.StringList:
                case PropKind.BoolList:
                case PropKind.IntList:
                case PropKind.ModelList:
                    sb.Append("DefineListProperty<").Append(property.ItemClrType).Append(">(nameof(").Append(property.PropertyName).Append("), [\"").Append(property.JsonName).Append("\"]");
                    AppendDefineFlags(sb, property);
                    sb.AppendLine(");");
                    break;
                default:
                    sb.Append("DefineProperty<").Append(property.ItemClrType).Append(">(nameof(").Append(property.PropertyName).Append("), [\"").Append(property.JsonName).Append("\"]");
                    AppendDefineFlags(sb, property);
                    sb.AppendLine(");");
                    break;
            }
        }
    }

    private static void AppendDefineFlags(StringBuilder sb, PropertyModel property)
    {
        if (property.IsOutputOnly)
        {
            sb.Append(", isOutput: true");
        }

        if (property.IsRequired && !property.IsOutputOnly)
        {
            sb.Append(", isRequired: true");
        }
    }

    private static List<PropertyModel> ReadProperties(List<JsonElement> types, JsonElement objectType)
    {
        var list = new List<PropertyModel>();
        if (!objectType.TryGetProperty("properties", out var properties) || properties.ValueKind != JsonValueKind.Object)
        {
            return list;
        }

        foreach (var property in properties.EnumerateObject())
        {
            var flags = 0;
            if (property.Value.TryGetProperty("flags", out var flagsEl) && flagsEl.TryGetInt32(out var f))
            {
                flags = f;
            }

            // Skip DeployTimeConstant resource metadata (`type` / `apiVersion` on Graph resources),
            // not nested ObjectType fields such as GraphPermissionScope.type (Admin | User).
            if (property.Name is "type" or "apiVersion" && (flags & FlagReadOnly) != 0)
            {
                continue;
            }

            var description = property.Value.TryGetProperty("description", out var descEl) && descEl.ValueKind == JsonValueKind.String
                ? descEl.GetString()
                : null;

            if (!property.Value.TryGetProperty("type", out var typeEl))
            {
                continue;
            }

            var resolved = ResolveClr(types, typeEl);
            if (resolved is null)
            {
                continue;
            }

            var isReadOnly = (flags & FlagReadOnly) != 0;
            var isIdentifier = (flags & FlagIdentifier) != 0;
            var isRequired = (flags & FlagRequired) != 0;
            // Identifier must remain assignable (existing lookup) even on otherwise read-only resources.
            var isOutputOnly = isReadOnly && !isIdentifier;

            list.Add(new PropertyModel(
                property.Name,
                ToPropertyName(property.Name),
                "_" + property.Name,
                resolved.Value.Kind,
                resolved.Value.PublicClrType,
                resolved.Value.FieldClrType,
                resolved.Value.ItemClrType,
                isRequired,
                isOutputOnly,
                isIdentifier,
                description));
        }

        return list;
    }

    private static (PropKind Kind, string PublicClrType, string FieldClrType, string ItemClrType)? ResolveClr(List<JsonElement> types, JsonElement typeRef)
    {
        var resolved = Resolve(types, typeRef);
        if (!TryGetTypeKind(resolved, out var kind))
        {
            return null;
        }

        switch (kind)
        {
            case "AnyType":
                return null;
            case "StringType":
            case "StringLiteralType":
                return (PropKind.String, "BicepValue<string>", "BicepValue<string>", "string");
            case "BooleanType":
                return (PropKind.Bool, "BicepValue<bool>", "BicepValue<bool>", "bool");
            case "IntegerType":
                return (PropKind.Int, "BicepValue<int>", "BicepValue<int>", "int");
            case "UnionType":
                return (PropKind.String, "BicepValue<string>", "BicepValue<string>", "string");
            case "ObjectType":
                var objectName = GetString(resolved, "name");
                if (string.IsNullOrEmpty(objectName) || objectName.Contains('/'))
                {
                    return null;
                }

                var model = ToModelClassName(objectName);
                return (PropKind.Model, model, model, model);
            case "ArrayType":
                if (!resolved.TryGetProperty("itemType", out var itemType))
                {
                    return null;
                }

                var item = ResolveClr(types, itemType);
                if (item is null)
                {
                    return null;
                }

                return item.Value.Kind switch
                {
                    PropKind.String => (PropKind.StringList, "BicepList<string>", "BicepList<string>", "string"),
                    PropKind.Bool => (PropKind.BoolList, "BicepList<bool>", "BicepList<bool>", "bool"),
                    PropKind.Int => (PropKind.IntList, "BicepList<int>", "BicepList<int>", "int"),
                    PropKind.Model => (PropKind.ModelList, $"BicepList<{item.Value.ItemClrType}>", $"BicepList<{item.Value.ItemClrType}>", item.Value.ItemClrType),
                    _ => null,
                };
            default:
                return null;
        }
    }

    private static JsonElement Resolve(List<JsonElement> types, JsonElement typeRef)
    {
        if (typeRef.ValueKind == JsonValueKind.Object && typeRef.TryGetProperty("$ref", out var refEl) && refEl.ValueKind == JsonValueKind.String)
        {
            var reference = refEl.GetString() ?? "";
            const string prefix = "#/";
            if (!reference.StartsWith(prefix, StringComparison.Ordinal) ||
                !int.TryParse(reference.Substring(prefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) ||
                index < 0 || index >= types.Count)
            {
                throw new InvalidOperationException($"Invalid type $ref '{reference}'.");
            }

            return types[index];
        }

        return typeRef;
    }

    private static bool TryGetTypeKind(JsonElement element, out string kind)
    {
        kind = "";
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty("$type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        kind = typeEl.GetString() ?? "";
        return kind.Length > 0;
    }

    private static bool TrySplitResource(string resourceName, out string typeName, out string apiVersion)
    {
        typeName = "";
        apiVersion = "";
        var at = resourceName.LastIndexOf('@');
        if (at <= 0 || at == resourceName.Length - 1)
        {
            return false;
        }

        typeName = resourceName.Substring(0, at);
        apiVersion = resourceName.Substring(at + 1);
        return true;
    }

    private static string GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private static string EscapeCSharp(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string EscapeXml(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    private enum PropKind
    {
        String,
        Bool,
        Int,
        Model,
        StringList,
        BoolList,
        IntList,
        ModelList,
    }

    private sealed class PropertyModel
    {
        public PropertyModel(
            string jsonName,
            string propertyName,
            string fieldName,
            PropKind kind,
            string publicClrType,
            string fieldClrType,
            string itemClrType,
            bool isRequired,
            bool isOutputOnly,
            bool isIdentifier,
            string? description)
        {
            JsonName = jsonName;
            PropertyName = propertyName;
            FieldName = fieldName;
            Kind = kind;
            PublicClrType = publicClrType;
            FieldClrType = fieldClrType;
            ItemClrType = itemClrType;
            IsRequired = isRequired;
            IsOutputOnly = isOutputOnly;
            IsIdentifier = isIdentifier;
            Description = description;
        }

        public string JsonName { get; }
        public string PropertyName { get; }
        public string FieldName { get; }
        public PropKind Kind { get; }
        public string PublicClrType { get; }
        public string FieldClrType { get; }
        public string ItemClrType { get; }
        public bool IsRequired { get; }
        public bool IsOutputOnly { get; }
        public bool IsIdentifier { get; }
        public string? Description { get; }
    }
}
