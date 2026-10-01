using Fx.Kit;
using NetPrints.Core;
using NetPrints.Extensibility;
using NetPrints.Graph;
using NetPrints.Translator;

namespace Fx.Beta;

/// <summary>Marks methods whose graph holds an <c>fx.alpha/Ping</c> node.</summary>
public sealed class BetaMemberEmitter : IMemberEmitter
{
    /// <summary>The marker the emitter adds.</summary>
    public const string Marker = "System.ComponentModel.Description(\"fx.beta\")";

    private const string AlphaPingNodeType = "Fx.Alpha.AlphaPingNode";

    /// <inheritdoc />
    public string Id => "fx.beta/member";

    /// <inheritdoc />
    public void EmitMember(MemberEmitContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Kind == EmittedMemberKind.Method
            && context.Model is MethodGraph method
            && method.Nodes.Any(node => node.GetType().FullName == AlphaPingNodeType))
        {
            context.Attributes.Add(Marker);
        }
    }
}

/// <summary>Depends on <c>fx.alpha</c>: a node kind and a member emitter that reacts to alpha's node.</summary>
public sealed class BetaExtension : INetPrintsExtension
{
    /// <summary>The extension id, also the manifest id.</summary>
    public const string Id = "fx.beta";

    /// <summary>The kind id of the node.</summary>
    public const string PongKind = "fx.beta/Pong";

    /// <summary>The text the Pong translator writes.</summary>
    public const string PongText = "fx.beta pong";

    /// <inheritdoc />
    public void Register(IExtensionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder
            .AddNodeLibrary(new FxNodeLibrary(Id, FxKit.Kind(PongKind, () => PongText)))
            .AddJsonTypeInfoResolver(FxKitJsonContext.Default)
            .AddMemberEmitter(new BetaMemberEmitter());
    }
}
