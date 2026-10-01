using System.Collections.Generic;
using System.IO;
using NetPrints.Testing;

namespace NetPrints.Tests.Extensibility.MultiExtension;

/// <summary>Where the fixture extensions of <c>tests/Fixtures/Extensions</c> (contracts/extensions.md §3) are built, and a way to copy them.</summary>
internal static class FixtureExtensions
{
    public const string Alpha = "fx.alpha";
    public const string Beta = "fx.beta";
    public const string LibV1 = "fx.libv1";
    public const string LibV2 = "fx.libv2";
    public const string PrefixedPrivate = "fx.private-prefix";
    public const string TypesProvider = "fx.types-provider";
    public const string TypesConsumer = "fx.types-consumer";
    public const string Diamond = "fx.diamond";
    public const string Native = "fx.native";

    private static readonly Dictionary<string, string> ProjectFolders = new(System.StringComparer.Ordinal)
    {
        [Alpha] = "Fx.Alpha",
        [Beta] = "Fx.Beta",
        [LibV1] = "Fx.LibV1",
        [LibV2] = "Fx.LibV2",
        [PrefixedPrivate] = "Fx.PrefixedPrivate",
        [TypesProvider] = "Fx.TypesProvider",
        [TypesConsumer] = "Fx.TypesConsumer",
        [Diamond] = "Fx.Diamond",
        [Native] = "Fx.Native",
    };

    /// <summary>The build output folder of the fixture with manifest id <paramref name="id"/>, in the configuration of the running tests.</summary>
    public static string Folder(string id) => Path.Combine(
        LocalSdkLayout.FindRepositoryRoot(), "tests", "Fixtures", "Extensions", ProjectFolders[id], "bin", LocalSdkLayout.DetectConfiguration(), "extensions", id);

    /// <summary>Copies the built fixture <paramref name="id"/>, subfolders included, to <c>&lt;destinationRoot&gt;/&lt;folderName&gt;</c>.</summary>
    /// <param name="destinationRoot">The directory that will hold the copy, for use as a search directory.</param>
    /// <param name="id">The fixture's manifest id.</param>
    /// <param name="folderName">The name of the copied folder; defaults to the id.</param>
    /// <returns>The path of the copy.</returns>
    public static string CopyTo(string destinationRoot, string id, string? folderName = null)
    {
        string source = Folder(id);
        string destination = Path.Combine(destinationRoot, folderName ?? id);
        foreach (string directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }

        Directory.CreateDirectory(destination);
        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, Path.Combine(destination, Path.GetRelativePath(source, file)), overwrite: true);
        }

        return destination;
    }
}
