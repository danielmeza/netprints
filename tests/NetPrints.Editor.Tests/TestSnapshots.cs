using System.Reflection;
using System.Runtime.InteropServices;
using NetPrints.Core;
using NetPrints.Projects;

namespace NetPrints.Editor.Tests;

/// <summary>Builds minimal <see cref="ProjectSnapshot"/>s for tests that need one without a real project system.</summary>
public static class TestSnapshots
{
    /// <summary>An otherwise-empty snapshot with no references or graph files.</summary>
    public static ProjectSnapshot Empty(string name, string rootNamespace, IReadOnlyList<ResolvedAssembly>? references = null) =>
        new(Path.Combine(Path.GetTempPath(), $"{name}.csproj"), name, rootNamespace, name, BinaryType.SharedLibrary,
            "net10.0", DefaultProjectProfile.ProfileId, ReferencesNetPrintsSdk: true,
            GraphFiles: [], ExtensionFolders: [], References: references ?? [], DeclaredReferences: [],
            OtherSources: [], CompilationOptionsJson: "{}", Properties: new Dictionary<string, string>(), Messages: []);

    /// <summary>
    /// A snapshot whose <see cref="ProjectSnapshot.References"/> are the running runtime's own
    /// assemblies (<see cref="RuntimeAssemblyPaths"/>), so a reflection
    /// provider built from it sees the whole BCL, like a real project's MSBuild-resolved references.
    /// </summary>
    public static ProjectSnapshot WithRuntimeAssemblies(string name, string rootNamespace) =>
        Empty(name, rootNamespace, RuntimeAssemblyPaths()
            .Select(path => new ResolvedAssembly(path, null)).ToList());

    /// <summary>Paths of the managed assemblies of the running .NET runtime, in path order.</summary>
    public static IReadOnlyList<string> RuntimeAssemblyPaths() => runtimeAssemblyPaths.Value;

    private static readonly Lazy<IReadOnlyList<string>> runtimeAssemblyPaths = new(() =>
        Directory.EnumerateFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll")
            .Where(IsManagedAssembly)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList());

    private static bool IsManagedAssembly(string path)
    {
        try
        {
            AssemblyName.GetAssemblyName(path);
            return true;
        }
        catch (BadImageFormatException)
        {
            // Native images throw BadImageFormatException.
            return false;
        }
    }
}
