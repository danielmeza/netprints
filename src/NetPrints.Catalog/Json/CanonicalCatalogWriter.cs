using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NetPrints.Catalog;

/// <summary>
/// Writes a <see cref="CatalogDocument"/> in the canonical form of contracts/catalog.md §1: LF, two-space
/// indentation, one property per line except parameters, type references, node hints and obsolete records,
/// which are written inline; empty optional properties are omitted. Shared source: no System.Text.Json.
/// </summary>
public static class CanonicalCatalogWriter
{
    private const string Indent = "  ";

    private const string SummaryKey = "summary";

    private const char NewLine = '\n';

    private const int FirstNonControlCharacter = 0x20;

    /// <summary>Writes <paramref name="document"/> exactly as it must be stored on disk (write it as UTF-8 without a byte order mark).</summary>
    /// <param name="document">The catalog to write, in the order it should appear.</param>
    /// <returns>The canonical text, ending in a newline.</returns>
    public static string Write(CatalogDocument document)
    {
        Guard.NotNull(document, nameof(document));

        var root = new MultiLineObject();
        root.Add("$schema", Quote(CatalogDocument.SchemaUrl));
        root.Add("schemaVersion", document.SchemaVersion.ToString(CultureInfo.InvariantCulture));
        root.Add("id", Quote(document.Id));
        root.Add("version", Quote(document.Version));
        root.AddIf("profile", document.Profile);
        root.Add("assemblies", MultiLineArray(document.Assemblies, WriteAssembly));
        root.AddArray("types", document.Types, WriteType);

        var builder = new StringBuilder();
        root.Write(builder, 0);
        builder.Append(NewLine);
        return builder.ToString();
    }

    private static Action<StringBuilder, int> WriteAssembly(CatalogAssembly assembly) => (builder, depth) =>
    {
        var value = new MultiLineObject();
        value.Add("name", Quote(assembly.Name));
        value.Add("version", Quote(assembly.Version));
        value.Write(builder, depth);
    };

    private static Action<StringBuilder, int> WriteType(CatalogType type) => (builder, depth) =>
    {
        var value = new MultiLineObject();
        value.Add("id", Quote(type.Id));
        value.AddIf("namespace", type.Namespace);
        value.Add("name", Quote(type.Name));
        value.Add("kind", Quote(EnumText(type.Kind)));
        value.AddStrings("modifiers", type.Modifiers);
        value.AddStrings("genericParameters", type.GenericParameters);
        value.AddIf("declaringType", type.DeclaringType);
        value.AddInline("baseType", type.BaseType, InlineTypeRef);
        value.AddArray("interfaces", type.Interfaces, reference => (b, _) => b.Append(InlineTypeRef(reference)));
        value.AddStringLines("enumMembers", type.EnumMembers);
        value.AddIf(SummaryKey, type.Summary);
        value.AddInline("node", type.Node, InlineNodeHint);
        value.AddArray("constructors", type.Constructors, WriteConstructor);
        value.AddArray("methods", type.Methods, WriteMethod);
        value.AddArray("variables", type.Variables, WriteVariable);
        value.Write(builder, depth);
    };

    private static Action<StringBuilder, int> WriteConstructor(CatalogConstructor constructor) => (builder, depth) =>
    {
        var value = new MultiLineObject();
        value.Add("id", Quote(constructor.Id));
        value.Add("visibility", Quote(EnumText(constructor.Visibility)));
        value.AddArray("parameters", constructor.Parameters, parameter => (b, _) => b.Append(InlineParameter(parameter)));
        value.AddIf(SummaryKey, constructor.Summary);
        value.Write(builder, depth);
    };

    private static Action<StringBuilder, int> WriteMethod(CatalogMethod method) => (builder, depth) =>
    {
        var value = new MultiLineObject();
        value.Add("id", Quote(method.Id));
        value.Add("name", Quote(method.Name));
        value.Add("visibility", Quote(EnumText(method.Visibility)));
        value.AddStrings("modifiers", method.Modifiers);
        value.AddStrings("genericParameters", method.GenericParameters);
        value.AddArray("parameters", method.Parameters, parameter => (b, _) => b.Append(InlineParameter(parameter)));
        value.AddInline("returnType", method.ReturnType, InlineTypeRef);
        value.AddIf("returnSummary", method.ReturnSummary);
        value.AddIf(SummaryKey, method.Summary);
        value.AddInline("obsolete", method.Obsolete, InlineObsolete);
        value.AddInline("node", method.Node, InlineNodeHint);
        value.Write(builder, depth);
    };

    private static Action<StringBuilder, int> WriteVariable(CatalogVariable variable) => (builder, depth) =>
    {
        var value = new MultiLineObject();
        value.Add("id", Quote(variable.Id));
        value.Add("name", Quote(variable.Name));
        value.Add("kind", Quote(EnumText(variable.Kind)));
        value.Add("type", InlineTypeRef(variable.Type));
        value.AddStrings("modifiers", variable.Modifiers);
        if (variable.Get is { } getter)
        {
            value.Add("get", Quote(EnumText(getter)));
        }

        if (variable.Set is { } setter)
        {
            value.Add("set", Quote(EnumText(setter)));
        }

        value.AddIf(SummaryKey, variable.Summary);
        value.Write(builder, depth);
    };

    private static string InlineParameter(CatalogParameter parameter)
    {
        var value = new InlineObject();
        value.Add("name", Quote(parameter.Name));
        value.Add("type", InlineTypeRef(parameter.Type));
        if (parameter.PassType is { } passType)
        {
            value.Add("passType", Quote(EnumText(passType)));
        }

        if (parameter.Params)
        {
            value.Add("params", "true");
        }

        if (parameter.Default is { } typedValue)
        {
            var typed = new InlineObject();
            typed.Add("type", Quote(typedValue.Type));
            typed.AddIf("value", typedValue.Value);
            value.Add("default", typed.ToString());
        }

        value.AddIf(SummaryKey, parameter.Summary);
        return value.ToString();
    }

