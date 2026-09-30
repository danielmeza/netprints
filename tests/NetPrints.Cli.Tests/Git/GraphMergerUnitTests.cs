using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NetPrints.Cli.Git;
using NetPrints.Cli.Infrastructure;
using NetPrints.Core;
using NetPrints.Serialization.Documents;
using Xunit;

namespace NetPrints.Cli.Tests.Git;

/// <summary>Branch coverage of <see cref="GraphMerger"/> over in-memory documents (F-R1, F-R2, F-R6).</summary>
public sealed class GraphMergerUnitTests
{
    private const string Main = "m0000000000001";
    private static readonly TypeRef StringType = new("System.String");
    private static readonly MethodRef Beep = new("Beep", new TypeRef("System.Console"), null, null, MethodModifiers.Static, MemberVisibility.Public, null);
    private static readonly GraphDocument EmptyGraph = new([], null, null);

    private static Task<MergeOutcome> MergeAsync(ClassDocument b, ClassDocument o, ClassDocument t) =>
        new GraphMerger(GraphFormats.CreateRegistry().Default).MergeAsync(b, o, t, TestContext.Current.CancellationToken);

    private static NodeDocument Entry(string id) => new MethodEntryNodeDocument(id, null, null, 0, null);

    private static NodeDocument Return(string id) => new ReturnNodeDocument(id, null, null, 0);

    private static NodeDocument Call(string id, bool pure = false, string? name = null, params PinStateDocument[] pins) =>
        new CallMethodNodeDocument(id, name, pins.Length == 0 ? null : pins, Beep, 0, pure);

    private static PinStateDocument Pin(string pin, string value) => new(pin, null, new TypedValue("System.String", value));

    private static ConnectionDocument Wire(string from, string to) => new(from, to);

    private static GraphDocument Graph(IEnumerable<NodeDocument> nodes, IEnumerable<ConnectionDocument>? connections = null, IEnumerable<LocalVariableDocument>? locals = null)
    {
        ConnectionDocument[] wires = [.. connections ?? []];
        LocalVariableDocument[] localList = [.. locals ?? []];
        return new GraphDocument([.. nodes], wires.Length == 0 ? null : wires, localList.Length == 0 ? null : localList);
    }

    private static MethodDocument Method(string id, string name, GraphDocument graph) => new(id, name, MemberVisibility.Public, MethodModifiers.None, graph);

    private static ClassDocument Class(IEnumerable<MethodDocument>? methods = null, IEnumerable<VariableDocument>? variables = null, string name = "C")
    {
        MethodDocument[] methodList = [.. methods ?? []];
        VariableDocument[] variableList = [.. variables ?? []];
        return new ClassDocument(
            1, "T", name, MemberVisibility.Public, ClassModifiers.None, null, EmptyGraph,
            variableList.Length == 0 ? null : variableList, methodList.Length == 0 ? null : methodList, null, null, null);
    }

    private static ClassDocument WithMain(GraphDocument graph) => Class([Method(Main, "Main", graph)]);

    private static GraphDocument Chain(params NodeDocument[] extra) =>
        Graph(
            [Entry("n1"), Call("n2"), Return("n4"), .. extra],
            [Wire("n1/out.exec.Exec", "n2/in.exec.Exec"), Wire("n2/out.exec.Exec", "n4/in.exec.Exec")]);

    private static GraphDocument MainGraph(ClassDocument document) => Assert.Single(document.Methods ?? []).Graph;

    private static ClassDocument AssertClean(MergeOutcome outcome) => Assert.IsType<MergeOutcome.Clean>(outcome).Document;

    private static MergeConflict AssertConflict(MergeOutcome outcome, MergeConflictKind kind, string pathPart)
    {
        MergeOutcome.Conflicted conflicted = Assert.IsType<MergeOutcome.Conflicted>(outcome);
        return Assert.Single(conflicted.Conflicts, conflict => conflict.Kind == kind && conflict.Path.Contains(pathPart, StringComparison.Ordinal));
    }

