#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NetPrints.Serialization.Json;

/// <summary>
/// Writes a <see cref="JsonNode"/> tree in NetPrints' canonical form (document-format.md §1.1, §2.3.1):
/// 2-space indentation, one property or array element per line for "block" values, but a handful of
/// small, frequently-changed shapes (connections, pin states, positions, typed values, type/method/
/// constructor/variable references, and a node with only common fields) written on a single "inline"
/// line each, so a graph edit touches as few lines as possible. Never reorders properties: the order
/// written is whatever order the input <see cref="JsonObject"/> already has (the DTOs' declared order).
/// </summary>
public static class CanonicalJsonWriter
{
    private static readonly JsonSerializerOptions ScalarOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Array-holding properties whose elements are always written inline (rule 1).</summary>
    private static readonly string[] InlineArrayElementProperties =
    [
        "connections", "pins", "locals", "parameters", "args", "returnTypes", "genericArgs", "dataTypes",
    ];

    /// <summary>Properties whose object/array value is always written inline (rule 2).</summary>
    private static readonly string[] InlineValueProperties =
    [
        "declaringType", "type", "literalType", "value", "default",
    ];

    /// <summary>Common fields a "nodes" array element may have and still qualify for rule 4.</summary>
    private static readonly string[] CommonNodeProperties = ["$kind", "id", "name"];

    /// <summary>
    /// Writes <paramref name="root"/> to <paramref name="output"/> in canonical form, followed by
    /// exactly one <c>\n</c>. Does not close <paramref name="output"/>.
    /// </summary>
    /// <param name="root">Root JSON object to write.</param>
    /// <param name="output">Stream to write UTF-8 bytes to.</param>
    /// <exception cref="ArgumentException"><paramref name="root"/> contains a non-finite
    /// (<see cref="double.NaN"/> or infinite) number.</exception>
    public static void Write(JsonObject root, Stream output)
    {
        using var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), bufferSize: 4096, leaveOpen: true)
        {
            NewLine = "\n",
        };

        WriteValue(writer, root, indent: 0, inline: false, propertyName: null, arrayPropertyName: null, layoutContext: LayoutContext.None);
        writer.Write('\n');
        writer.Flush();
    }

    private enum LayoutContext
    {
        /// <summary>Not inside the root <c>layout</c> object.</summary>
        None,

        /// <summary>This node is the root <c>layout</c> object's own value (map of graph key → per-graph
        /// map). Not inline; its children (per-graph maps) become <see cref="GraphMap"/>.</summary>
        LayoutRoot,

        /// <summary>This node is a per-graph map (node id → position). Not inline (block, one node id
        /// per line, per the document-format.md §1.7 example); its children (positions) become
        /// <see cref="PositionValue"/>.</summary>
        GraphMap,

        /// <summary>This node is an <c>[x, y]</c> position array: inline (rule 3).</summary>
        PositionValue,
    }

    private static void WriteValue(TextWriter w, JsonNode? node, int indent, bool inline,
        string? propertyName, string? arrayPropertyName, LayoutContext layoutContext)
    {
        switch (node)
        {
            case null:
                w.Write("null");
                return;
            case JsonValue value:
                w.Write(value.ToJsonString(ScalarOptions));
                return;
            case JsonObject obj:
                WriteObject(w, obj, indent, inline || IsInline(node, propertyName, arrayPropertyName, layoutContext), layoutContext);
                return;
            case JsonArray array:
                WriteArray(w, array, indent, inline || IsInline(node, propertyName, arrayPropertyName, layoutContext), propertyName, layoutContext);
                return;
            default:
                throw new ArgumentException($"Unknown JsonNode kind '{node.GetType()}'.", nameof(node));
        }
    }

    private static bool IsInline(JsonNode node, string? propertyName, string? arrayPropertyName, LayoutContext layoutContext)
    {
        if (arrayPropertyName is not null && InlineArrayElementProperties.Contains(arrayPropertyName, StringComparer.Ordinal))
        {
            return true;
        }

        if (propertyName is not null && InlineValueProperties.Contains(propertyName, StringComparer.Ordinal))
        {
            return true;
        }

        if (layoutContext == LayoutContext.PositionValue)
        {
            return true;
        }

        if (arrayPropertyName == "nodes" && node is JsonObject nodeObject
            && nodeObject.Select(kv => kv.Key).All(key => CommonNodeProperties.Contains(key, StringComparer.Ordinal)))
        {
            return true;
        }

        return false;
    }

    private static void WriteObject(TextWriter w, JsonObject obj, int indent, bool inline, LayoutContext layoutContext)
    {
        if (obj.Count == 0)
        {
            w.Write("{}");
            return;
        }

        if (inline)
        {
            w.Write("{ ");
            bool first = true;
            foreach ((string key, JsonNode? value) in obj)
            {
                if (!first)
                {
                    w.Write(", ");
                }

                first = false;
                WritePropertyName(w, key);
                WriteValue(w, value, indent, inline: true, key, arrayPropertyName: null, ChildLayoutContext(layoutContext, key));
            }

            w.Write(" }");
            return;
        }

        w.Write('{');
        w.WriteLine();
        bool firstBlock = true;
        foreach ((string key, JsonNode? value) in obj)
        {
            if (!firstBlock)
            {
                w.Write(',');
                w.WriteLine();
            }

            firstBlock = false;
            WriteIndent(w, indent + 2);
            WritePropertyName(w, key);
            WriteValue(w, value, indent + 2, inline: false, key, arrayPropertyName: null, ChildLayoutContext(layoutContext, key));
        }

        w.WriteLine();
        WriteIndent(w, indent);
        w.Write('}');
    }

    private static LayoutContext ChildLayoutContext(LayoutContext current, string childPropertyName)
    {
        return current switch
        {
            LayoutContext.None when childPropertyName == "layout" => LayoutContext.LayoutRoot,
            LayoutContext.LayoutRoot => LayoutContext.GraphMap,
            LayoutContext.GraphMap => LayoutContext.PositionValue,
            _ => LayoutContext.None,
        };
    }

    private static void WriteArray(TextWriter w, JsonArray array, int indent, bool inline, string? propertyName, LayoutContext layoutContext)
    {
        if (array.Count == 0)
        {
            w.Write("[]");
            return;
        }

        if (inline)
        {
            w.Write('[');
            bool first = true;
            foreach (JsonNode? element in array)
            {
                if (!first)
                {
                    w.Write(", ");
                }

                first = false;
                WriteValue(w, element, indent, inline: true, propertyName: null, arrayPropertyName: propertyName, layoutContext);
            }

            w.Write(']');
            return;
        }

        w.Write('[');
        w.WriteLine();
        bool firstBlock = true;
        foreach (JsonNode? element in array)
        {
            if (!firstBlock)
            {
                w.Write(',');
                w.WriteLine();
            }

            firstBlock = false;
            WriteIndent(w, indent + 2);
            WriteValue(w, element, indent + 2, inline: false, propertyName: null, arrayPropertyName: propertyName, layoutContext);
        }

        w.WriteLine();
        WriteIndent(w, indent);
        w.Write(']');
    }

    private static void WritePropertyName(TextWriter w, string name)
    {
        w.Write(JsonValue.Create(name)!.ToJsonString(ScalarOptions));
        w.Write(": ");
    }

    private static void WriteIndent(TextWriter w, int indent)
    {
        for (int i = 0; i < indent; i++)
        {
            w.Write(' ');
        }
    }
}
