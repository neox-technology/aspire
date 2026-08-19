using System.Collections;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Neox.Keycloak.Provisioning.Realm.Generators.Internal;

/// <summary>
/// Parses Keycloak Admin REST OpenAPI and emits realm representation POCOs.
/// </summary>
public static class KeycloakRealmProvisioningCodeGen
{
    private const string RootSchemaName = "RealmRepresentation";
    private const string Namespace = "Neox.Keycloak.Provisioning.Realm";

    public static IReadOnlyList<(string HintName, string Source)> Generate(string openApiJson)
    {
        using var document = JsonDocument.Parse(openApiJson);
        var root = document.RootElement;
        if (!root.TryGetProperty("components", out var components) ||
            !components.TryGetProperty("schemas", out var schemasElement) ||
            schemasElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("openapi.json must contain components.schemas.");
        }

        var schemas = ParseSchemas(schemasElement);
        if (!schemas.ContainsKey(RootSchemaName))
        {
            throw new InvalidOperationException($"components.schemas must contain {RootSchemaName}.");
        }

        var closure = CollectTransitiveClosure(schemas, RootSchemaName);
        var unions = CollectPolymorphicUnions(schemas, closure);
        var implementingTypes = unions.ToDictionary(
            u => u.InterfaceName,
            u => (ISet<string>)u.Implementations,
            StringComparer.Ordinal);

        var files = new List<(string HintName, string Source)>();
        files.Add(("KeycloakRealmInfrastructure.g.cs", GenerateInfrastructure()));
        files.Add(("KeycloakRealmJsonOptions.g.cs", GenerateJsonOptions(unions)));

        foreach (var union in unions.OrderBy(u => u.InterfaceName, StringComparer.Ordinal))
        {
            files.Add(($"{union.InterfaceName}.g.cs", GenerateInterface(union)));
            files.Add(($"{union.InterfaceName}JsonConverter.g.cs", GeneratePolymorphicConverter(union)));
        }

        foreach (var schemaName in closure.OrderBy(static n => n, StringComparer.Ordinal))
        {
            var schema = schemas[schemaName];
            if (IsEnumSchema(schema))
            {
                files.Add(($"{schemaName}.g.cs", GenerateEnum(schemaName, schema)));
                continue;
            }

            if (IsPureUnionSchema(schema))
            {
                continue;
            }

            files.Add(($"{schemaName}.g.cs", GenerateClass(schemaName, schema, schemas, implementingTypes)));
        }

        return files;
    }

    public static string ToPropertyName(string jsonName)
    {
        if (string.IsNullOrEmpty(jsonName))
        {
            return "Value";
        }

        return char.ToUpperInvariant(jsonName[0]) + jsonName.Substring(1);
    }

    private static Dictionary<string, JsonElement> ParseSchemas(JsonElement schemasElement)
    {
        var schemas = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in schemasElement.EnumerateObject())
        {
            schemas[property.Name] = property.Value;
        }