    [Fact]
    public async Task TwoInsertionsAfterTheSameExecOutputConflict()
    {
        ClassDocument b = WithMain(Chain());
        ClassDocument o = WithMain(Graph(
            [Entry("n1"), Call("n2"), Call("n10"), Return("n4")],
            [Wire("n1/out.exec.Exec", "n2/in.exec.Exec"), Wire("n2/out.exec.Exec", "n10/in.exec.Exec"), Wire("n10/out.exec.Exec", "n4/in.exec.Exec")]));
        ClassDocument t = WithMain(Graph(
            [Entry("n1"), Call("n2"), Call("n20"), Return("n4")],
            [Wire("n1/out.exec.Exec", "n2/in.exec.Exec"), Wire("n2/out.exec.Exec", "n20/in.exec.Exec"), Wire("n20/out.exec.Exec", "n4/in.exec.Exec")]));

        AssertConflict(await MergeAsync(b, o, t), MergeConflictKind.ExecOutputTwice, "n2");
    }

    [Fact]
    public async Task TwoSourcesIntoOneTypeInputConflict()
    {
        ClassDocument b = WithMain(Chain());
        ClassDocument o = WithMain(Chain(new TypeNodeDocument("n10", null, null, StringType)) with
        {
            Connections = [.. MainGraph(WithMain(Chain())).Connections ?? [], Wire("n10/out.type.Type", "n2/in.type.Type")],
        });
        ClassDocument t = WithMain(Chain(new TypeNodeDocument("n20", null, null, StringType)) with
        {
            Connections = [.. MainGraph(WithMain(Chain())).Connections ?? [], Wire("n20/out.type.Type", "n2/in.type.Type")],
        });

        AssertConflict(await MergeAsync(b, o, t), MergeConflictKind.TypeInputTwice, "n2");
    }

    [Fact]
    public async Task InsertionsAtDifferentExecOutputsMergeCleanly()
    {
        ClassDocument b = WithMain(Chain());
        ClassDocument o = WithMain(Graph(
            [Entry("n1"), Call("n10"), Call("n2"), Return("n4")],
            [Wire("n1/out.exec.Exec", "n10/in.exec.Exec"), Wire("n10/out.exec.Exec", "n2/in.exec.Exec"), Wire("n2/out.exec.Exec", "n4/in.exec.Exec")]));
        ClassDocument t = WithMain(Graph(
            [Entry("n1"), Call("n2"), Call("n20"), Return("n4")],
            [Wire("n1/out.exec.Exec", "n2/in.exec.Exec"), Wire("n2/out.exec.Exec", "n20/in.exec.Exec"), Wire("n20/out.exec.Exec", "n4/in.exec.Exec")]));

        ClassDocument merged = AssertClean(await MergeAsync(b, o, t));

        Assert.Equal(["n1", "n2", "n4", "n10", "n20"], MainGraph(merged).Nodes.Select(node => node.Id));
    }

    [Fact]
    public async Task ANodeShapeChangeOnOneSideAgainstNewConnectionsOnTheOtherConflicts()
    {
        ClassDocument b = WithMain(Chain());
        ClassDocument o = WithMain(Graph([Entry("n1"), Call("n2", pure: true), Return("n4")], [Wire("n1/out.exec.Exec", "n4/in.exec.Exec")]));
        ClassDocument t = WithMain(Graph(
            [Entry("n1"), Call("n2"), Call("n20"), Return("n4")],
            [Wire("n1/out.exec.Exec", "n2/in.exec.Exec"), Wire("n2/out.exec.Exec", "n20/in.exec.Exec"), Wire("n20/out.exec.Exec", "n4/in.exec.Exec")]));

        AssertConflict(await MergeAsync(b, o, t), MergeConflictKind.NodeProperty, "nodes[n2]");
        AssertConflict(await MergeAsync(b, t, o), MergeConflictKind.NodeProperty, "nodes[n2]");
    }

