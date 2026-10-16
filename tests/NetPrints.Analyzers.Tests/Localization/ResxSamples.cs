using System.Linq;
using NetPrints.Analyzers.Tests.Pins;

namespace NetPrints.Analyzers.Tests.Localization;

/// <summary>Samples for the NPA040-NPA042 tests: a loop node kind and resx files with chosen entries.</summary>
internal static class ResxSamples
{
    public const string NeutralPath = "Strings/NodeStrings.resx";

    public const string SatellitePath = "Strings/NodeStrings.es.resx";

    public const string Kind = "sample.loop";

    public const string TitleKey = "Node.sample.loop.Title";

    public const string ExecutionKey = "Node.sample.loop.Pin.in.exec.Execution";

    public const string LoopKey = "Node.sample.loop.Pin.out.exec.Loop";

    /// <summary>Gets the sample node class.</summary>
    public static string Loop() =>
        PinSamples.Header + $$"""
            [NodeKind("{{Kind}}")]
            public sealed partial class LoopNode : Node
            {
                public LoopNode(NodeGraph graph) : base(graph)
                {
                    DeclarePins();
                }

                public partial NodeInputExecPin ExecutionPin { get; }

                public partial NodeOutputExecPin LoopPin { get; }
            }
            """;

    /// <summary>Builds a resx text from key and value pairs, one entry per line.</summary>
    public static string Resx(params (string Key, string Value)[] entries) =>
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<root>\n"
        + string.Concat(entries.Select(entry => $"  <data name=\"{entry.Key}\" xml:space=\"preserve\"><value>{entry.Value}</value></data>\n"))
        + "</root>\n";

    public static string Complete() => Resx((TitleKey, "Loop"), (ExecutionKey, "Execution"), (LoopKey, "Loop body"));
}
