using System.Threading.Tasks;
using NetPrints.Analyzers.Tests.Infrastructure;
using Xunit;

namespace NetPrints.Analyzers.Tests.Pins;

/// <summary>NPA005: a base class adds pins imperatively to a collection in which the convention declares pins.</summary>
public class Npa005Tests
{
    private const string Id = "NPA005";

    [Fact]
    public async Task ABaseThatAddsToACollectionItDeclaresInIsReported()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(
            PinSamples.Header + """
            [NodeBase]
            public abstract partial class ExecBase : Node
            {
                protected ExecBase(NodeGraph graph) : base(graph)
                {
                    {|NPA005:AddInputExecPin("Late")|};
                }

                public partial NodeInputExecPin ExecutionPin { get; }
            }
            """,
            Id);
    }

    [Fact]
    public async Task ABaseThatAddsToACollectionADerivedClassDeclaresInIsReported()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(
            PinSamples.Header + """
            [NodeBase]
            public abstract partial class ExecBase : Node
            {
                protected ExecBase(NodeGraph graph) : base(graph)
                {
                    {|NPA005:OutputDataPins.Add(new NodeOutputDataPin<int>(this, "Late"))|};
                    AddInputTypePin("Other");
                }

                public partial NodeInputExecPin ExecutionPin { get; }
            }

            [NodeKind("sample.derived")]
            public sealed partial class DerivedNode : ExecBase
            {
                public DerivedNode(NodeGraph graph) : base(graph)
                {
                    DeclarePins();
                }

                public partial NodeOutputDataPin<int> ResultPin { get; }
            }
            """,
            Id);
    }

    [Fact]
    public async Task ABaseThatAddsADataPinToACollectionItDeclaresInIsReported()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(
            PinSamples.Header + """
            [NodeBase]
            public abstract partial class ValueBase : Node
            {
                protected ValueBase(NodeGraph graph) : base(graph)
                {
                    {|NPA005:AddOutputDataPin("Late", new ObservableValue<BaseType>(TypeSpecifier.FromType<int>()))|};
                    AddInputDataPin("Other", new ObservableValue<BaseType>(TypeSpecifier.FromType<int>()));
                }

                public partial NodeOutputDataPin<int> ResultPin { get; }
            }
            """,
            Id);
    }

    [Fact]
    public async Task AKindThatAddsAfterDeclarePinsIsNotReported()
    {
        await PinAnalyzerHarness.AssertDiagnosticsAsync(
            PinSamples.Header + """
            [NodeKind("sample.loop")]
            public sealed partial class LoopNode : Node
            {
                public LoopNode(NodeGraph graph) : base(graph)
                {
                    DeclarePins();
                    AddInputExecPin("Late");
                }

                public partial NodeInputExecPin ExecutionPin { get; }
            }
            """,
            Id);
    }
}