    [Fact]
    public async Task ANodeShapeChangeOnOneSideAgainstAPinValueChangeOnTheOtherConflicts()
    {
        ClassDocument b = WithMain(Graph([Entry("n1"), Call("n2", pins: Pin("in.data.value", "a")), Return("n4")], [Wire("n1/out.exec.Exec", "n4/in.exec.Exec")]));
        ClassDocument o = WithMain(Graph([Entry("n1"), Call("n2", pure: true, pins: Pin("in.data.value", "a")), Return("n4")], [Wire("n1/out.exec.Exec", "n4/in.exec.Exec")]));
        ClassDocument t = WithMain(Graph([Entry("n1"), Call("n2", pins: Pin("in.data.value", "b")), Return("n4")], [Wire("n1/out.exec.Exec", "n4/in.exec.Exec")]));

        AssertConflict(await MergeAsync(b, o, t), MergeConflictKind.NodeProperty, "nodes[n2]");
    }

    [Fact]
    public async Task ANodeShapeChangeAgainstEditsToOtherNodesMergesCleanly()
    {
        ClassDocument b = WithMain(Chain(Call("n5")));
        ClassDocument o = WithMain(Graph([Entry("n1"), Call("n2", pure: true), Return("n4"), Call("n5")], [Wire("n1/out.exec.Exec", "n4/in.exec.Exec")]));
        ClassDocument t = WithMain(Graph(
            [Entry("n1"), Call("n2"), Return("n4"), Call("n5", pins: Pin("in.data.value", "x"))],
            [Wire("n1/out.exec.Exec", "n2/in.exec.Exec"), Wire("n2/out.exec.Exec", "n4/in.exec.Exec")]));

        ClassDocument merged = AssertClean(await MergeAsync(b, o, t));

        Assert.True(Assert.IsType<CallMethodNodeDocument>(MainGraph(merged).Nodes.Single(node => node.Id == "n2")).Pure);
    }

    [Fact]
    public async Task ANodeShapeChangeOnOneSideWithTheOtherSideUnchangedKeepsTheChange()
    {
        ClassDocument b = WithMain(Chain());
        ClassDocument o = WithMain(Graph([Entry("n1"), Call("n2", pure: true), Return("n4")], [Wire("n1/out.exec.Exec", "n4/in.exec.Exec")]));

        ClassDocument merged = AssertClean(await MergeAsync(b, o, b));

        Assert.True(Assert.IsType<CallMethodNodeDocument>(MainGraph(merged).Nodes.Single(node => node.Id == "n2")).Pure);
    }

    [Fact]
    public async Task ANodePropertyChangedDifferentlyOnBothSidesConflicts()
    {
        ClassDocument b = WithMain(Chain());
        ClassDocument o = WithMain(Graph([Entry("n1"), Call("n2", name: "Ours"), Return("n4")], MainGraph(WithMain(Chain())).Connections));
        ClassDocument t = WithMain(Graph([Entry("n1"), Call("n2", name: "Theirs"), Return("n4")], MainGraph(WithMain(Chain())).Connections));

        AssertConflict(await MergeAsync(b, o, t), MergeConflictKind.NodeProperty, "nodes[n2]");
    }

    [Fact]
    public async Task ANodeAddedWithTheSameIdAndDifferentContentConflicts()
    {
        ClassDocument b = WithMain(Chain());
        ClassDocument o = WithMain(Chain(Call("n9", name: "One")));
        ClassDocument t = WithMain(Chain(Call("n9", name: "Two")));

        AssertConflict(await MergeAsync(b, o, t), MergeConflictKind.NodeProperty, "nodes[n9]");
    }

