using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetPrints.Core;
using NetPrints.Serialization.Documents;

namespace NetPrints.Cli.Git;

/// <summary>
/// Renders a <see cref="ClassDocument"/> as the line-oriented summary of contracts/git.md §1: one item per line, LF, two spaces per nesting
/// level, nodes sorted by id, pins by name and connections by endpoints, so the text does not depend on the order of the file.
/// </summary>
internal static class GraphSummaryWriter
{
    private const int Member = 1;
    private const int Body = 2;
    private const int NestedBody = 3;
    private const int IndentWidth = 2;
    private const string UnknownNodeSuffix = " (extension not loaded)";

    /// <summary>Renders <paramref name="document"/>.</summary>
    /// <param name="document">The graph document.</param>
    /// <returns>The summary, every line ended by <c>\n</c>.</returns>
    public static string Write(ClassDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var text = new StringBuilder();
        string name = string.IsNullOrEmpty(document.Namespace) ? document.Name : document.Namespace + "." + document.Name;
        Line(text, 0, Join("class", name, document.Visibility.ToString(), Flags(document.Modifiers)));
        foreach (string argument in document.GenericArguments ?? [])
        {
            Line(text, Member, "generic " + argument);
        }

        WriteGraph(text, Member, document.ClassGraph);
        foreach (VariableDocument variable in document.Variables ?? [])
        {
            WriteVariable(text, variable);
        }

        foreach (MethodDocument method in document.Methods ?? [])
        {
            Line(text, Member, Join("method", method.Id, method.Name, method.Visibility.ToString(), Flags(method.Modifiers)));
            WriteGraph(text, Body, method.Graph);
        }

        foreach (ConstructorDocument constructor in document.Constructors ?? [])
        {
            Line(text, Member, Join("constructor", constructor.Id, constructor.Visibility.ToString()));
            WriteGraph(text, Body, constructor.Graph);
        }

        foreach (EventGraphDocument eventGraph in document.EventGraphs ?? [])
        {
            Line(text, Member, Join("event-graph", eventGraph.Id, eventGraph.Name));
            WriteGraph(text, Body, eventGraph.Graph);
        }

        int entries = document.Layout?.Values.Sum(graph => graph.Count) ?? 0;
        Line(text, 0, string.Create(CultureInfo.InvariantCulture, $"layout {entries} entries"));
        return text.ToString();
    }

    private static void WriteVariable(StringBuilder text, VariableDocument variable)
    {
        Line(text, Member, Join("variable", variable.Id, variable.Name + " : " + VariableType(variable.TypeGraph), variable.Visibility.ToString(), Flags(variable.Modifiers)));
        Line(text, Body, "type-graph");
        WriteGraph(text, NestedBody, variable.TypeGraph);
        foreach ((string label, AccessorDocument? accessor) in new[] { ("getter", variable.Getter), ("setter", variable.Setter) })
        {
            if (accessor is null)
            {
                continue;
            }

            Line(text, Body, Join(label, accessor.Visibility.ToString()));
            WriteGraph(text, NestedBody, accessor.Graph);
        }
    }

    // The variable's type is whatever the type graph wires into its TypeReturn node; a type node renders as its TypeRef.
    private static string VariableType(GraphDocument typeGraph)
    {
        TypeReturnNodeDocument? root = typeGraph.Nodes.OfType<TypeReturnNodeDocument>().FirstOrDefault();
        if (root is null)
        {
            return "?";
        }

        string target = root.Id + "/in.type.Type";
        string? source = typeGraph.Connections?.FirstOrDefault(connection => connection.To == target)?.From;
        string? sourceId = source?[..source.IndexOf('/', StringComparison.Ordinal)];
        return typeGraph.Nodes.OfType<TypeNodeDocument>().FirstOrDefault(node => node.Id == sourceId) is { } typeNode ? Type(typeNode.Type) : "?";
    }

    private static void WriteGraph(StringBuilder text, int depth, GraphDocument graph)
    {
        foreach (LocalVariableDocument local in (graph.Locals ?? []).OrderBy(local => local.Name, StringComparer.Ordinal))
        {
            Line(text, depth, $"local {local.Name} : {Type(local.Type)}");
        }

        foreach (NodeDocument node in graph.Nodes.OrderBy(node => node.Id, StringComparer.Ordinal))
        {
            Line(text, depth, NodeLine(node));
            if (node is UnknownNodeDocument unknown && Raw(unknown) is { } raw)
            {
                Line(text, depth + 1, "raw " + raw);
            }

            foreach (PinStateDocument pin in (node.Pins ?? []).OrderBy(pin => pin.Pin, StringComparer.Ordinal))
            {
                Line(text, depth + 1, PinLine(pin));
            }
        }

        foreach (ConnectionDocument connection in (graph.Connections ?? [])
            .OrderBy(connection => connection.From, StringComparer.Ordinal)
            .ThenBy(connection => connection.To, StringComparer.Ordinal))
        {
            Line(text, depth, $"connect {connection.From} -> {connection.To}");
        }
    }

