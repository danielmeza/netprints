using System.Globalization;
using Fx.Kit;
using NetPrints.Extensibility;

namespace Fx.Fail.Registration;

/// <summary>An extension whose property name depends on how often anything registered it in this process.</summary>
public sealed class FailRegistrationExtension : INetPrintsExtension
{
    private const string CounterKey = "fx.fail.registration.calls";

    /// <inheritdoc />
    public void Register(IExtensionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        int calls = AppDomain.CurrentDomain.GetData(CounterKey) is int previous ? previous + 1 : 1;
        AppDomain.CurrentDomain.SetData(CounterKey, calls);
        builder
            .AddNodeLibrary(new FxNodeLibrary("fx.fail.registration", FxKit.Kind("fx.fail.registration/Ping", () => "ping")))
            .AddJsonTypeInfoResolver(FxKitJsonContext.Default)
            .AddProjectProperty("FxFailRegistration" + calls.ToString(CultureInfo.InvariantCulture));
    }
}