    [Fact]
    public async Task AMemberAddedWithTheSameIdAndDifferentContentConflicts()
    {
        ClassDocument b = Class();
        ClassDocument o = Class([Method("m2", "A", EmptyGraph)]);
        ClassDocument t = Class([Method("m2", "B", EmptyGraph)]);

        AssertConflict(await MergeAsync(b, o, t), MergeConflictKind.DuplicateMember, "methods[m2]");
    }

    [Fact]
    public async Task TwoMembersAddedWithTheSameNameAndDifferentIdsConflict()
    {
        ClassDocument b = Class();
        ClassDocument o = Class([Method("m2", "Same", EmptyGraph)]);
        ClassDocument t = Class([Method("m3", "Same", EmptyGraph)]);

        AssertConflict(await MergeAsync(b, o, t), MergeConflictKind.DuplicateMember, "methods[Same]");
    }

    [Fact]
    public async Task BothSidesAddingDifferentMembersMergeCleanlyInBaseThenOursThenTheirsOrder()
    {
        ClassDocument b = Class([Method("m1", "One", EmptyGraph)]);
        ClassDocument o = Class([Method("m1", "One", EmptyGraph), Method("m2", "Two", EmptyGraph)]);
        ClassDocument t = Class([Method("m1", "One", EmptyGraph), Method("m3", "Three", EmptyGraph)]);

        ClassDocument merged = AssertClean(await MergeAsync(b, o, t));

        Assert.Equal(["m1", "m2", "m3"], merged.Methods?.Select(method => method.Id));
    }

    [Fact]
    public async Task AMemberDeletedOnOneSideAndUnchangedOnTheOtherIsRemoved()
    {
        ClassDocument b = Class([Method("m1", "One", EmptyGraph), Method("m2", "Two", EmptyGraph)]);
        ClassDocument o = Class([Method("m1", "One", EmptyGraph)]);

        ClassDocument merged = AssertClean(await MergeAsync(b, o, b));

        Assert.Equal(["m1"], merged.Methods?.Select(method => method.Id));
    }

    [Fact]
    public async Task AMemberDeletedOnOneSideAndModifiedOnTheOtherConflicts()
    {
        ClassDocument b = Class([Method("m1", "One", EmptyGraph)]);
        ClassDocument t = Class([Method("m1", "Renamed", EmptyGraph)]);

        AssertConflict(await MergeAsync(b, Class(), t), MergeConflictKind.DeleteModify, "methods[m1]");
        AssertConflict(await MergeAsync(b, t, Class()), MergeConflictKind.DeleteModify, "methods[m1]");
    }

    [Fact]
    public async Task AMemberChangedOnOneSideTakesTheChange()
    {
        ClassDocument b = Class([Method("m1", "One", EmptyGraph)]);
        ClassDocument o = Class([Method("m1", "Renamed", EmptyGraph)]);

        ClassDocument merged = AssertClean(await MergeAsync(b, o, b));

        Assert.Equal("Renamed", Assert.Single(merged.Methods ?? []).Name);
    }

    [Fact]
    public async Task ScalarFieldsChangedDifferentlyOnBothSidesConflict()
    {
        ClassDocument b = Class([Method("m1", "One", EmptyGraph)]);
        ClassDocument o = b with { Name = "Ours", Visibility = MemberVisibility.Internal };
        ClassDocument t = b with { Name = "Theirs", Visibility = MemberVisibility.Private };

        MergeOutcome outcome = await MergeAsync(b, o, t);

        AssertConflict(outcome, MergeConflictKind.Scalar, "name");
        AssertConflict(outcome, MergeConflictKind.Scalar, "visibility");
    }

    [Fact]
    public async Task AMethodRenamedDifferentlyOnBothSidesConflicts()
    {
        ClassDocument b = Class([Method("m1", "One", EmptyGraph)]);
        ClassDocument o = Class([Method("m1", "Ours", EmptyGraph)]);
        ClassDocument t = Class([Method("m1", "Theirs", EmptyGraph)]);

        AssertConflict(await MergeAsync(b, o, t), MergeConflictKind.Scalar, "methods[m1].name");
    }