    private static string InlineTypeRef(CatalogTypeRef reference)
    {
        var value = new InlineObject();
        value.Add("name", Quote(reference.Name));
        if (reference.Generic)
        {
            value.Add("generic", "true");
        }

        if (reference.IsEnum)
        {
            value.Add("isEnum", "true");
        }

        if (reference.IsInterface)
        {
            value.Add("isInterface", "true");
        }

        if (reference.Args is { Count: > 0 } args)
        {
            var rendered = new List<string>(args.Count);
            foreach (CatalogTypeRef arg in args)
            {
                rendered.Add(InlineTypeRef(arg));
            }

            value.Add("args", "[" + string.Join(", ", rendered) + "]");
        }

        return value.ToString();
    }

    private static string InlineNodeHint(CatalogNodeHint hint)
    {
        var value = new InlineObject();
        value.AddIf("displayName", hint.DisplayName);
        value.AddIf("category", hint.Category);
        if (hint.Keywords is { Count: > 0 } keywords)
        {
            value.Add("keywords", InlineStrings(keywords));
        }

        return value.ToString();
    }

    private static string InlineObsolete(CatalogObsoleteInfo obsolete)
    {
        var value = new InlineObject();
        value.AddIf("message", obsolete.Message);
        if (obsolete.Error)
        {
            value.Add("error", "true");
        }

        return value.ToString();
    }

    private static string InlineStrings(IReadOnlyList<string> values)
    {
        var rendered = new List<string>(values.Count);
        foreach (string value in values)
        {
            rendered.Add(Quote(value));
        }

        return "[" + string.Join(", ", rendered) + "]";
    }

    private static string EnumText<TEnum>(TEnum value)
        where TEnum : struct, Enum =>
        value.ToString().ToLowerInvariant();

    private static string Quote(string value)
    {
        var builder = new StringBuilder(value.Length + 2);
        builder.Append('"');
        for (int index = 0; index < value.Length; index++)
        {
            char c = value[index];
            switch (c)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (char.IsHighSurrogate(c) && index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]))
                    {
                        builder.Append(c).Append(value[++index]);
                    }
                    else if (c < FirstNonControlCharacter || char.IsSurrogate(c))
                    {
                        builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(c);
                    }

                    break;
            }
        }

        return builder.Append('"').ToString();
    }

    private static Action<StringBuilder, int> MultiLineArray<T>(IReadOnlyList<T> items, Func<T, Action<StringBuilder, int>> writeItem) => (builder, depth) =>
    {
        builder.Append('[').Append(NewLine);
        for (int i = 0; i < items.Count; i++)
        {
            AppendIndent(builder, depth + 1);
            writeItem(items[i])(builder, depth + 1);
            builder.Append(i < items.Count - 1 ? "," : string.Empty).Append(NewLine);
        }

        AppendIndent(builder, depth);
        builder.Append(']');
    };

    private static void AppendIndent(StringBuilder builder, int depth)
    {
        for (int i = 0; i < depth; i++)
        {
            builder.Append(Indent);
        }
    }

    private sealed class InlineObject
    {
        private readonly List<string> properties = [];

        public void Add(string key, string renderedValue) => properties.Add(Quote(key) + ": " + renderedValue);

        public void AddIf(string key, string? value)
        {
            if (value is { Length: > 0 } text)
            {
                Add(key, Quote(text));
            }
        }

        public override string ToString() => properties.Count == 0 ? "{}" : "{ " + string.Join(", ", properties) + " }";
    }

    private sealed class MultiLineObject
    {
        private readonly List<(string Key, Action<StringBuilder, int> Write)> properties = [];

        public void Add(string key, string renderedValue) => properties.Add((key, (builder, _) => builder.Append(renderedValue)));

        public void Add(string key, Action<StringBuilder, int> write) => properties.Add((key, write));

        public void AddIf(string key, string? value)
        {
            if (value is { Length: > 0 } text)
            {
                Add(key, Quote(text));
            }
        }

        public void AddStrings(string key, IReadOnlyList<string>? values)
        {
            if (values is { Count: > 0 })
            {
                Add(key, InlineStrings(values));
            }
        }

        public void AddStringLines(string key, IReadOnlyList<string>? values)
        {
            if (values is { Count: > 0 })
            {
                Add(key, MultiLineArray(values, item => (builder, _) => builder.Append(Quote(item))));
            }
        }

        public void AddInline<T>(string key, T? value, Func<T, string> render)
            where T : class
        {
            if (value is not null)
            {
                Add(key, render(value));
            }
        }

        public void AddArray<T>(string key, IReadOnlyList<T>? items, Func<T, Action<StringBuilder, int>> writeItem)
        {
            if (items is { Count: > 0 })
            {
                Add(key, MultiLineArray(items, writeItem));
            }
        }

        public void Write(StringBuilder builder, int depth)
        {
            builder.Append('{').Append(NewLine);
            for (int i = 0; i < properties.Count; i++)
            {
                AppendIndent(builder, depth + 1);
                builder.Append(Quote(properties[i].Key)).Append(": ");
                properties[i].Write(builder, depth + 1);
                builder.Append(i < properties.Count - 1 ? "," : string.Empty).Append(NewLine);
            }

            AppendIndent(builder, depth);
            builder.Append('}');
        }
    }
}
