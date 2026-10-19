using Fx.Kit;
using NetPrints.Extensibility;

namespace Fx.Fail.RegistryIssues;

/// <summary>An extension that contributes the same node kind from two libraries; the registry rejects the second.</summary>
public sealed class FailRegistryIssuesExtension : INetPrintsExtension
{
    private const string Kind = "fx.fail.registryissues/Ping";

    /// <inheritdoc />
    public void Register(IExtensionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder
            .AddNodeLibrary(new FxNodeLibrary("fx.fail.registryissues", FxKit.Kind(Kind, () => "first")))
            .AddNodeLibrary(new FxNodeLibrary("fx.fail.registryissues.again", FxKit.Kind(Kind, () => "second")))
            .AddJsonTypeInfoResolver(FxKitJsonContext.Default);
    }
}
