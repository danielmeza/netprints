using Fx.Kit;
using NetPrints.Extensibility;

namespace Fx.Fail.Packaging;

/// <summary>An extension whose folder holds NetPrints.Core.dll.</summary>
public sealed class FailPackagingExtension : INetPrintsExtension
{
    /// <inheritdoc />
    public void Register(IExtensionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder
            .AddNodeLibrary(new FxNodeLibrary("fx.fail.packaging", FxKit.Kind("fx.fail.packaging/Ping", () => "ping")))
            .AddJsonTypeInfoResolver(FxKitJsonContext.Default);
    }
}
