using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Graph.Pins;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.Graph;

/// <summary>
/// A graph opened before the reflection host finished loading (for example a project passed on
/// the command line) must pick up overloads, enum names and documentation when it loads.
/// </summary>
public sealed class ReflectionReloadTests : IDisposable
{
    private readonly ReflectionHost host = new(new InlineDispatcher(), TestExtensions.CreateBuiltIn(), NullLogger<ReflectionHost>.Instance);
    private readonly ClassEditorViewModel classEditor;
    private readonly MethodGraph method;

    public ReflectionReloadTests()
    {
        var cls = new ClassGraph { Name = "C", Namespace = "N" };
        classEditor = new ClassEditorViewModel(cls, new TestEditor(host).Context);
        classEditor.CreateMethodCommand.Execute(null);
        method = (MethodGraph)classEditor.Methods.Single().Graph;
    }

    public void Dispose() => classEditor.Dispose();

    private static MethodSpecifier WriteLine(TypeSpecifier parameter) =>
        new("WriteLine", [new MethodParameter("value", parameter, MethodParameterPassType.Default, false, null)],
            [], MethodModifiers.Static, MemberVisibility.Public, TypeSpecifier.FromType(typeof(Console)), []);

    [Fact]
    public async Task OverloadsRefreshWhenReflectionLoads()
    {
        var call = new CallMethodNode(method, WriteLine(TypeSpecifier.FromType<string>()));
        var node = classEditor.OpenedGraph!.Nodes.Single(n => n.Node == call);
        Assert.Empty(node.Overloads);

        await host.ReloadAsync(Project.FromSnapshot(TestSnapshots.WithRuntimeAssemblies("P", "N")), TestContext.Current.CancellationToken);

        Assert.True(node.Overloads.Count > 10, $"overloads after load: {node.Overloads.Count}");
        Assert.True(node.ShowOverloads);
    }

    [Fact]
    public async Task EnumNamesAndDocumentationRefreshWhenReflectionLoads()
    {
        var call = new CallMethodNode(method, WriteLine(TypeSpecifier.FromType<DayOfWeek>()));
        NodePinViewModel pin = classEditor.OpenedGraph!.Nodes.Single(n => n.Node == call).InputDataPins.Single();
        var node = classEditor.OpenedGraph!.Nodes.Single(n => n.Node == call);
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
