using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging;

namespace NetPrints.Editor.Hosting;

/// <summary>Finds the assembly the loader loaded for an extension.</summary>
internal static class ExtensionAssemblies
{
    /// <summary>Finds the loaded assembly named by an extension's manifest, comparing the full paths.</summary>
    /// <param name="manifestPath">Path of the manifest; <see langword="null"/> for an in-process extension.</param>
    /// <param name="assemblyFile">The manifest's <c>assembly</c>, relative to the manifest; it may hold dot segments.</param>
    /// <param name="extensionId">The extension id, named in the warning for a miss.</param>
    /// <param name="logger">Receives the warning (1045) for a miss.</param>
    /// <returns>The assembly, or <see langword="null"/> when none is loaded from that path.</returns>
    public static Assembly? Find(string? manifestPath, string assemblyFile, string extensionId, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        if (manifestPath is null || Path.GetDirectoryName(Path.GetFullPath(manifestPath)) is not { } folder)
        {
            return null;
        }

        string path = Path.GetFullPath(assemblyFile, folder);
        Assembly? found = AssemblyLoadContext.All
            .SelectMany(context => context.Assemblies)
            .FirstOrDefault(assembly => !assembly.IsDynamic && assembly.Location.Length > 0 && ExtensionPaths.Same(assembly.Location, path));
        if (found is null)
        {
            Log.ExtensionAssemblyNotFound(logger, extensionId, path);
        }

        return found;
    }
}
