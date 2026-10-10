using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Graph.Pins;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.UndoRedo;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.Graph;

/// <summary>
/// A graph opened before the reflection host finished loading (for example a project passed on
/// the command line) must pick up overloads, enum names and documentation when it loads.
/// </summary>
public sealed class ReflectionReloadTests : IDisposable
{
    private readonly ReflectionHost host = new(new InlineDispatcher(), TestExtensions.CreateBuiltIn(), NullLogger<ReflectionHost>.Instance);
    private readonly ClassContext classContext;
    private readonly NodeGraphViewModel graph;
    private readonly MethodGraph method;

    public ReflectionReloadTests()
    {
        var cls = new ClassGraph { Name = "C", Namespace = "N" };
        classContext = new ClassContext(cls, new TestEditor(host).Context, new UndoRedoStack());
        method = classContext.CreateMethod();
        graph = new NodeGraphViewModel(method, classContext.Services);
    }

    public void Dispose()
    {
        graph.Dispose();
        classContext.Dispose();
    }

    private static MethodSpecifier WriteLine(TypeSpecifier parameter) =>
        new("WriteLine", [new MethodParameter("value", parameter, MethodParameterPassType.Default, false, null)],
            [], MethodModifiers.Static, MemberVisibility.Public, TypeSpecifier.FromType(typeof(Console)), []);

    [Fact]
    public async Task OverloadsRefreshWhenReflectionLoads()
    {
        var call = new CallMethodNode(method, WriteLine(TypeSpecifier.FromType<string>()));
        var node = graph.Nodes.Single(n => n.Node == call);
        Assert.False(node.ShowOverloads);

        await host.ReloadAsync(Project.FromSnapshot(TestSnapshots.WithRuntimeAssemblies("P", "N")), TestContext.Current.CancellationToken);

        var rows = node.OverloadPicker?.Rows.Count ?? 0;
        Assert.True(rows > 10, $"overloads after load: {rows}");
        Assert.True(node.ShowOverloads);
    }

    [Fact]
    public async Task EnumNamesAndDocumentationRefreshWhenReflectionLoads()
    {
        var call = new CallMethodNode(method, WriteLine(TypeSpecifier.FromType<DayOfWeek>()));
        NodePinViewModel pin = graph.Nodes.Single(n => n.Node == call).InputDataPins.Single();
        var node = graph.Nodes.Single(n => n.Node == call);
        var pinChanges = new List<string?>();
        var nodeChanges = new List<string?>();
        pin.PropertyChanged += (_, e) => pinChanges.Add(e.PropertyName);
        node.PropertyChanged += (_, e) => nodeChanges.Add(e.PropertyName);

        await host.ReloadAsync(Project.FromSnapshot(TestSnapshots.WithRuntimeAssemblies("P", "N")), TestContext.Current.CancellationToken);

        Assert.Contains(nameof(NodePinViewModel.PossibleEnumNames), pinChanges);
        Assert.Contains("Monday", pin.PossibleEnumNames!);
        Assert.Contains(nameof(NodePinViewModel.ToolTip), pinChanges);
        Assert.Contains(nameof(node.ToolTip), nodeChanges);
    }
}
