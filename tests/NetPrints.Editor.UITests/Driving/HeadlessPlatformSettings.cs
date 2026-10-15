using System.Reflection;
using Avalonia;
using Avalonia.Input;
using Avalonia.Platform;

namespace NetPrints.Editor.UITests;

/// <summary>
/// The headless platform's <see cref="IPlatformSettings"/> with a long double-tap time: headless input timestamps come from a
/// wall-clock stopwatch, so a slow first press (JIT, GC, a loaded runner) would otherwise turn a synthetic double click into
/// two single clicks. <see cref="IPlatformSettings"/> is not client-implementable, so a proxy forwards everything else.
/// </summary>
public class HeadlessPlatformSettings : DispatchProxy
{
    /// <summary>The double-tap time headless tests use, far above the platform's 500 ms.</summary>
    public static readonly TimeSpan DoubleTapTime = TimeSpan.FromSeconds(30);

    private IPlatformSettings? inner;

    /// <summary>Wraps <paramref name="settings"/> in a proxy that reports <see cref="DoubleTapTime"/>.</summary>
    public static IPlatformSettings Wrap(IPlatformSettings settings)
    {
        var proxy = Create<IPlatformSettings, HeadlessPlatformSettings>();
        ((HeadlessPlatformSettings)(object)proxy).inner = settings;
        return proxy;
    }

    /// <summary>
    /// Replaces the platform's settings of <paramref name="application"/> with <see cref="Wrap"/> of them. Avalonia's service
    /// locator is internal and has no other seam, so it is reached by reflection.
    /// </summary>
    /// <param name="application">The started test application.</param>
    public static void Install(Application? application)
    {
        IPlatformSettings settings = application?.PlatformSettings ?? throw new InvalidOperationException("No platform settings.");
        Type locator = typeof(Visual).Assembly.GetType("Avalonia.AvaloniaLocator", throwOnError: true)
            ?? throw new InvalidOperationException("AvaloniaLocator not found.");
        object mutable = locator.GetProperty("CurrentMutable", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
            ?? throw new InvalidOperationException("AvaloniaLocator.CurrentMutable not found.");
        object registration = locator.GetMethod("Bind")?.MakeGenericMethod(typeof(IPlatformSettings)).Invoke(mutable, null)
            ?? throw new InvalidOperationException("AvaloniaLocator.Bind not found.");
        registration.GetType().GetMethods().Single(m => m.Name == "ToConstant").MakeGenericMethod(typeof(IPlatformSettings))
            .Invoke(registration, [Wrap(settings)]);
    }

    /// <inheritdoc/>
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        targetMethod?.Name == nameof(IPlatformSettings.GetDoubleTapTime)
            ? DoubleTapTime
            : targetMethod?.Invoke(inner, args);
}
