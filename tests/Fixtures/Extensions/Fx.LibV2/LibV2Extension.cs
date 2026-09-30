using Fixture;
using Fx.Kit;
using NetPrints.Extensibility;

namespace Fx.LibV2;

/// <summary>A node whose translator calls its own private copy of the shared library, version 2.</summary>
public sealed class LibV2Extension : INetPrintsExtension
{
    /// <summary>The extension id, also the manifest id.</summary>
    public const string Id = "fx.libv2";

    /// <summary>The kind id of the node.</summary>
    public const string KindId = "fx.libv2/Describe";

    /// <summary>The shared library assembly as this extension sees it, for isolation checks.</summary>
    public static System.Reflection.Assembly SeenSharedLib => typeof(SharedLib).Assembly;

    /// <inheritdoc />
    public void Register(IExtensionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder
            .AddNodeLibrary(new FxNodeLibrary(Id, FxKit.Kind(KindId, () => SharedLib.Describe("fx.libv2"))))
            .AddJsonTypeInfoResolver(FxKitJsonContext.Default);
    }
}
