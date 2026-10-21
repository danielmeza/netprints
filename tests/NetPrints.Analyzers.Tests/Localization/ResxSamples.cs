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

    /// <summary>Gets the sample node class, whose title is built from its content.</summary>
    public static string Loop() => LoopClass(string.Empty);

    /// <summary>Gets the sample node class with a title that is the same for every instance, so its text comes from the resx.</summary>
    public static string FixedTitleLoop() => LoopClass("    public override bool TitleIsFixed => true;\n");

    /// <summary>Gets the sample node class whose fixed title comes from a base class, so the kind itself declares no override.</summary>
    public static string InheritedFixedTitleLoop() =>
        PinSamples.Header + $$"""
            public abstract class FixedTitleNode : Node
            {
                protected FixedTitleNode(NodeGraph graph) : base(graph)
                {
                }

                public override bool TitleIsFixed => true;
            }

            [NodeKind("{{Kind}}")]
            public sealed partial class LoopNode : FixedTitleNode
            {
                public LoopNode(NodeGraph graph) : base(graph)
                {
                    DeclarePins();
                }

                public partial NodeInputExecPin ExecutionPin { get; }

                public partial NodeOutputExecPin LoopPin { get; }
            }
            """;

    private static string LoopClass(string extra) =>
        PinSamples.Header + $$"""
            [NodeKind("{{Kind}}")]
            public sealed partial class LoopNode : Node
            {
            {{extra}}
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