    [Fact]
    public async Task LocalsAddedDifferentlyUnderOneNameConflict()
    {
        ClassDocument b = WithMain(Chain());
        ClassDocument o = WithMain(Graph(MainGraph(b).Nodes, MainGraph(b).Connections, [new LocalVariableDocument("x", StringType)]));
        ClassDocument t = WithMain(Graph(MainGraph(b).Nodes, MainGraph(b).Connections, [new LocalVariableDocument("x", new TypeRef("System.Int32"))]));

        AssertConflict(await MergeAsync(b, o, t), MergeConflictKind.Scalar, "locals[x]");
    }

    [Fact]
    public async Task LocalsAddedOnBothSidesUnderDifferentNamesAreKept()
    {
        ClassDocument b = WithMain(Chain());
        ClassDocument o = WithMain(Graph(MainGraph(b).Nodes, MainGraph(b).Connections, [new LocalVariableDocument("x", StringType)]));
        ClassDocument t = WithMain(Graph(MainGraph(b).Nodes, MainGraph(b).Connections, [new LocalVariableDocument("y", StringType)]));

        ClassDocument merged = AssertClean(await MergeAsync(b, o, t));

        Assert.Equal(["x", "y"], MainGraph(merged).Locals?.Select(local => local.Name));
    }

    [Fact]
    public async Task APinDeletedOnOneSideAndUnchangedOnTheOtherIsRemoved()
    {
        ClassDocument b = WithMain(Graph([Entry("n1"), Call("n2", pins: Pin("in.data.value", "a")), Return("n4")]));
        ClassDocument o = WithMain(Graph([Entry("n1"), Call("n2"), Return("n4")]));

        ClassDocument merged = AssertClean(await MergeAsync(b, o, b));

        Assert.Null(MainGraph(merged).Nodes.Single(node => node.Id == "n2").Pins);
    }

    [Fact]
    public async Task APinDeletedOnOneSideAndChangedOnTheOtherConflicts()
    {
        ClassDocument b = WithMain(Graph([Entry("n1"), Call("n2", pins: Pin("in.data.value", "a")), Return("n4")]));
        ClassDocument o = WithMain(Graph([Entry("n1"), Call("n2"), Return("n4")]));
        ClassDocument t = WithMain(Graph([Entry("n1"), Call("n2", pins: Pin("in.data.value", "b")), Return("n4")]));

        AssertConflict(await MergeAsync(b, o, t), MergeConflictKind.PinValue, "pins[in.data.value]");
    }

    private static VariableDocument Variable(AccessorDocument? getter = null, AccessorDocument? setter = null, string name = "V") =>
        new("v1", name, MemberVisibility.Public, VariableModifiers.None, EmptyGraph, getter, setter);

    private static AccessorDocument Accessor(MemberVisibility visibility = MemberVisibility.Public, params NodeDocument[] nodes) => new(visibility, Graph(nodes));

    [Fact]
    public async Task AnAccessorAddedOnOneSideIsKept()
    {
        ClassDocument b = Class(variables: [Variable()]);
        ClassDocument o = Class(variables: [Variable(getter: Accessor())]);

        ClassDocument merged = AssertClean(await MergeAsync(b, o, b));

        Assert.NotNull(Assert.Single(merged.Variables ?? []).Getter);
        Assert.Null(Assert.Single(merged.Variables ?? []).Setter);
    }

    [Fact]
    public async Task AnAccessorAddedIdenticallyOnBothSidesIsKeptOnce()
    {
        ClassDocument b = Class(variables: [Variable()]);
        ClassDocument o = Class(variables: [Variable(getter: Accessor())]);

        ClassDocument merged = AssertClean(await MergeAsync(b, o, o));

        Assert.NotNull(Assert.Single(merged.Variables ?? []).Getter);
    }

