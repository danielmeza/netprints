using System.Globalization;
using Fx.Kit;
using NetPrints.Extensibility;

namespace Fx.Fail.Determinism;

/// <summary>An extension whose translator writes how often it has run.</summary>
public sealed class FailDeterminismExtension : INetPrintsExtension
{
    private const string CounterKey = "fx.fail.determinism.runs";

    /// <inheritdoc />
    public void Register(IExtensionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder
            .AddNodeLibrary(new FxNodeLibrary("fx.fail.determinism", FxKit.Kind("fx.fail.determinism/Ping", NextRun)))
            .AddJsonTypeInfoResolver(FxKitJsonContext.Default);
    }

    private static string NextRun()
    {
        int runs = AppDomain.CurrentDomain.GetData(CounterKey) is int previous ? previous + 1 : 1;
        AppDomain.CurrentDomain.SetData(CounterKey, runs);
        return "run " + runs.ToString(CultureInfo.InvariantCulture);
    }
}
