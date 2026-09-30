using Fx.Kit;
using NetPrints.Extensibility;
using NetPrintsFixture.Runtime;

namespace Fx.PrefixedPrivate;

/// <summary>A node whose translator uses a private dependency named like a host assembly.</summary>
public sealed class PrefixedPrivateExtension : INetPrintsExtension
{
    /// <summary>The extension id, also the manifest id.</summary>
    public const string Id = "fx.private-prefix";

    /// <summary>The kind id of the node.</summary>
    public const string KindId = "fx.private-prefix/Describe";

    /// <inheritdoc />
    public void Register(IExtensionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder
            .AddNodeLibrary(new FxNodeLibrary(Id, FxKit.Kind(KindId, Helper.Name)))
            .AddJsonTypeInfoResolver(FxKitJsonContext.Default);
    }
}
