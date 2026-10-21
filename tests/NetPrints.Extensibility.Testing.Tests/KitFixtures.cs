using Fx.Kit;
using NetPrints.Extensibility;
using NetPrints.Testing;

namespace NetPrints.Extensibility.Testing.Tests;

/// <summary>Where the built fixture extensions are, and the minimal extension the kit tests drive in-process.</summary>
internal static class KitFixtures
{
    public const string Alpha = "fx.alpha";

    public const string AlphaKind = "fx.alpha/Ping";

    public static string AlphaFolder() => Folder("Fx.Alpha", Alpha, conformance: false);

    public static string FailFolder(string project, string id) => Folder(project, id, conformance: true);

    private static string Folder(string project, string id, bool conformance)
    {
        string extensions = Path.Combine(LocalSdkLayout.FindRepositoryRoot(), "tests", "Fixtures", "Extensions");
        string parent = conformance ? Path.Combine(extensions, "Conformance") : extensions;
        return Path.Combine(parent, project, "bin", LocalSdkLayout.DetectConfiguration(), "extensions", id);
    }
}

/// <summary>A minimal real extension: one node kind that writes a line.</summary>
public sealed class MinimalExtension : INetPrintsExtension
{
    /// <summary>The id the tests load the extension under.</summary>
    public const string Id = "test.minimal";

    /// <summary>The node kind.</summary>
    public const string PingKind = "test.minimal/Ping";

    /// <summary>The text the translator writes.</summary>
    public const string PingText = "minimal ping";

    /// <inheritdoc />
    public void Register(IExtensionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder
            .AddNodeLibrary(new FxNodeLibrary(Id, FxKit.Kind(PingKind, () => PingText)))
            .AddJsonTypeInfoResolver(FxKitJsonContext.Default);
    }
}
