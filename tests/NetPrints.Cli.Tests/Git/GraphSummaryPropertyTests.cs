using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using NetPrints.Cli.Git;
using NetPrints.Cli.Infrastructure;
using NetPrints.Core;
using NetPrints.Serialization;
using NetPrints.Serialization.Documents;
using NetPrints.Testing;
using Xunit;

namespace NetPrints.Cli.Tests.Git;

/// <summary>GI-T02: every property of every built-in node kind that changes the generated code or the meaning of a graph changes the <c>show</c> summary (F-R4, F-R16).</summary>
public sealed class GraphSummaryPropertyTests
{
    private static readonly string Root = LocalSdkLayout.FindRepositoryRoot();

    private static readonly string[] NotSummarized = [nameof(NodeDocument.Id), nameof(NodeDocument.Pins), "EqualityContract"];

    private static readonly TypeRef Int = new("System.Int32");
    private static readonly TypeRef Text = new("System.String");

    private static readonly Dictionary<string, Func<NodeDocument, NodeDocument>[]> Mutations = new()
    {
        ["MethodEntryNodeDocument.ArgumentCount"] = [On<MethodEntryNodeDocument>(n => n with { ArgumentCount = n.ArgumentCount + 1 })],
        ["MethodEntryNodeDocument.GenericArguments"] = [On<MethodEntryNodeDocument>(n => n with { GenericArguments = [.. n.GenericArguments ?? [], "Extra"] })],
        ["ConstructorEntryNodeDocument.ArgumentCount"] = [On<ConstructorEntryNodeDocument>(n => n with { ArgumentCount = n.ArgumentCount + 1 })],
        ["ReturnNodeDocument.ReturnCount"] = [On<ReturnNodeDocument>(n => n with { ReturnCount = n.ReturnCount + 1 })],
        ["ClassReturnNodeDocument.InterfaceCount"] = [On<ClassReturnNodeDocument>(n => n with { InterfaceCount = n.InterfaceCount + 1 })],
        ["EventEntryNodeDocument.EventName"] = [On<EventEntryNodeDocument>(n => n with { EventName = n.EventName + "X" })],
        ["EventEntryNodeDocument.Visibility"] = [On<EventEntryNodeDocument>(n => n with { Visibility = Flip(n.Visibility) })],
        ["EventEntryNodeDocument.Modifiers"] = [On<EventEntryNodeDocument>(n => n with { Modifiers = n.Modifiers ^ MethodModifiers.Abstract })],
        ["EventEntryNodeDocument.Overrides"] = [On<EventEntryNodeDocument>(n => n with { Overrides = n.Overrides is null ? null : n.Overrides with { ReturnTypes = [Int] } })],
        ["EventEntryNodeDocument.Arguments"] = [On<EventEntryNodeDocument>(n => n with { Arguments = [new EventArgumentDocument("amount", Int)] })],
        ["EventEntryNodeDocument.ArgumentCount"] = [On<EventEntryNodeDocument>(n => n with { ArgumentCount = n.ArgumentCount + 1 })],
        ["CallMethodNodeDocument.Method"] = MethodMutations<CallMethodNodeDocument>(n => n.Method, (n, method) => n with { Method = method }),
        ["CallMethodNodeDocument.GenericArgumentCount"] = [On<CallMethodNodeDocument>(n => n with { GenericArgumentCount = n.GenericArgumentCount + 1 })],
        ["CallMethodNodeDocument.Pure"] = [On<CallMethodNodeDocument>(n => n with { Pure = !n.Pure })],
        ["ConstructorNodeDocument.Constructor"] =
        [
            On<ConstructorNodeDocument>(n => n with { Constructor = n.Constructor with { DeclaringType = Text } }),
            On<ConstructorNodeDocument>(n => n with { Constructor = n.Constructor with { Parameters = [new ParameterRef("p", Int, MethodParameterPassType.Default, new TypedValue("System.Int32", "1"))] } }),
        ],
        ["ConstructorNodeDocument.Pure"] = [On<ConstructorNodeDocument>(n => n with { Pure = !n.Pure })],
        ["MakeDelegateNodeDocument.Method"] = MethodMutations<MakeDelegateNodeDocument>(n => n.Method, (n, method) => n with { Method = method }),
        ["VariableGetterNodeDocument.Variable"] = VariableMutations<VariableGetterNodeDocument>(n => n.Variable, (n, variable) => n with { Variable = variable }),
        ["VariableSetterNodeDocument.Variable"] = VariableMutations<VariableSetterNodeDocument>(n => n.Variable, (n, variable) => n with { Variable = variable }),
        ["LiteralNodeDocument.LiteralType"] = [On<LiteralNodeDocument>(n => n with { LiteralType = n.LiteralType == Text ? Int : Text })],
        ["TypeNodeDocument.Type"] = [On<TypeNodeDocument>(n => n with { Type = n.Type == Text ? Int : Text })],
        ["MakeArrayNodeDocument.UsePredefinedSize"] = [On<MakeArrayNodeDocument>(n => n with { UsePredefinedSize = !n.UsePredefinedSize })],
        ["MakeArrayNodeDocument.ElementCount"] = [On<MakeArrayNodeDocument>(n => n with { ElementCount = n.ElementCount + 1 })],
        ["ExplicitCastNodeDocument.Pure"] = [On<ExplicitCastNodeDocument>(n => n with { Pure = !n.Pure })],
        ["TernaryNodeDocument.Pure"] = [On<TernaryNodeDocument>(n => n with { Pure = !n.Pure })],
        ["AwaitNodeDocument.Pure"] = [On<AwaitNodeDocument>(n => n with { Pure = !n.Pure })],
        ["RerouteNodeDocument.PinKind"] = [On<RerouteNodeDocument>(n => n with { PinKind = n.PinKind + "x" })],
        ["RerouteNodeDocument.Count"] = [On<RerouteNodeDocument>(n => n with { Count = n.Count + 1 })],
        ["RerouteNodeDocument.DataTypes"] = [On<RerouteNodeDocument>(n => n with { DataTypes = [.. n.DataTypes ?? [], [Text]] })],
        ["NodeDocument.Name"] = [n => WithName(n, "Renamed")],
    };

