using Fixture;
using Fx.Kit;
using NetPrints.Extensibility;

namespace Fx.Diamond;

/// <summary>A node whose translator calls the shared library, compiled against version 1.</summary>
public sealed class DiamondExtension : INetPrintsExtension
{
    /// <summary>The extension id, also the manifest id.</summary>
    public const string Id = "fx.diamond";

    /// <summary>The kind id of the node.</summary>
    public const string KindId = "fx.diamond/Describe";

    /// <summary>The shared library assembly as this extension sees it, for resolution-order checks.</summary>
    public static System.Reflection.Assembly SeenSharedLib => typeof(SharedLib).Assembly;

    /// <inheritdoc />
    public void Register(IExtensionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder
            .AddNodeLibrary(new FxNodeLibrary(Id, FxKit.Kind(KindId, SharedLib.Describe)))
            .AddJsonTypeInfoResolver(FxKitJsonContext.Default);
    }
}
