using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
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

    private static string NodeLine(NodeDocument node) => node switch
    {
        UnknownNodeDocument unknown => Join("node", unknown.Id, unknown.Kind) + UnknownNodeSuffix,
        MethodEntryNodeDocument => Join("node", node.Id, BuiltInNodeKinds.MethodEntry),
        ConstructorEntryNodeDocument => Join("node", node.Id, BuiltInNodeKinds.ConstructorEntry),
        ReturnNodeDocument => Join("node", node.Id, BuiltInNodeKinds.Return),
        ClassReturnNodeDocument => Join("node", node.Id, BuiltInNodeKinds.ClassReturn),
        TypeReturnNodeDocument => Join("node", node.Id, BuiltInNodeKinds.TypeReturn),
        EventEntryNodeDocument entry => Join("node", node.Id, BuiltInNodeKinds.EventEntry, entry.EventName),
        CallMethodNodeDocument call => Join("node", node.Id, BuiltInNodeKinds.CallMethod, Method(call.Method)),
        ConstructorNodeDocument constructor => Join("node", node.Id, BuiltInNodeKinds.Constructor, Type(constructor.Constructor.DeclaringType) + Parameters(constructor.Constructor.Parameters)),
        MakeDelegateNodeDocument delegateNode => Join("node", node.Id, BuiltInNodeKinds.MakeDelegate, Method(delegateNode.Method)),
        VariableGetterNodeDocument getter => Join("node", node.Id, BuiltInNodeKinds.VariableGetter, Variable(getter.Variable)),
        VariableSetterNodeDocument setter => Join("node", node.Id, BuiltInNodeKinds.VariableSetter, Variable(setter.Variable)),
        LiteralNodeDocument literal => Join("node", node.Id, BuiltInNodeKinds.Literal, Type(literal.LiteralType)),
        TypeNodeDocument type => Join("node", node.Id, BuiltInNodeKinds.Type, Type(type.Type)),
        MakeArrayTypeNodeDocument => Join("node", node.Id, BuiltInNodeKinds.MakeArrayType),
        MakeArrayNodeDocument => Join("node", node.Id, BuiltInNodeKinds.MakeArray),
        ExplicitCastNodeDocument => Join("node", node.Id, BuiltInNodeKinds.ExplicitCast),
        TypeOfNodeDocument => Join("node", node.Id, BuiltInNodeKinds.TypeOf),
        IfElseNodeDocument => Join("node", node.Id, BuiltInNodeKinds.IfElse),
        ForLoopNodeDocument => Join("node", node.Id, BuiltInNodeKinds.ForLoop),
        TernaryNodeDocument => Join("node", node.Id, BuiltInNodeKinds.Ternary),
        AwaitNodeDocument => Join("node", node.Id, BuiltInNodeKinds.Await),
        ThrowNodeDocument => Join("node", node.Id, BuiltInNodeKinds.Throw),
        DefaultNodeDocument => Join("node", node.Id, BuiltInNodeKinds.Default),
        RerouteNodeDocument reroute => Join("node", node.Id, BuiltInNodeKinds.Reroute, reroute.PinKind),
        _ => Join("node", node.Id, node.GetType().Name) + UnknownNodeSuffix,
    };

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

    private static string Method(MethodRef method) =>
        Type(method.DeclaringType) + "." + method.Name
        + (method.GenericArgs is { Count: > 0 } args ? "<" + string.Join(",", args.Select(Type)) + ">" : string.Empty)
        + Parameters(method.Parameters);

    private static string Parameters(IReadOnlyList<ParameterRef>? parameters) =>
        "(" + string.Join(",", (parameters ?? []).Select(parameter =>
            (parameter.PassType == NetPrints.Core.MethodParameterPassType.Default ? string.Empty : parameter.PassType.ToString().ToLowerInvariant() + " ") + Type(parameter.Type))) + ")";

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
