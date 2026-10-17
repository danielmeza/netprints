using Fixture;
using Fx.Kit;
using NetPrints.Extensibility;

namespace Fx.TransitiveConflict;

/// <summary>A node whose translator calls a private library that was built against a newer shared library than the dependency provides.</summary>
public sealed class TransitiveConflictExtension : INetPrintsExtension
{
    /// <summary>The extension id, also the manifest id.</summary>
    public const string Id = "fx.transitive-conflict";

    /// <summary>The kind id of the node.</summary>
    public const string KindId = "fx.transitive-conflict/Describe";

    /// <inheritdoc />
    public void Register(IExtensionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder
            .AddNodeLibrary(new FxNodeLibrary(Id, FxKit.Kind(KindId, MidLib.Describe)))
            .AddJsonTypeInfoResolver(FxKitJsonContext.Default);
    }
}