    private static string NodeLine(NodeDocument node)
    {
        if (node is UnknownNodeDocument unknown)
        {
            return Join("node", unknown.Id, unknown.Kind) + UnknownNodeSuffix;
        }

        var properties = new List<string>();
        Add(properties, "name", string.IsNullOrEmpty(node.Name) ? null : Quote(node.Name));
        (string kind, string target) = Describe(node, properties);
        return Join("node", node.Id, kind, target, string.Join(' ', properties));
    }

    // The kind, the target and (appended to properties, in the order of contracts/git.md §1) the non-default scalar properties of a built-in node.
    private static (string Kind, string Target) Describe(NodeDocument node, List<string> properties)
    {
        switch (node)
        {
            case MethodEntryNodeDocument entry:
                Count(properties, "args", entry.ArgumentCount);
                Add(properties, "generics", entry.GenericArguments is { Count: > 0 } generics ? string.Join(',', generics) : null);
                return (BuiltInNodeKinds.MethodEntry, string.Empty);
            case ConstructorEntryNodeDocument entry:
                Count(properties, "args", entry.ArgumentCount);
                return (BuiltInNodeKinds.ConstructorEntry, string.Empty);
            case ReturnNodeDocument ret:
                Count(properties, "returns", ret.ReturnCount);
                return (BuiltInNodeKinds.Return, string.Empty);
            case ClassReturnNodeDocument classReturn:
                Count(properties, "interfaces", classReturn.InterfaceCount);
                return (BuiltInNodeKinds.ClassReturn, string.Empty);
            case EventEntryNodeDocument entry:
                Count(properties, "args", entry.ArgumentCount);
                Add(properties, "argTypes", entry.Arguments is { Count: > 0 } arguments ? string.Join(',', arguments.Select(argument => Type(argument.Type))) : null);
                Add(properties, "visibility", NonDefault(entry.Visibility));
                Add(properties, "modifiers", NonDefault(entry.Modifiers));
                Add(properties, "overrides", entry.Overrides is { } overrides ? Signature(overrides) + Returns(overrides) : null);
                return (BuiltInNodeKinds.EventEntry, entry.EventName);
            case CallMethodNodeDocument call:
                Flag(properties, "pure", call.Pure);
                Count(properties, "genericArgs", call.GenericArgumentCount);
                MethodProperties(properties, call.Method);
                return (BuiltInNodeKinds.CallMethod, Signature(call.Method) + Returns(call.Method));
            case ConstructorNodeDocument constructor:
                Flag(properties, "pure", constructor.Pure);
                return (BuiltInNodeKinds.Constructor, Type(constructor.Constructor.DeclaringType) + Parameters(constructor.Constructor.Parameters));
            case MakeDelegateNodeDocument delegateNode:
                MethodProperties(properties, delegateNode.Method);
                return (BuiltInNodeKinds.MakeDelegate, Signature(delegateNode.Method) + Returns(delegateNode.Method));
            case VariableGetterNodeDocument getter:
                VariableProperties(properties, getter.Variable);
                return (BuiltInNodeKinds.VariableGetter, Variable(getter.Variable));
            case VariableSetterNodeDocument setter:
                VariableProperties(properties, setter.Variable);
                return (BuiltInNodeKinds.VariableSetter, Variable(setter.Variable));
            case LiteralNodeDocument literal:
                return (BuiltInNodeKinds.Literal, Type(literal.LiteralType));
            case TypeNodeDocument type:
                return (BuiltInNodeKinds.Type, Type(type.Type));
            case MakeArrayNodeDocument array:
                Flag(properties, "predefinedSize", array.UsePredefinedSize);
                Count(properties, "elements", array.ElementCount);
                return (BuiltInNodeKinds.MakeArray, string.Empty);
            case ExplicitCastNodeDocument cast:
                Flag(properties, "pure", cast.Pure);
                return (BuiltInNodeKinds.ExplicitCast, string.Empty);
            case TernaryNodeDocument ternary:
                Flag(properties, "pure", ternary.Pure);
                return (BuiltInNodeKinds.Ternary, string.Empty);
            case AwaitNodeDocument await:
                Flag(properties, "pure", await.Pure);
                return (BuiltInNodeKinds.Await, string.Empty);
            case RerouteNodeDocument reroute:
                Count(properties, "count", reroute.Count);
                Add(properties, "types", reroute.DataTypes is { Count: > 0 } groups ? string.Join(';', groups.Select(group => string.Join(',', group.Select(Type)))) : null);
                return (BuiltInNodeKinds.Reroute, reroute.PinKind);
            default:
                return (KindOf(node), string.Empty);
        }
    }