        return schemas;
    }

    private static HashSet<string> CollectTransitiveClosure(
        IReadOnlyDictionary<string, JsonElement> schemas,
        string rootSchemaName)
    {
        var closure = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        queue.Enqueue(rootSchemaName);

        while (queue.Count > 0)
        {
            var name = queue.Dequeue();
            if (!closure.Add(name) || !schemas.TryGetValue(name, out var schema))
            {
                continue;
            }

            foreach (var reference in EnumerateReferences(schema))
            {
                if (schemas.ContainsKey(reference))
                {
                    queue.Enqueue(reference);
                }
            }
        }

        return closure;
    }

    private static List<PolymorphicUnion> CollectPolymorphicUnions(
        IReadOnlyDictionary<string, JsonElement> schemas,
        IReadOnlyCollection<string> closure)
    {
        var unions = new Dictionary<string, PolymorphicUnion>(StringComparer.Ordinal);

        foreach (var schemaName in closure)
        {
            if (!schemas.TryGetValue(schemaName, out var schema))
            {
                continue;
            }

            RegisterUnion(unions, schemaName, schema, schemas, closure, schemaName);
            if (!schema.TryGetProperty("properties", out var properties) ||
                properties.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var property in properties.EnumerateObject())
            {
                RegisterUnion(
                    unions,
                    $"{schemaName}_{ToPropertyName(property.Name)}",
                    property.Value,
                    schemas,
                    closure,
                    schemaName,
                    property.Name);
            }
        }

        return unions.Values
            .Where(u => u.Implementations.Count > 1)
            .OrderBy(u => u.InterfaceName, StringComparer.Ordinal)
            .ToList();
    }

    private static void RegisterUnion(
        IDictionary<string, PolymorphicUnion> unions,
        string unionKey,
        JsonElement schema,
        IReadOnlyDictionary<string, JsonElement> schemas,
        IReadOnlyCollection<string> closure,
        string contextSchemaName,
        string? propertyJsonName = null)
    {
        if (!TryGetUnionBranches(schema, out var branches) || branches.Count <= 1)
        {
            return;
        }

        var implementations = branches
            .Where(closure.Contains)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static n => n, StringComparer.Ordinal)
            .ToList();
        if (implementations.Count <= 1)
        {
            return;
        }

        var interfaceName = $"I{unionKey}";
        if (!unions.TryGetValue(interfaceName, out var union))
        {
            union = new PolymorphicUnion(interfaceName, contextSchemaName, propertyJsonName);
            unions[interfaceName] = union;
        }

        foreach (var implementation in implementations)
        {
            union.Implementations.Add(implementation);
        }
    }

    private static bool TryGetUnionBranches(JsonElement schema, out List<string> branches)
    {
        branches = [];
        foreach (var keyword in new[] { "oneOf", "anyOf" })
        {
            if (!schema.TryGetProperty(keyword, out var unionElement) ||
                unionElement.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var branch in unionElement.EnumerateArray())
            {
                if (TryGetRefName(branch, out var refName))
                {
                    branches.Add(refName);
                }
            }

            if (branches.Count > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsEnumSchema(JsonElement schema)
    {
        return schema.TryGetProperty("enum", out var enumElement) &&
               enumElement.ValueKind == JsonValueKind.Array &&
               enumElement.GetArrayLength() > 0;
    }

    private static bool IsPureUnionSchema(JsonElement schema)
    {
        return TryGetUnionBranches(schema, out var branches) &&
               branches.Count > 1 &&
               (!schema.TryGetProperty("properties", out var properties) ||
                properties.ValueKind != JsonValueKind.Object);
    }

    private static string GenerateInfrastructure()
    {
        var sb = new StringBuilder();
        AppendFileHeader(sb);
        sb.AppendLine("internal static class KeycloakRealmInfrastructure");
        sb.AppendLine("{");
        sb.AppendLine("    internal static ConcurrentDictionary<string, object> CreateAdditionalProperties() =>");
        sb.AppendLine("        new(StringComparer.Ordinal);");
        sb.AppendLine();
        sb.AppendLine("    internal static ConcurrentBag<T> CreateBag<T>() => [];");
        sb.AppendLine();
        sb.AppendLine("    internal static ConcurrentDictionary<string, T> CreateDictionary<T>() =>");
        sb.AppendLine("        new(StringComparer.Ordinal);");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("internal sealed class ConcurrentBagJsonConverterFactory : JsonConverterFactory");
        sb.AppendLine("{");
        sb.AppendLine("    public override bool CanConvert(Type typeToConvert) =>");
        sb.AppendLine("        typeToConvert.IsGenericType &&");
        sb.AppendLine("        typeToConvert.GetGenericTypeDefinition() == typeof(ConcurrentBag<>);");
        sb.AppendLine();
        sb.AppendLine("    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)");
        sb.AppendLine("    {");
        sb.AppendLine("        var itemType = typeToConvert.GetGenericArguments()[0];");
        sb.AppendLine("        var converterType = typeof(ConcurrentBagJsonConverter<>).MakeGenericType(itemType);");
        sb.AppendLine("        return (JsonConverter)Activator.CreateInstance(converterType)!;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("internal sealed class ConcurrentBagJsonConverter<T> : JsonConverter<ConcurrentBag<T>>");
        sb.AppendLine("{");
        sb.AppendLine("    public override ConcurrentBag<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)");
        sb.AppendLine("    {");
        sb.AppendLine("        var items = JsonSerializer.Deserialize<List<T>>(ref reader, options);");
        sb.AppendLine("        return items is null ? null : new ConcurrentBag<T>(items);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public override void Write(Utf8JsonWriter writer, ConcurrentBag<T> value, JsonSerializerOptions options)");
        sb.AppendLine("    {");
        sb.AppendLine("        JsonSerializer.Serialize(writer, value.ToArray(), options);");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("internal sealed class ConcurrentDictionaryJsonConverterFactory : JsonConverterFactory");
        sb.AppendLine("{");
        sb.AppendLine("    public override bool CanConvert(Type typeToConvert) =>");
        sb.AppendLine("        typeToConvert.IsGenericType &&");
        sb.AppendLine("        typeToConvert.GetGenericTypeDefinition() == typeof(ConcurrentDictionary<,>);");
        sb.AppendLine();
        sb.AppendLine("    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)");
        sb.AppendLine("    {");
        sb.AppendLine("        var keyType = typeToConvert.GetGenericArguments()[0];");
        sb.AppendLine("        var valueType = typeToConvert.GetGenericArguments()[1];");
        sb.AppendLine("        var converterType = typeof(ConcurrentDictionaryJsonConverter<,>).MakeGenericType(keyType, valueType);");
        sb.AppendLine("        return (JsonConverter)Activator.CreateInstance(converterType)!;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("internal sealed class ConcurrentDictionaryJsonConverter<TKey, TValue> : JsonConverter<ConcurrentDictionary<TKey, TValue>>");
        sb.AppendLine("    where TKey : notnull");
        sb.AppendLine("{");
        sb.AppendLine("    public override ConcurrentDictionary<TKey, TValue>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)");
        sb.AppendLine("    {");
        sb.AppendLine("        var dictionary = JsonSerializer.Deserialize<Dictionary<TKey, TValue>>(ref reader, options);");
        sb.AppendLine("        return dictionary is null ? null : new ConcurrentDictionary<TKey, TValue>(dictionary);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public override void Write(Utf8JsonWriter writer, ConcurrentDictionary<TKey, TValue> value, JsonSerializerOptions options)");
        sb.AppendLine("    {");
        sb.AppendLine("        JsonSerializer.Serialize(writer, value.ToDictionary(static pair => pair.Key, static pair => pair.Value), options);");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateJsonOptions(IReadOnlyList<PolymorphicUnion> unions)
    {
        var sb = new StringBuilder();
        AppendFileHeader(sb);
        sb.AppendLine("/// <summary>Default <see cref=\"JsonSerializerOptions\"/> for Keycloak realm documents.</summary>");
        sb.AppendLine("public static class KeycloakRealmJsonOptions");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>Shared options for realm import/export JSON.</summary>");
        sb.AppendLine("    public static JsonSerializerOptions Default { get; } = CreateDefault();");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Creates a copy of the default options.</summary>");
        sb.AppendLine("    public static JsonSerializerOptions CreateDefault()");
        sb.AppendLine("    {");
        sb.AppendLine("        var options = new JsonSerializerOptions");
        sb.AppendLine("        {");
        sb.AppendLine("            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,");
        sb.AppendLine("            PropertyNamingPolicy = null,");
        sb.AppendLine("            WriteIndented = true,");
        sb.AppendLine("        };");
        sb.AppendLine("        options.Converters.Add(new ConcurrentBagJsonConverterFactory());");
        sb.AppendLine("        options.Converters.Add(new ConcurrentDictionaryJsonConverterFactory());");
        foreach (var union in unions)
        {
            sb.Append("        options.Converters.Add(new ").Append(union.InterfaceName)
                .AppendLine("JsonConverter());");
        }

        sb.AppendLine("        return options;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateInterface(PolymorphicUnion union)
    {
        var sb = new StringBuilder();
        AppendFileHeader(sb);
        sb.Append("/// <summary>Polymorphic union for ").Append(EscapeXml(union.ContextSchemaName ?? string.Empty));
        if (!string.IsNullOrEmpty(union.PropertyJsonName))
        {
            sb.Append('.').Append(EscapeXml(union.PropertyJsonName ?? string.Empty));
        }

        sb.AppendLine(".</summary>");
        sb.Append("public interface ").Append(union.InterfaceName).AppendLine();
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>Discriminator value when present in JSON.</summary>");
        sb.AppendLine("    string? Type { get; }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GeneratePolymorphicConverter(PolymorphicUnion union)
    {
        var sb = new StringBuilder();
        AppendFileHeader(sb);
        sb.Append("internal sealed class ").Append(union.InterfaceName).Append("JsonConverter : JsonConverter<")
            .Append(union.InterfaceName).AppendLine(">");
        sb.AppendLine("{");
        sb.Append("    public override ").Append(union.InterfaceName).Append("? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)");
        sb.AppendLine();
        sb.AppendLine("    {");
        sb.AppendLine("        if (reader.TokenType == JsonTokenType.Null)");
        sb.AppendLine("        {");
        sb.AppendLine("            return null;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        using var document = JsonDocument.ParseValue(ref reader);");
        sb.AppendLine("        var root = document.RootElement;");
        sb.AppendLine("        var discriminator = TryReadDiscriminator(root);");
        sb.AppendLine();
        foreach (var implementation in union.Implementations.OrderBy(static n => n, StringComparer.Ordinal))
        {
            sb.Append("        if (discriminator is null || string.Equals(discriminator, \"")
                .Append(EscapeString(implementation)).Append("\", StringComparison.OrdinalIgnoreCase))");
            sb.AppendLine();
            sb.AppendLine("        {");
            sb.Append("            var candidate = root.Deserialize<").Append(implementation).AppendLine(">(options);");
            sb.AppendLine("            if (candidate is not null)");
            sb.AppendLine("            {");
            sb.AppendLine("                return candidate;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        sb.AppendLine("        throw new JsonException(\"Unable to deserialize polymorphic value for " +
                      union.InterfaceName + ".\");");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.Append("    public override void Write(Utf8JsonWriter writer, ").Append(union.InterfaceName)
            .AppendLine(" value, JsonSerializerOptions options)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (value is null)");
        sb.AppendLine("        {");
        sb.AppendLine("            writer.WriteNullValue();");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        JsonSerializer.Serialize(writer, value, value.GetType(), options);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private static string? TryReadDiscriminator(JsonElement root)");
        sb.AppendLine("    {");
        sb.AppendLine("        foreach (var propertyName in new[] { \"type\", \"$type\" })");
        sb.AppendLine("        {");
        sb.AppendLine("            if (root.TryGetProperty(propertyName, out var property) &&");
        sb.AppendLine("                property.ValueKind == JsonValueKind.String)");
        sb.AppendLine("            {");
        sb.AppendLine("                return property.GetString();");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        return null;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateEnum(string schemaName, JsonElement schema)
    {
        var sb = new StringBuilder();
        AppendFileHeader(sb);
        sb.Append("public enum ").Append(schemaName).AppendLine();
        sb.AppendLine("{");
        var values = schema.GetProperty("enum");
        var index = 0;
        foreach (var value in values.EnumerateArray())
        {
            var literal = value.GetString() ?? $"Value{index}";
            var memberName = ToEnumMemberName(literal);
            if (index > 0)
            {
                sb.AppendLine(",");
            }

            sb.Append("    [JsonStringEnumMemberName(\"").Append(EscapeString(literal)).Append("\")]");
            sb.AppendLine();
            sb.Append("    ").Append(memberName).Append(" = ").Append(index);
            index++;
        }

        sb.AppendLine();
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateClass(
        string schemaName,
        JsonElement schema,
        IReadOnlyDictionary<string, JsonElement> schemas,
        IReadOnlyDictionary<string, ISet<string>> implementingTypes)
    {
        var implementedInterfaces = implementingTypes
            .Where(pair => pair.Value.Contains(schemaName))
            .Select(pair => pair.Key)
            .OrderBy(static n => n, StringComparer.Ordinal)
            .ToList();

        var mergedSchema = MergeAllOf(schema, schemas);
        var sb = new StringBuilder();
        AppendFileHeader(sb);
        sb.Append("public partial class ").Append(schemaName);
        if (implementedInterfaces.Count > 0)
        {
            sb.Append(" : ").Append(string.Join(", ", implementedInterfaces));
        }

        sb.AppendLine();
        sb.AppendLine("{");

        if (implementedInterfaces.Count > 0)
        {
            sb.AppendLine("    /// <inheritdoc />");
            sb.AppendLine("    [JsonIgnore]");
            sb.AppendLine("    public string? Type => GetType().Name;");
            sb.AppendLine();
        }

        if (mergedSchema.TryGetProperty("properties", out var properties) &&
            properties.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in properties.EnumerateObject().OrderBy(static p => p.Name, StringComparer.Ordinal))
            {
                if (property.Value.TryGetProperty("readOnly", out var readOnly) &&
                    readOnly.ValueKind == JsonValueKind.True)
                {
                    continue;
                }

                var propertyType = ResolveType(property.Value, schemas, schemaName, property.Name, implementingTypes);
                var obsolete = property.Value.TryGetProperty("deprecated", out var deprecated) &&
                               deprecated.ValueKind == JsonValueKind.True;
                if (obsolete)
                {
                    sb.AppendLine("    [Obsolete]");
                }

                sb.Append("    [JsonPropertyName(\"").Append(EscapeString(property.Name)).AppendLine("\")]");
                sb.Append("    public ").Append(propertyType).Append(' ').Append(ToPropertyName(property.Name))
                    .AppendLine(" { get; set; }");
                sb.AppendLine();
            }
        }

        sb.AppendLine("    [JsonExtensionData]");
        sb.AppendLine("    public ConcurrentDictionary<string, object>? AdditionalProperties { get; set; }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static JsonElement MergeAllOf(JsonElement schema, IReadOnlyDictionary<string, JsonElement> schemas)
    {
        if (!schema.TryGetProperty("allOf", out var allOf) || allOf.ValueKind != JsonValueKind.Array)
        {
            return schema;
        }

        var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var branch in allOf.EnumerateArray())
        {
            JsonElement branchSchema = branch;
            if (TryGetRefName(branch, out var refName) && schemas.TryGetValue(refName, out var resolved))
            {
                branchSchema = resolved;
            }

            if (branchSchema.TryGetProperty("properties", out var branchProperties) &&
                branchProperties.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in branchProperties.EnumerateObject())
                {
                    properties[property.Name] = property.Value;
                }
            }
        }

        if (schema.TryGetProperty("properties", out var ownProperties) &&
            ownProperties.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in ownProperties.EnumerateObject())
            {
                properties[property.Name] = property.Value;
            }
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteStartObject("properties");
            foreach (var property in properties.OrderBy(static p => p.Key, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Key);
                property.Value.WriteTo(writer);
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        using var document = JsonDocument.Parse(stream.ToArray());
        return document.RootElement.Clone();
    }

    private static string ResolveType(
        JsonElement schema,
        IReadOnlyDictionary<string, JsonElement> schemas,
        string contextSchemaName,
        string propertyJsonName,
        IReadOnlyDictionary<string, ISet<string>> implementingTypes)
    {
        if (TryGetUnionBranches(schema, out var branches) && branches.Count > 1)
        {
            return $"I{contextSchemaName}_{ToPropertyName(propertyJsonName)}?";
        }

        if (TryGetRefName(schema, out var refName))
        {
            if (schemas.TryGetValue(refName, out var refSchema) && IsEnumSchema(refSchema))
            {
                return refName + "?";
            }

            return refName + "?";
        }

        if (schema.TryGetProperty("allOf", out var allOf) && allOf.ValueKind == JsonValueKind.Array)
        {
            foreach (var branch in allOf.EnumerateArray())
            {
                if (TryGetRefName(branch, out var branchRef))
                {
                    return branchRef + "?";
                }
            }
        }

        if (schema.TryGetProperty("type", out var typeElement))
        {
            switch (typeElement.GetString())
            {
                case "string":
                    if (IsEnumSchema(schema))
                    {
                        return ToPropertyName(propertyJsonName) + "Enum?";
                    }

                    return "string?";
                case "boolean":
                    return "bool?";
                case "integer":
                    return schema.TryGetProperty("format", out var format) &&
                           string.Equals(format.GetString(), "int64", StringComparison.Ordinal)
                        ? "long?"
                        : "int?";
                case "number":
                    return "double?";
                case "array":
                    if (schema.TryGetProperty("items", out var items))
                    {
                        var itemType = ResolveItemsType(items, schemas);
                        return $"ConcurrentBag<{itemType}>?";
                    }

                    return "ConcurrentBag<object>?";
                case "object":
                    if (schema.TryGetProperty("additionalProperties", out var additionalProperties))
                    {
                        var valueType = ResolveAdditionalPropertyType(additionalProperties, schemas);
                        return $"ConcurrentDictionary<string, {valueType}>?";
                    }

                    return "ConcurrentDictionary<string, object>?";
            }
        }

        return "object?";
    }

    private static string ResolveItemsType(JsonElement items, IReadOnlyDictionary<string, JsonElement> schemas)
    {
        if (TryGetRefName(items, out var refName))
        {
            return refName;
        }

        if (items.TryGetProperty("type", out var typeElement))
        {
            return typeElement.GetString() switch
            {
                "string" => "string",
                "boolean" => "bool",
                "integer" => items.TryGetProperty("format", out var format) &&
                             string.Equals(format.GetString(), "int64", StringComparison.Ordinal)
                    ? "long"
                    : "int",
                "number" => "double",
                "object" => "object",
                _ => "object",
            };
        }

        return "object";
    }

    private static string ResolveAdditionalPropertyType(
        JsonElement additionalProperties,
        IReadOnlyDictionary<string, JsonElement> schemas)
    {
        if (additionalProperties.ValueKind == JsonValueKind.False)
        {
            return "object";
        }

        if (TryGetRefName(additionalProperties, out var refName))
        {
            return refName;
        }

        if (additionalProperties.TryGetProperty("type", out var typeElement))
        {
            return typeElement.GetString() switch
            {
                "string" => "string",
                "boolean" => "bool",
                "integer" => additionalProperties.TryGetProperty("format", out var format) &&
                               string.Equals(format.GetString(), "int64", StringComparison.Ordinal)
                    ? "long"
                    : "int",
                "number" => "double",
                "array" => additionalProperties.TryGetProperty("items", out var items)
                    ? $"ConcurrentBag<{ResolveItemsType(items, schemas)}>"
                    : "ConcurrentBag<object>",
                "object" => additionalProperties.TryGetProperty("additionalProperties", out var nested)
                    ? $"ConcurrentDictionary<string, {ResolveAdditionalPropertyType(nested, schemas)}>"
                    : "object",
                _ => "object",
            };
        }

        return "object";
    }

    private static IEnumerable<string> EnumerateReferences(JsonElement schema)
    {
        if (TryGetRefName(schema, out var refName))
        {
            yield return refName;
            yield break;
        }

        foreach (var keyword in new[] { "oneOf", "anyOf", "allOf" })
        {
            if (!schema.TryGetProperty(keyword, out var array) || array.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var branch in array.EnumerateArray())
            {
                foreach (var nested in EnumerateReferences(branch))
                {
                    yield return nested;
                }
            }
        }

        if (schema.TryGetProperty("properties", out var properties) &&
            properties.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in properties.EnumerateObject())
            {
                foreach (var nested in EnumerateReferences(property.Value))
                {
                    yield return nested;
                }
            }
        }

        if (schema.TryGetProperty("items", out var items))
        {
            foreach (var nested in EnumerateReferences(items))
            {
                yield return nested;
            }
        }

        if (schema.TryGetProperty("additionalProperties", out var additionalProperties) &&
            additionalProperties.ValueKind == JsonValueKind.Object)
        {
            foreach (var nested in EnumerateReferences(additionalProperties))
            {
                yield return nested;
            }
        }
    }

    private static bool TryGetRefName(JsonElement schema, out string refName)
    {
        refName = string.Empty;
        if (!schema.TryGetProperty("$ref", out var refElement))
        {
            return false;
        }

        var value = refElement.GetString();
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        const string prefix = "#/components/schemas/";
        if (value is null || !value.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        refName = value.Substring(prefix.Length);
        return refName.Length > 0;
    }

    private static string ToEnumMemberName(string literal)
    {
        var sanitized = new StringBuilder();
        foreach (var character in literal)
        {
            if (char.IsLetterOrDigit(character))
            {
                sanitized.Append(character);
            }
            else
            {
                sanitized.Append('_');
            }
        }

        var name = sanitized.ToString().Trim('_');
        if (string.IsNullOrEmpty(name))
        {
            return "Unknown";
        }

        if (char.IsDigit(name[0]))
        {
            name = "Value_" + name;
        }

        return name;
    }

    private static void AppendFileHeader(StringBuilder sb)
    {
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System.Collections.Concurrent;");
        sb.AppendLine("using System.Text.Json;");
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine();
        sb.Append("namespace ").Append(Namespace).AppendLine(";");
        sb.AppendLine();
    }

    private static string EscapeXml(string value) =>
        value.Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");

    private static string EscapeString(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private sealed class PolymorphicUnion(string interfaceName, string contextSchemaName, string? propertyJsonName)
    {
        public string InterfaceName { get; } = interfaceName;
        public string ContextSchemaName { get; } = contextSchemaName;
        public string? PropertyJsonName { get; } = propertyJsonName;
        public HashSet<string> Implementations { get; } = new(StringComparer.Ordinal);
    }
}
