using Fx.Kit;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Nodes;

namespace Fx.Fail.Disposal;

/// <summary>A node library whose disposal fails.</summary>
public sealed class ThrowingLibrary : INodeLibrary, IDisposable
{
    private readonly FxNodeLibrary inner = new("fx.fail.disposal", FxKit.Kind("fx.fail.disposal/Ping", () => "ping"));

    /// <inheritdoc />
    public string Id => inner.Id;

    /// <inheritdoc />
    public IReadOnlyList<NodeKindDescriptor> NodeKinds => inner.NodeKinds;

    /// <inheritdoc />
    public void Dispose() => throw new InvalidOperationException("The library cannot be disposed.");
}

/// <summary>An extension that contributes a library whose disposal throws.</summary>
public sealed class FailDisposalExtension : INetPrintsExtension
{
    /// <inheritdoc />
    public void Register(IExtensionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder
            .AddNodeLibrary(new ThrowingLibrary())
            .AddJsonTypeInfoResolver(FxKitJsonContext.Default);
    }
}
