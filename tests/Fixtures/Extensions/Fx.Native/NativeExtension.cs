using System.Runtime.InteropServices;
using NetPrints.Extensibility;

namespace Fx.Native;

/// <summary>Calls into a native library shipped in its own folder.</summary>
public sealed class NativeExtension : INetPrintsExtension
{
    /// <summary>The extension id, also the manifest id.</summary>
    public const string Id = "fx.native";

    /// <summary>The value the native call returned in <see cref="Register"/>; zero before it ran.</summary>
    public static int Milestone { get; private set; }

    /// <inheritdoc />
    public void Register(IExtensionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        Milestone = sk_version_get_milestone();
    }

    [DllImport("libSkiaSharp")]
    private static extern int sk_version_get_milestone();
}
