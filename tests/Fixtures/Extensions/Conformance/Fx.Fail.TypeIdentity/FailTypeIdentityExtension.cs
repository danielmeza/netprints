using Fx.Kit;
using NetPrints.Extensibility;
using NetPrints.Generation;

namespace Fx.Fail.TypeIdentity;

/// <summary>An extension that references NetPrints.Generation, which the host does not share.</summary>
public sealed class FailTypeIdentityExtension : INetPrintsExtension
{
    /// <summary>Names a type of the referenced assembly, so the reference is part of the compiled extension.</summary>
    /// <returns>The type.</returns>
    public static Type GeneratorType() => typeof(GraphCodeGenerator);

    /// <inheritdoc />
    public void Register(IExtensionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder
            .AddNodeLibrary(new FxNodeLibrary("fx.fail.typeidentity", FxKit.Kind("fx.fail.typeidentity/Ping", () => "ping")))
            .AddJsonTypeInfoResolver(FxKitJsonContext.Default);
    }
}