    // A built-in node without scalar properties, named by the discriminator its type is registered under.
    private static string KindOf(NodeDocument node) =>
        typeof(NodeDocument).GetCustomAttributes<JsonDerivedTypeAttribute>().FirstOrDefault(attribute => attribute.DerivedType == node.GetType())?.TypeDiscriminator?.ToString()
        ?? node.GetType().Name;

    private static void MethodProperties(List<string> properties, MethodRef method)
    {
        Add(properties, "modifiers", NonDefault(method.Modifiers));
        Add(properties, "visibility", NonDefault(method.Visibility));
    }

    private static void VariableProperties(List<string> properties, VariableRef variable)
    {
        Add(properties, "visibility", NonDefault(variable.Visibility));
        Add(properties, "getter", NonDefault(variable.GetterVisibility));
        Add(properties, "setter", NonDefault(variable.SetterVisibility));
        Add(properties, "modifiers", NonDefault(variable.Modifiers));
        Add(properties, "scope", variable.Scope == VariableScope.Member ? null : variable.Scope.ToString());
    }

    private static void Add(List<string> properties, string key, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            properties.Add(key + "=" + value);
        }
    }

    private static void Count(List<string> properties, string key, int value) =>
        Add(properties, key, value == 0 ? null : value.ToString(CultureInfo.InvariantCulture));

    private static void Flag(List<string> properties, string key, bool value) => Add(properties, key, value ? "true" : null);

    // Public visibility and no modifiers are the defaults and are left out.
    private static string? NonDefault(MemberVisibility visibility) => visibility == MemberVisibility.Public ? null : visibility.ToString();

    private static string? NonDefault<T>(T modifiers) where T : struct, Enum => Flags(modifiers) is { Length: > 0 } flags ? flags : null;

    private static string Quote(string value) => "\"" + Escape(value) + "\"";

    // The properties of an unknown node other than its kind and id, compact, in file order.
    private static string? Raw(UnknownNodeDocument node)
    {
        if (node.Raw.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            using JsonElement.ObjectEnumerator properties = node.Raw.EnumerateObject();
            foreach (JsonProperty property in properties)
            {
                if (property.Name is not ("$kind" or "id"))
                {
                    property.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
        }

        string json = Encoding.UTF8.GetString(stream.ToArray());
        return json == "{}" ? null : json;
    }

    private static string PinLine(PinStateDocument pin)
    {
        string line = "pin " + pin.Pin;
        if (!string.IsNullOrEmpty(pin.Name))
        {
            line += " as " + pin.Name;
        }

        if (pin.Value is { } value)
        {
            line += " = " + value.Type + " " + (value.Value is null ? "null" : "\"" + Escape(value.Value) + "\"");
        }

        return line;
    }

    private static string Signature(MethodRef method) =>
        Type(method.DeclaringType) + "." + method.Name
        + (method.GenericArgs is { Count: > 0 } args ? "<" + string.Join(",", args.Select(Type)) + ">" : string.Empty)
        + Parameters(method.Parameters);

    private static string Returns(MethodRef method) =>
        method.ReturnTypes is { Count: > 0 } types ? "->" + string.Join(",", types.Select(Type)) : string.Empty;

    private static string Parameters(IReadOnlyList<ParameterRef>? parameters) =>
        "(" + string.Join(",", (parameters ?? []).Select(parameter =>
            (parameter.PassType == MethodParameterPassType.Default ? string.Empty : parameter.PassType.ToString().ToLowerInvariant() + " ")
            + Type(parameter.Type)
            + (parameter.Default is { } value ? "=" + value.Type + ":" + (value.Value is null ? "null" : Quote(value.Value)) : string.Empty))) + ")";

    private static string Variable(VariableRef variable) =>
        (variable.DeclaringType is { } declaring ? Type(declaring) + "." : string.Empty) + variable.Name + " : " + Type(variable.Type);

    private static string Type(TypeRef type) =>
        type.Args is { Count: > 0 } args ? type.Name + "<" + string.Join(",", args.Select(Type)) + ">" : type.Name;

    private static string Flags<T>(T modifiers) where T : struct, Enum
    {
        string text = modifiers.ToString();
        return text == "None" ? string.Empty : text.Replace(", ", ",", StringComparison.Ordinal);
    }

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);

    private static string Join(params string[] parts) => string.Join(' ', parts.Where(part => part.Length > 0));

    private static void Line(StringBuilder text, int depth, string line) => text.Append(' ', depth * IndentWidth).Append(line).Append('\n');
}
