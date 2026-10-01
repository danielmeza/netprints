using Fx.TypesProvider;
using NetPrints.Core;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Nodes;
using NetPrints.Graph;
using NetPrints.Translator;

namespace Fx.TypesConsumer;

/// <summary>A node whose data input has the provider's type.</summary>
public sealed class UseNode : Node
{
    /// <summary>Creates the node and adds it to <paramref name="graph"/>.</summary>
    /// <param name="graph">The graph.</param>
    public UseNode(NodeGraph graph)
        : base(graph)
    {
        AddInputExecPin("Exec");
        AddInputDataPin("Value", TypeSpecifier.FromType(typeof(ProviderType)));
        AddOutputExecPin("Then");
    }
}

/// <summary>Adds the provider's marker to every class as a description attribute.</summary>
public sealed class ConsumerClassEmitter : IClassEmitter
{
    /// <inheritdoc />
    public string Id => "fx.types-consumer/class";

    /// <inheritdoc />
    public void EmitClass(ClassEmitContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Attributes.Add($"System.ComponentModel.Description(\"{ProviderType.Describe()}\")");
    }
}

/// <summary>Uses <see cref="ProviderType"/> in a node pin and an emitter.</summary>
public sealed class TypesConsumerExtension : INetPrintsExtension
{
    /// <summary>The extension id, also the manifest id.</summary>
    public const string Id = "fx.types-consumer";

    /// <summary>The kind id of <see cref="UseNode"/>.</summary>
    public const string KindId = "fx.types-consumer/Use";

    /// <summary>The provider type as this extension sees it, for identity checks.</summary>
    public static Type SeenProviderType => typeof(ProviderType);

    /// <inheritdoc />
    public void Register(IExtensionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder
            .AddNodeLibrary(new Fx.Kit.FxNodeLibrary(Id, Fx.Kit.FxKit.Kind(KindId, typeof(UseNode), graph => new UseNode(graph), ProviderType.Describe)))
            .AddJsonTypeInfoResolver(Fx.Kit.FxKitJsonContext.Default)
            .AddClassEmitter(new ConsumerClassEmitter());
    }
}
