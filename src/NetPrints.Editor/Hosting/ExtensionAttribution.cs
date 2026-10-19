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
/// <param name="Context">The load context of the extension; every assembly loaded in it, when the exception is attributed, belongs to the extension. <see langword="null"/> when only the main assembly counts.</param>
internal sealed record ExtensionOrigin(string Id, string Name, string AssemblyName, AssemblyLoadContext? Context = null);

/// <summary>
/// Finds the extension whose code threw an exception, by the assembly of the stack frames (FR-034): the first frame whose
/// assembly belongs to a loaded extension decides. The outer exception is checked first, then the inner ones in order.
/// </summary>
/// <param name="origins">The loaded extensions now; the first of two sharing an assembly name wins, so list the copy serving the session first.</param>
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
                    .Select(found => new ExtensionOrigin(found.Result.Id, found.Result.Manifest.Name, found.Main?.GetName().Name ?? string.Empty, OwnContext(found.Main))),
            ]);
    }

    /// <summary>Finds the extension that threw <paramref name="exception"/>.</summary>
    /// <param name="exception">The exception.</param>
    /// <returns>The extension, or <see langword="null"/> when no frame belongs to a loaded extension.</returns>
    public ExtensionOrigin? Find(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        IReadOnlyList<ExtensionOrigin> known = origins();
        return known.Count == 0 ? null : Visit(exception).Select(candidate => Match(candidate, known)).FirstOrDefault(origin => origin is not null);
    }

    /// <summary>Builds the text of the error dialog for an exception an extension threw.</summary>
    /// <param name="origin">The extension.</param>
    /// <param name="exception">The exception.</param>
    /// <returns>The text: who threw, what the user can do, then the exception.</returns>
    public static string Describe(ExtensionOrigin origin, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(exception);
        return TextFormat.Of(Strings.Message_ExtensionError, origin.Name, origin.Id, exception.ToString());
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

    private static bool Owns(ExtensionOrigin origin, string simpleName) =>
        string.Equals(origin.AssemblyName, simpleName, StringComparison.Ordinal)
        || (origin.Context?.Assemblies.Any(assembly => string.Equals(assembly.GetName().Name, simpleName, StringComparison.Ordinal)) ?? false);

    private static ExtensionOrigin? Match(Exception exception, IReadOnlyList<ExtensionOrigin> known)
    {
        foreach (StackFrame frame in new StackTrace(exception, false).GetFrames())
        {
            string? assembly = DiagnosticMethodInfo.Create(frame)?.DeclaringAssemblyName;
            if (assembly is null)
            {
                continue;
            }

            int comma = assembly.IndexOf(',', StringComparison.Ordinal);
            string simple = (comma < 0 ? assembly : assembly[..comma]).Trim();
            ExtensionOrigin? origin = known.FirstOrDefault(candidate => Owns(candidate, simple));
            if (origin is not null)
            {
                return origin;
            }
        }

        return null;
    }
}