    private static MemberVisibility Flip(MemberVisibility visibility) => visibility == MemberVisibility.Private ? MemberVisibility.Public : MemberVisibility.Private;

    private static Func<NodeDocument, NodeDocument> On<T>(Func<T, T> change) where T : NodeDocument => node => change((T)node);

    private static NodeDocument WithName(NodeDocument node, string name) => node switch
    {
        MethodEntryNodeDocument n => n with { Name = name },
        ConstructorEntryNodeDocument n => n with { Name = name },
        ReturnNodeDocument n => n with { Name = name },
        ClassReturnNodeDocument n => n with { Name = name },
        TypeReturnNodeDocument n => n with { Name = name },
        EventEntryNodeDocument n => n with { Name = name },
        CallMethodNodeDocument n => n with { Name = name },
        ConstructorNodeDocument n => n with { Name = name },
        MakeDelegateNodeDocument n => n with { Name = name },
        VariableGetterNodeDocument n => n with { Name = name },
        VariableSetterNodeDocument n => n with { Name = name },
        LiteralNodeDocument n => n with { Name = name },
        TypeNodeDocument n => n with { Name = name },
        MakeArrayTypeNodeDocument n => n with { Name = name },
        MakeArrayNodeDocument n => n with { Name = name },
        ExplicitCastNodeDocument n => n with { Name = name },
        TypeOfNodeDocument n => n with { Name = name },
        IfElseNodeDocument n => n with { Name = name },
        ForLoopNodeDocument n => n with { Name = name },
        TernaryNodeDocument n => n with { Name = name },
        AwaitNodeDocument n => n with { Name = name },
        ThrowNodeDocument n => n with { Name = name },
        DefaultNodeDocument n => n with { Name = name },
        RerouteNodeDocument n => n with { Name = name },
        _ => throw new NotSupportedException(node.GetType().Name),
    };

    private static Func<NodeDocument, NodeDocument>[] MethodMutations<T>(Func<T, MethodRef> get, Func<T, MethodRef, T> set) where T : NodeDocument =>
    [
        On<T>(n => set(n, get(n) with { Name = get(n).Name + "X" })),
        On<T>(n => set(n, get(n) with { DeclaringType = Text })),
        On<T>(n => set(n, get(n) with { ReturnTypes = [.. get(n).ReturnTypes ?? [], Text] })),
        On<T>(n => set(n, get(n) with { Modifiers = get(n).Modifiers ^ MethodModifiers.Static })),
        On<T>(n => set(n, get(n) with { Visibility = Flip(get(n).Visibility) })),
        On<T>(n => set(n, get(n) with { GenericArgs = [.. get(n).GenericArgs ?? [], Text] })),
        On<T>(n => set(n, get(n) with { Parameters = [.. get(n).Parameters ?? [], new ParameterRef("extra", Int, MethodParameterPassType.Default, new TypedValue("System.Int32", "7"))] })),
        On<T>(n => set(n, get(n) with { Parameters = [new ParameterRef("p", Int, MethodParameterPassType.Default, null)] })),
        On<T>(n => set(n, get(n) with { Parameters = [new ParameterRef("p", Int, MethodParameterPassType.Default, new TypedValue("System.Int32", "8"))] })),
    ];

    private static Func<NodeDocument, NodeDocument>[] VariableMutations<T>(Func<T, VariableRef> get, Func<T, VariableRef, T> set) where T : NodeDocument =>
    [
        On<T>(n => set(n, get(n) with { Name = get(n).Name + "X" })),
        On<T>(n => set(n, get(n) with { Type = Text })),
        On<T>(n => set(n, get(n) with { DeclaringType = Text })),
        On<T>(n => set(n, get(n) with { GetterVisibility = Flip(get(n).GetterVisibility) })),
        On<T>(n => set(n, get(n) with { SetterVisibility = Flip(get(n).SetterVisibility) })),
        On<T>(n => set(n, get(n) with { Visibility = Flip(get(n).Visibility) })),
        On<T>(n => set(n, get(n) with { Modifiers = get(n).Modifiers ^ VariableModifiers.ReadOnly })),
        On<T>(n => set(n, get(n) with { Scope = get(n).Scope == VariableScope.Local ? VariableScope.Member : VariableScope.Local })),
    ];

