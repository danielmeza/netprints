using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;

namespace NetPrints.Extensibility.Settings;

/// <summary>
/// Exports the JSON schema of a settings section, with the NetPrints keywords its metadata attributes add (research R6).
/// </summary>
public static class SettingsSchema
{
    /// <summary>The keyword that marks a property whose change needs a restart.</summary>
    public const string RestartKeyword = "x-netprints-restart";

    /// <summary>The keyword that marks a path property; its value is <c>file</c> or <c>folder</c>.</summary>
    public const string PathKeyword = "x-netprints-path";

    /// <summary>The keyword that names the provider of an open string set.</summary>
    public const string ChoicesKeyword = "x-netprints-choices";

    private const string DescriptionKeyword = "description";
    private const string PropertiesKeyword = "properties";
    private const string RequiredKeyword = "required";
    private const string TypeKeyword = "type";
    private const string DefaultKeyword = "default";
    private const string MinLengthKeyword = "minLength";
    private const string NullType = "null";
    private const string StringType = "string";
    private const string ArrayType = "array";

    /// <summary>
    /// Exports the schema of a section.
    /// </summary>
    /// <param name="typeInfo">The section's source-generated JSON metadata.</param>
    /// <returns>The schema, with <c>description</c> from <see cref="DescriptionAttribute"/> and the <c>x-netprints-*</c> keywords on the properties that carry the metadata attributes.</returns>
    public static JsonNode Export(JsonTypeInfo typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        var options = new JsonSchemaExporterOptions { TreatNullObliviousAsNonNullable = true, TransformSchemaNode = Transform };
        return JsonSchemaExporter.GetJsonSchemaAsNode(typeInfo, options);
    }

    private static void WriteConstraints(ICustomAttributeProvider provider, JsonObject obj)
    {
        if (provider.GetCustomAttributes(typeof(RangeAttribute), true).OfType<RangeAttribute>().FirstOrDefault() is { } range
            && range.Minimum is IConvertible minimum && range.Maximum is IConvertible maximum)
        {
            bool whole = range.OperandType == typeof(int);
            obj["minimum"] = whole ? JsonValue.Create(minimum.ToInt32(CultureInfo.InvariantCulture)) : JsonValue.Create(minimum.ToDouble(CultureInfo.InvariantCulture));
            obj["maximum"] = whole ? JsonValue.Create(maximum.ToInt32(CultureInfo.InvariantCulture)) : JsonValue.Create(maximum.ToDouble(CultureInfo.InvariantCulture));
        }

        if (provider.GetCustomAttributes(typeof(MinLengthAttribute), true).OfType<MinLengthAttribute>().FirstOrDefault() is { } minLength)
        {
            obj["minLength"] = minLength.Length;
        }

        if (provider.GetCustomAttributes(typeof(StringLengthAttribute), true).OfType<StringLengthAttribute>().FirstOrDefault() is { } stringLength)
        {
            obj["maxLength"] = stringLength.MaximumLength;
            if (stringLength.MinimumLength > 0)
            {
                obj["minLength"] = stringLength.MinimumLength;
            }
        }

        if (provider.GetCustomAttributes(typeof(RegularExpressionAttribute), true).OfType<RegularExpressionAttribute>().FirstOrDefault() is { } pattern)
        {
            obj["pattern"] = pattern.Pattern;
        }
    }

    // A list member the file omits loads as the empty list, but the exporter takes a constructor parameter's null as its default.
    private static void DefaultOmittedListToEmpty(JsonObject obj)
    {
        bool isList = obj[TypeKeyword] switch
        {
            JsonArray types => types.Any(type => (string?)type == ArrayType),
            JsonNode type => (string?)type == ArrayType,
            _ => false,
        };
        if (isList && obj.TryGetPropertyValue(DefaultKeyword, out JsonNode? current) && current is null)
        {
            obj[DefaultKeyword] = new JsonArray();
        }
    }

    private static void RequireMembers(JsonTypeInfo typeInfo, JsonObject schema)
    {
        if (schema[PropertiesKeyword] is not JsonObject properties)
        {
            return;
        }

        JsonArray required = schema[RequiredKeyword] as JsonArray ?? [];
        foreach (JsonPropertyInfo property in typeInfo.Properties)
        {
            if (property.AttributeProvider?.IsDefined(typeof(RequiredAttribute), true) != true || properties[property.Name] is not JsonObject member)
            {
                continue;
            }

            if (!required.Any(name => (string?)name == property.Name))
            {
                JsonNode? name = JsonValue.Create(property.Name);
                required.Add(name);
            }

            bool allowEmpty = property.AttributeProvider?.GetCustomAttributes(typeof(RequiredAttribute), true).OfType<RequiredAttribute>().Any(attribute => attribute.AllowEmptyStrings) == true;
            MakeNonNull(member, allowEmpty);
        }

        if (required.Count > 0)
        {
            schema[RequiredKeyword] = required;
        }
    }

    private static void MakeNonNull(JsonObject member, bool allowEmpty)
    {
        if (member[TypeKeyword] is JsonArray types)
        {
            string[] kept = [.. types.Select(type => (string?)type).OfType<string>().Where(type => type != NullType)];
            member[TypeKeyword] = kept.Length == 1 ? kept[0] : new JsonArray([.. kept.Select(type => JsonValue.Create(type))]);
        }

        member.Remove(DefaultKeyword);
        if (!allowEmpty && (string?)member[TypeKeyword] == StringType && member[MinLengthKeyword] is null)
        {
            member[MinLengthKeyword] = 1;
        }
    }

    private static JsonNode Transform(JsonSchemaExporterContext context, JsonNode schema)
    {
        if (schema is JsonObject objectSchema && context.TypeInfo.Kind == JsonTypeInfoKind.Object)
        {
            RequireMembers(context.TypeInfo, objectSchema);
        }

        ICustomAttributeProvider? provider = context.PropertyInfo?.AttributeProvider;
        if (provider is null || schema is not JsonObject obj)
        {
            return schema;
        }

        if (provider.GetCustomAttributes(typeof(DescriptionAttribute), true).OfType<DescriptionAttribute>().FirstOrDefault() is { } description)
        {
            obj[DescriptionKeyword] = description.Description;
        }

        WriteConstraints(provider, obj);
        DefaultOmittedListToEmpty(obj);

        if (provider.IsDefined(typeof(RequiresRestartAttribute), true))
        {
            obj[RestartKeyword] = true;
        }

        if (provider.GetCustomAttributes(typeof(SettingPathAttribute), true).OfType<SettingPathAttribute>().FirstOrDefault() is { } path)
        {
            obj[PathKeyword] = path.Kind.ToString().ToLowerInvariant();
        }

        if (provider.GetCustomAttributes(typeof(SettingChoicesAttribute), true).OfType<SettingChoicesAttribute>().FirstOrDefault() is { } choices)
        {
            obj[ChoicesKeyword] = choices.ProviderId;
        }

        return schema;
    }
}
