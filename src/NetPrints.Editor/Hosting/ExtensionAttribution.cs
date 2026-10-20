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
/// <param name="ProjectCopy"><see langword="true"/> when the extension was loaded from one of the open project's extension folders, which the project's build needs.</param>
/// <param name="Context">The load context of the extension; every assembly loaded in it, when the exception is attributed, belongs to the extension. <see langword="null"/> when only the main assembly counts.</param>
internal sealed record ExtensionOrigin(string Id, string Name, string AssemblyName, string Folder, bool ProjectCopy = false, AssemblyLoadContext? Context = null);

/// <summary>
/// Finds the extension whose code threw an exception, by the assembly of the stack frames (FR-034): the first frame whose
/// assembly is an extension's main assembly decides. When there is none, a frame counts only when its library is held by
/// exactly one load context in the whole process and that context is a listed extension's; the counting frames must all name
/// the same extension, else none is named. Naming an innocent extension is worse than naming none, because the user then removes the wrong one.
/// </summary>
/// <param name="origins">The loaded extensions now; the first listed of two sharing a main assembly name wins, so list the copy serving the session first.</param>
internal sealed class ExtensionAttribution(Func<IReadOnlyList<ExtensionOrigin>> origins)
{
    /// <summary>Gets the attribution for the extensions the host has loaded now.</summary>
    /// <param name="host">The extension host whose current registry names the loaded extensions.</param>
    /// <param name="projectFolders">Gives the extension folders of the open project, read each time the attribution is built; an extension loaded from one of them is the project's own copy.</param>
    /// <returns>The attribution.</returns>
    public static ExtensionAttribution For(IExtensionHost host, Func<IReadOnlyList<string>> projectFolders)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(projectFolders);
        return new ExtensionAttribution(() =>
        {
            HashSet<string> project = new(projectFolders().Select(ExtensionPaths.Normalize), ExtensionPaths.Comparer);
            return
            [
                .. host.Current.Results.OfType<ExtensionLoadResult.Loaded>()
                    .Select(result => (Result: result, Main: ExtensionAssemblies.Find(result.ManifestPath, result.Manifest.Assembly, result.Id, NullLogger.Instance)))
                    .Where(found => found.Main?.GetName().Name is not null)
                    .Select(found => (found.Result, found.Main, Folder: Path.GetDirectoryName(found.Result.ManifestPath ?? string.Empty) ?? string.Empty))
                    .Select(found => new ExtensionOrigin(
                        found.Result.Id,
                        found.Result.Manifest.Name,
                        found.Main?.GetName().Name ?? string.Empty,
                        found.Folder,
                        found.Folder.Length > 0 && project.Contains(ExtensionPaths.Normalize(found.Folder)),
                        OwnContext(found.Main))),
            ];
        });
    }

    /// <summary>Gets the loaded extensions as the attribution sees them now.</summary>
    /// <returns>The extensions, in the order they are listed.</returns>
    internal IReadOnlyList<ExtensionOrigin> Origins() => origins();

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

        return Decide(frames, known, SoleHolders(known));
    }

    /// <summary>Decides which loaded extension, if any, the frames belong to.</summary>
    /// <param name="frames">The simple assembly names, innermost frame first, exception by exception.</param>
    /// <param name="known">The loaded extensions, in the order they are listed.</param>
    /// <param name="soleHolder">Returns, for a simple assembly name, the listed extension whose load context is the only one in the process holding it, or <see langword="null"/>.</param>
    /// <returns>The extension, or <see langword="null"/> when no frame belongs to one.</returns>
    internal static ExtensionOrigin? Decide(IReadOnlyList<string> frames, IReadOnlyList<ExtensionOrigin> known, Func<string, ExtensionOrigin?> soleHolder)
    {
        foreach (string frame in frames)
        {
            ExtensionOrigin? origin = known.FirstOrDefault(candidate => string.Equals(candidate.AssemblyName, frame, StringComparison.Ordinal));
            if (origin is not null)
            {
                return origin;
            }
        }

        ExtensionOrigin? counted = null;
        foreach (string frame in frames)
        {
            ExtensionOrigin? holder = soleHolder(frame);
            if (holder is null)
            {
                continue;
            }

            if (counted is not null && !ReferenceEquals(counted, holder))
            {
                return null;
            }

            counted = holder;
        }

        return counted;
    }

    /// <summary>Builds the text of the error dialog for an exception an extension threw.</summary>
    /// <param name="origin">The extension.</param>
    /// <param name="exception">The exception.</param>
    /// <returns>The text: who threw, its folder, what the user can do, then the exception.</returns>
    public static string Describe(ExtensionOrigin origin, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(exception);
        string template = origin.ProjectCopy ? Strings.Message_ExtensionErrorProjectCopy : Strings.Message_ExtensionError;
        return TextFormat.Of(template, origin.Name, origin.Id, origin.Folder, exception.ToString());
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

    private static Func<string, ExtensionOrigin?> SoleHolders(IReadOnlyList<ExtensionOrigin> known)
    {
        Dictionary<string, AssemblyLoadContext?> holders = new(StringComparer.Ordinal);
        foreach (AssemblyLoadContext context in AssemblyLoadContext.All)
        {
            foreach (string name in context.Assemblies.Select(assembly => assembly.GetName().Name).OfType<string>().Distinct(StringComparer.Ordinal))
            {
                holders[name] = holders.ContainsKey(name) ? null : context;
            }
        }

        return name => holders.TryGetValue(name, out AssemblyLoadContext? only) && only is not null
            ? known.FirstOrDefault(origin => ReferenceEquals(origin.Context, only))
            : null;
    }
}
