using Fixture;
using Fx.Kit;
using NetPrints.Extensibility;

namespace Fx.HostConflict;

/// <summary>A node whose translator calls a library that the host holds in an older version than this extension was built against.</summary>
public sealed class HostConflictExtension : INetPrintsExtension
{
    /// <summary>The extension id, also the manifest id.</summary>
    public const string Id = "fx.host-conflict";

    /// <summary>The kind id of the node.</summary>
    public const string KindId = "fx.host-conflict/Describe";

    /// <inheritdoc />
    public void Register(IExtensionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder
            .AddNodeLibrary(new FxNodeLibrary(Id, FxKit.Kind(KindId, HostLib.Describe)))
            .AddJsonTypeInfoResolver(FxKitJsonContext.Default);
    }
}
