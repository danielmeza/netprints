using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.Messaging;
using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.UndoRedo;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.Graph;

/// <summary>
/// M2 baseline (research R4 step 1, SC-005): building the view models of a generated method graph of 1000 nodes
/// and about 4000 pins. Time and allocated bytes go to the test output, with the number of calls the graph makes
/// into its class editor services (the messenger and the project classes accessor), counted per pin by counting
/// fakes. The numbers pin today's hand-wired behaviour; the composition step C1 compares against them.
/// </summary>
public sealed class GraphScopePerformanceTests(TestEditor editor)
{
    private const int NodeCount = 1000;
    private const int Runs = 5;

    [Fact(Timeout = 300_000)]
    [Trait("Category", "Performance")]
    public void BuildingALargeGraphIsMeasured()
    {
        var output = TestContext.Current.TestOutputHelper
            ?? throw new InvalidOperationException($"{nameof(TestContext)} has no {nameof(TestContext.Current.TestOutputHelper)}.");

        var times = new List<double>();
        var bytes = new List<double>();
        int pins = 0;
        int nodes = 0;
        CountingServices counting = new(editor.Context);
        for (int run = 1; run <= Runs; run++)
        {
            MethodGraph method = BuildGraph();
            counting = new CountingServices(editor.Context);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            long before = GC.GetAllocatedBytesForCurrentThread();
            var stopwatch = Stopwatch.StartNew();
            var graph = new NodeGraphViewModel(method, counting.Services);
            stopwatch.Stop();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            nodes = graph.Nodes.Count();
            pins = graph.Nodes.Sum(node => node.AllPins.Count());
            times.Add(stopwatch.Elapsed.TotalMilliseconds);
            bytes.Add(allocated);
            output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"M2 run {run}/{Runs}: {nodes} nodes, {pins} pins, {stopwatch.Elapsed.TotalMilliseconds:F1} ms, {allocated} bytes"));
        }

        double medianMs = Median(times);
        double medianBytes = Median(bytes);
        output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"M2 median: {medianMs:F1} ms, {medianBytes:F0} bytes, {medianBytes / nodes:F0} bytes per node, {medianBytes / pins:F0} bytes per pin"));
        output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"M2 service calls (last run): messenger {counting.MessengerCalls} ({counting.MessengerCalls / (double)pins:F3} per pin), project classes {counting.ProjectClassesCalls} ({counting.ProjectClassesCalls / (double)pins:F3} per pin)"));

        Assert.Equal(NodeCount + 2, nodes);
        Assert.InRange(pins, NodeCount * 3, NodeCount * 6);
    }

    private static MethodGraph BuildGraph()
    {
        var method = new MethodGraph("Large") { Class = new ClassGraph { Name = "C", Namespace = "N" } };
        NodeOutputExecPin previous = method.EntryNode.InitialExecutionPin;
        for (int index = 0; index < NodeCount; index++)
        {
            var call = new CallMethodNode(method, WriteLine());
            GraphUtil.ConnectExecPins(previous, call.InputExecPins[0]);
            previous = call.OutputExecPins[0];
        }

        GraphUtil.ConnectExecPins(previous, method.MainReturnNode.ReturnPin);
        return method;
    }

    private static MethodSpecifier WriteLine() =>
        new("WriteLine",
            [
                new MethodParameter("format", TypeSpecifier.FromType<string>(), MethodParameterPassType.Default, false, null),
                new MethodParameter("arg", TypeSpecifier.FromType<object>(), MethodParameterPassType.Default, false, null),
            ],
            [], MethodModifiers.Static, MemberVisibility.Public, TypeSpecifier.FromType(typeof(Console)), []);

    private static double Median(List<double> values)
    {
        values.Sort();
        return values[values.Count / 2];
    }

    private sealed class CountingServices
    {
        private readonly CountingMessenger messenger = new(new StrongReferenceMessenger());
        private int projectClassesCalls;

        public CountingServices(EditorContext context) =>
            Services = new ClassEditorServices(context, new UndoRedoStack(), messenger, () =>
            {
                projectClassesCalls++;
                return [];
            });

        public ClassEditorServices Services { get; }

        public int MessengerCalls => messenger.Calls;

        public int ProjectClassesCalls => projectClassesCalls;
    }

    private sealed class CountingMessenger(IMessenger inner) : IMessenger
    {
        public int Calls { get; private set; }

        public bool IsRegistered<TMessage, TToken>(object recipient, TToken token)
            where TMessage : class
            where TToken : IEquatable<TToken>
        {
            Calls++;
            return inner.IsRegistered<TMessage, TToken>(recipient, token);
        }

        public void Register<TRecipient, TMessage, TToken>(TRecipient recipient, TToken token, MessageHandler<TRecipient, TMessage> handler)
            where TRecipient : class
            where TMessage : class
            where TToken : IEquatable<TToken>
        {
            Calls++;
            inner.Register(recipient, token, handler);
        }

        public void UnregisterAll(object recipient)
        {
            Calls++;
            inner.UnregisterAll(recipient);
        }

        public void UnregisterAll<TToken>(object recipient, TToken token)
            where TToken : IEquatable<TToken>
        {
            Calls++;
            inner.UnregisterAll(recipient, token);
        }

        public void Unregister<TMessage, TToken>(object recipient, TToken token)
            where TMessage : class
            where TToken : IEquatable<TToken>
        {
            Calls++;
            inner.Unregister<TMessage, TToken>(recipient, token);
        }

        public TMessage Send<TMessage, TToken>(TMessage message, TToken token)
            where TMessage : class
            where TToken : IEquatable<TToken>
        {
            Calls++;
            return inner.Send(message, token);
        }

        public void Cleanup() => inner.Cleanup();

        public void Reset() => inner.Reset();
    }
}
