using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Extensibility.Loading;

namespace NetPrints.Editor.Hosting;

/// <summary>A loaded extension, as the error dialog names it.</summary>
/// <param name="Id">The extension id.</param>
/// <param name="Name">The display name.</param>
/// <param name="AssemblyName">The simple name of the extension's main assembly.</param>
/// <param name="Folder">The folder that holds the extension's manifest.</param>
/// <param name="Context">The load context of the extension; every assembly loaded in it, when the exception is attributed, belongs to the extension. <see langword="null"/> when only the main assembly counts.</param>
internal sealed record ExtensionOrigin(string Id, string Name, string AssemblyName, string Folder, AssemblyLoadContext? Context = null);

/// <summary>
/// Finds the extension whose code threw an exception, by the assembly of the stack frames (FR-034): the first frame whose
/// assembly is an extension's main assembly decides. When there is none, the first frame whose assembly exactly one
/// extension's load context holds decides, and a library that several extensions hold is skipped.
/// </summary>
/// <param name="origins">The loaded extensions now; the first listed of two sharing a main assembly name wins, so list the copy serving the session first.</param>
internal sealed class ExtensionAttribution(Func<IReadOnlyList<ExtensionOrigin>> origins)
{
    /// <summary>Gets the attribution for the extensions the host has loaded now.</summary>
    /// <param name="host">The extension host whose current registry names the loaded extensions.</param>
    /// <returns>The attribution.</returns>
    public static ExtensionAttribution For(IExtensionHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        return new ExtensionAttribution(() =>
            [
                .. host.Current.Results.OfType<ExtensionLoadResult.Loaded>()
                    .Select(result => (Result: result, Main: ExtensionAssemblies.Find(result.ManifestPath, result.Manifest.Assembly, result.Id, NullLogger.Instance)))
                    .Where(found => found.Main?.GetName().Name is not null)
                    .Select(found => new ExtensionOrigin(
                        found.Result.Id,
                        found.Result.Manifest.Name,
                        found.Main?.GetName().Name ?? string.Empty,
                        Path.GetDirectoryName(found.Result.ManifestPath ?? string.Empty) ?? string.Empty,
                        OwnContext(found.Main))),
            ]);
    }

    /// <summary>Finds the extension that threw <paramref name="exception"/>.</summary>
    /// <param name="exception">The exception.</param>
    /// <returns>The extension, or <see langword="null"/> when no frame belongs to a loaded extension.</returns>
    public ExtensionOrigin? Find(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        IReadOnlyList<ExtensionOrigin> known = origins();
        if (known.Count == 0)
        {
            return null;
        }

        List<string> frames = [];
        foreach (Exception candidate in Visit(exception))
        {
            foreach (StackFrame frame in new StackTrace(candidate, false).GetFrames())
            {
                string? assembly = DiagnosticMethodInfo.Create(frame)?.DeclaringAssemblyName;
                if (assembly is null)
                {
                    continue;
                }

                int comma = assembly.IndexOf(',', StringComparison.Ordinal);
                frames.Add((comma < 0 ? assembly : assembly[..comma]).Trim());
            }
        }

        return Decide(frames, known, Holds);
    }

    /// <summary>Decides which loaded extension, if any, the frames belong to.</summary>
    /// <param name="frames">The simple assembly names, innermost frame first, exception by exception.</param>
    /// <param name="known">The loaded extensions, in the order they are listed.</param>
    /// <param name="holds">Says whether an extension's load context holds an assembly with the simple name.</param>
    /// <returns>The extension, or <see langword="null"/> when no frame belongs to one.</returns>
    internal static ExtensionOrigin? Decide(IReadOnlyList<string> frames, IReadOnlyList<ExtensionOrigin> known, Func<ExtensionOrigin, string, bool> holds)
    {
        foreach (string frame in frames)
        {
            ExtensionOrigin? origin = known.FirstOrDefault(candidate => string.Equals(candidate.AssemblyName, frame, StringComparison.Ordinal));
            if (origin is not null)
            {
                return origin;
            }
        }

        foreach (string frame in frames)
        {
            ExtensionOrigin? holder = null;
            int holders = 0;
            foreach (ExtensionOrigin candidate in known)
            {
                if (!holds(candidate, frame))
                {
                    continue;
                }

                holder = candidate;
                holders++;
            }

            if (holders == 1)
            {
                return holder;
            }
        }

        return null;
    }

    /// <summary>Builds the text of the error dialog for an exception an extension threw.</summary>
    /// <param name="origin">The extension.</param>
    /// <param name="exception">The exception.</param>
    /// <returns>The text: who threw, its folder, what the user can do, then the exception.</returns>
    public static string Describe(ExtensionOrigin origin, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(exception);
        return TextFormat.Of(Strings.Message_ExtensionError, origin.Name, origin.Id, origin.Folder, exception.ToString());
    }

    private static IEnumerable<Exception> Visit(Exception exception)
    {
        yield return exception;
        if (exception is AggregateException aggregate)
        {
            foreach (Exception inner in aggregate.InnerExceptions.SelectMany(Visit))
            {
                yield return inner;
            }
        }
        else if (exception.InnerException is { } next)
        {
            foreach (Exception inner in Visit(next))
            {
                yield return inner;
            }
        }
    }

    private static AssemblyLoadContext? OwnContext(Assembly? main) =>
        main is not null && AssemblyLoadContext.GetLoadContext(main) is { } context && context != AssemblyLoadContext.Default ? context : null;

    private static bool Holds(ExtensionOrigin origin, string simpleName) =>
        origin.Context?.Assemblies.Any(assembly => string.Equals(assembly.GetName().Name, simpleName, StringComparison.Ordinal)) ?? false;
}