    private static IEnumerable<Type> BuiltInNodeTypes() =>
        typeof(NodeDocument).GetCustomAttributes<JsonDerivedTypeAttribute>().Select(attribute => attribute.DerivedType);

    private static async Task<ClassDocument> ReadAsync(string path)
    {
        DocumentFormatRegistry formats = GraphFormats.CreateRegistry();
        await using FileStream input = File.OpenRead(path);
        return await formats.Default.ReadClassAsync(input, new DocumentId(Path.GetFileName(path)), TestContext.Current.CancellationToken);
    }

    private static IEnumerable<NodeDocument> AllNodes(ClassDocument document)
    {
        var graphs = new List<GraphDocument> { document.ClassGraph };
        foreach (VariableDocument variable in document.Variables ?? [])
        {
            graphs.Add(variable.TypeGraph);
            graphs.AddRange(new[] { variable.Getter?.Graph, variable.Setter?.Graph }.OfType<GraphDocument>());
        }

        graphs.AddRange((document.Methods ?? []).Select(method => method.Graph));
        graphs.AddRange((document.Constructors ?? []).Select(constructor => constructor.Graph));
        graphs.AddRange((document.EventGraphs ?? []).Select(eventGraph => eventGraph.Graph));
        return graphs.SelectMany(graph => graph.Nodes);
    }

    private static async Task<(ClassDocument Host, List<NodeDocument> Nodes)> LoadAsync()
    {
        ClassDocument everything = await ReadAsync(Path.Combine(Root, "tests", "NetPrints.Core.Tests", "Fixtures", "AllNodes", "AllNodes.Everything.netpc.json"));
        ClassDocument grammar = await ReadAsync(Path.Combine(Root, "tests", "NetPrints.Cli.Tests", "Git", "Fixtures", "ShowGrammar", "graph.txt"));
        return (everything, [.. AllNodes(everything).Concat(AllNodes(grammar)).Where(node => node is not UnknownNodeDocument)]);
    }

    private static string Summary(ClassDocument host, NodeDocument node) =>
        GraphSummaryWriter.Write(host with { ClassGraph = new GraphDocument([node], null, null) });

    [Fact]
    public async Task EveryBuiltInNodeKindHasAnArmThatRendersItsKind()
    {
        (ClassDocument host, List<NodeDocument> nodes) = await LoadAsync();

        foreach (JsonDerivedTypeAttribute attribute in typeof(NodeDocument).GetCustomAttributes<JsonDerivedTypeAttribute>())
        {
            NodeDocument node = nodes.FirstOrDefault(candidate => candidate.GetType() == attribute.DerivedType)
                ?? throw new Xunit.Sdk.XunitException($"No fixture node of type {attribute.DerivedType.Name}.");

            string line = Summary(host, node).Split('\n').First(text => text.StartsWith("  node ", StringComparison.Ordinal));

            Assert.StartsWith($"  node {node.Id} {attribute.TypeDiscriminator}", line, StringComparison.Ordinal);
            Assert.DoesNotContain("extension not loaded", line, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void EveryPropertyOfEveryBuiltInNodeKindHasAMutationCase()
    {
        var expected = new HashSet<string> { "NodeDocument.Name" };
        foreach (Type type in BuiltInNodeTypes())
        {
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(property => !NotSummarized.Contains(property.Name)))
            {
                expected.Add($"{type.Name}.{property.Name}");
            }
        }

        Assert.Equal([.. expected.Order(StringComparer.Ordinal)], [.. Mutations.Keys.Order(StringComparer.Ordinal)]);
    }

    [Fact]
    public async Task ChangingANonPinPropertyChangesTheSummary()
    {
        (ClassDocument host, List<NodeDocument> nodes) = await LoadAsync();
        var unchanged = new List<string>();

        foreach ((string key, Func<NodeDocument, NodeDocument>[] mutations) in Mutations)
        {
            string typeName = key[..key.IndexOf('.', StringComparison.Ordinal)];
            IEnumerable<NodeDocument> candidates = typeName == nameof(NodeDocument)
                ? nodes
                : nodes.Where(node => node.GetType().Name == typeName);
            Assert.NotEmpty(candidates);

            for (int index = 0; index < mutations.Length; index++)
            {
                foreach (NodeDocument node in candidates)
                {
                    if (Summary(host, node) == Summary(host, mutations[index](node)))
                    {
                        unchanged.Add($"{key}#{index} on {node.Id}");
                    }
                }
            }
        }

        Assert.Empty(unchanged);
    }
}
