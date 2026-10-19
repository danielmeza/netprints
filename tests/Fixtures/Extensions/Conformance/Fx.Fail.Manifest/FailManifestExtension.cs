using Fx.Kit;
using NetPrints.Extensibility;

namespace Fx.Fail.Manifest;

/// <summary>An extension whose manifest asks for <c>netprintsApi</c> 9.0.</summary>
public sealed class FailManifestExtension : INetPrintsExtension
{
    /// <inheritdoc />
    public void Register(IExtensionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder
            .AddNodeLibrary(new FxNodeLibrary("fx.fail.manifest", FxKit.Kind("fx.fail.manifest/Ping", () => "ping")))
            .AddJsonTypeInfoResolver(FxKitJsonContext.Default);
    }
}
