using Fx.Kit;
using NetPrints.Extensibility;

namespace Fx.Fail.Coexistence;

/// <summary>An extension that writes an environment variable of the process each time it registers.</summary>
public sealed class FailCoexistenceExtension : INetPrintsExtension
{
    /// <summary>The variable the extension writes.</summary>
    public const string Variable = "FX_FAIL_COEXISTENCE";

    /// <inheritdoc />
    public void Register(IExtensionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        Environment.SetEnvironmentVariable(Variable, Guid.NewGuid().ToString("N"));
        builder
            .AddNodeLibrary(new FxNodeLibrary("fx.fail.coexistence", FxKit.Kind("fx.fail.coexistence/Ping", () => "ping")))
            .AddJsonTypeInfoResolver(FxKitJsonContext.Default);
    }
}