    [Fact]
    public async Task AnAccessorAddedDifferentlyOnBothSidesConflicts()
    {
        ClassDocument b = Class(variables: [Variable()]);
        ClassDocument o = Class(variables: [Variable(getter: Accessor(MemberVisibility.Public))]);
        ClassDocument t = Class(variables: [Variable(getter: Accessor(MemberVisibility.Private))]);

        AssertConflict(await MergeAsync(b, o, t), MergeConflictKind.Scalar, "getter");
    }

    [Fact]
    public async Task AnAccessorDeletedOnOneSideAndUnchangedOnTheOtherIsRemoved()
    {
        ClassDocument b = Class(variables: [Variable(setter: Accessor())]);
        ClassDocument o = Class(variables: [Variable()]);

        ClassDocument merged = AssertClean(await MergeAsync(b, o, b));

        Assert.Null(Assert.Single(merged.Variables ?? []).Setter);
    }

    [Fact]
    public async Task AnAccessorDeletedOnOneSideAndModifiedOnTheOtherConflicts()
    {
        ClassDocument b = Class(variables: [Variable(setter: Accessor(MemberVisibility.Public))]);
        ClassDocument o = Class(variables: [Variable()]);
        ClassDocument t = Class(variables: [Variable(setter: Accessor(MemberVisibility.Private))]);

        AssertConflict(await MergeAsync(b, o, t), MergeConflictKind.DeleteModify, "setter");
    }

    [Fact]
    public async Task AnAccessorKeptOnBothSidesMergesItsVisibilityAndGraph()
    {
        ClassDocument b = Class(variables: [Variable(getter: Accessor(MemberVisibility.Public, Entry("n1")))]);
        ClassDocument o = Class(variables: [Variable(getter: Accessor(MemberVisibility.Private, Entry("n1")))]);
        ClassDocument t = Class(variables: [Variable(getter: Accessor(MemberVisibility.Public, Entry("n1"), Return("n2")))]);

        AccessorDocument getter = Assert.Single(AssertClean(await MergeAsync(b, o, t)).Variables ?? []).Getter ?? throw new InvalidOperationException("The getter is kept.");

        Assert.Equal(MemberVisibility.Private, getter.Visibility);
        Assert.Equal(["n1", "n2"], getter.Graph.Nodes.Select(node => node.Id));
    }

    [Fact]
    public async Task LayoutOfANodeOnlyTheirsAddedIsCarriedAndLayoutOfDeletedNodesIsDropped()
    {
        ClassDocument b = WithMain(Chain()) with { Layout = Layout(Main, ("n1", 1, 1), ("n2", 2, 2), ("n4", 4, 4)) };
        ClassDocument o = WithMain(Graph([Entry("n1"), Return("n4")])) with { Layout = Layout(Main, ("n1", 1, 1), ("n4", 4, 4)) };
        ClassDocument t = WithMain(Chain(Call("n5"))) with { Layout = Layout(Main, ("n1", 1, 1), ("n2", 2, 2), ("n4", 4, 4), ("n5", 5, 5)) };

        ClassDocument merged = AssertClean(await MergeAsync(b, o, t));

        var layout = Assert.IsType<System.Collections.Generic.SortedDictionary<string, System.Collections.Generic.SortedDictionary<string, int[]>>>(merged.Layout);
        Assert.Equal(["n1", "n4", "n5"], layout[Main].Keys);
        Assert.Equal([5, 5], layout[Main]["n5"]);
    }

    private static System.Collections.Generic.SortedDictionary<string, System.Collections.Generic.SortedDictionary<string, int[]>> Layout(string key, params (string Node, int X, int Y)[] positions) =>
        new() { [key] = new(positions.ToDictionary(position => position.Node, position => new[] { position.X, position.Y }, StringComparer.Ordinal), StringComparer.Ordinal) };
}
