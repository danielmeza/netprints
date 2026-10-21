using Fx.Kit;
using NetPrints.Extensibility;

namespace Fx.Fail.Views;

/// <summary>An extension whose view source, <c>Views/BadView.axaml</c>, holds literals a theme cannot reach.</summary>
public sealed class FailViewsExtension : INetPrintsExtension
{
    /// <inheritdoc />
    public void Register(IExtensionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder
            .AddNodeLibrary(new FxNodeLibrary("fx.fail.views", FxKit.Kind("fx.fail.views/Ping", () => "ping")))
            .AddJsonTypeInfoResolver(FxKitJsonContext.Default);
    }
}
