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

/// <summary>Who holds one assembly identity in the process.</summary>
/// <param name="Listed">The listed extensions whose load context holds it, in list order.</param>
/// <param name="Editor">Whether the editor's own load context holds it too, and no listed extension without a context of its own claims it.</param>
/// <param name="Unknown">Whether another load context that belongs to no listed extension holds it too.</param>
internal sealed record AssemblyHolders(IReadOnlyList<ExtensionOrigin> Listed, bool Editor, bool Unknown);

/// <summary>
/// Finds the extension whose code threw an exception, by the full assembly name of the stack frames (FR-034). The main-assembly
/// frame nearest the throw decides: it names its extension only when every context holding that identity belongs to listed copies
/// of one extension, apart from an earlier copy of that extension's own version, and otherwise names none. When no main-assembly
/// frame is on the stack, the library frames decide: each must be held by one extension only, all by the same one, and an
/// uncertain frame names none. Naming an innocent extension is worse than naming none, because the user then removes the wrong one.
/// </summary>
/// <param name="origins">The loaded extensions now, in the order the session lists them.</param>
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
    /// <returns>The extension, or <see langword="null"/> when no frame belongs to a loaded extension or the stack does not identify one extension for certain.</returns>
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
                if (DiagnosticMethodInfo.Create(frame)?.DeclaringAssemblyName is { } assembly && Identity(assembly) is { } identity)
                {
                    frames.Add(identity);
                }
            }
        }

        return Decide(frames, Holders(known));
    }

    /// <summary>Decides which loaded extension, if any, the frames belong to.</summary>
    /// <param name="frames">The normalized assembly identities, innermost frame first, exception by exception.</param>
    /// <param name="holders">Gives who holds one assembly identity in the process.</param>
    /// <returns>The extension, or <see langword="null"/> when no frame belongs to a loaded extension or the stack does not identify one extension for certain.</returns>
    internal static ExtensionOrigin? Decide(IReadOnlyList<string> frames, Func<string, AssemblyHolders> holders)
    {
        foreach (string frame in frames)
        {
            string simple = Simple(frame);
            AssemblyHolders found = holders(frame);
            ExtensionOrigin[] candidates = [.. found.Listed.Where(origin => string.Equals(origin.AssemblyName, simple, StringComparison.Ordinal))];
            if (candidates.Length == 0)
            {
                continue;
            }

            return IsCertain(found) ? candidates[0] : null;
        }

        ExtensionOrigin? counted = null;
        foreach (string frame in frames)
        {
            AssemblyHolders found = holders(frame);
            if (found.Listed.Count == 0 && !found.Unknown)
            {
                continue;
            }

            if (!IsCertain(found))
            {
                return null;
            }

            if (counted is not null && !string.Equals(counted.Id, found.Listed[0].Id, StringComparison.Ordinal))
            {
                return null;
            }

            counted ??= found.Listed[0];
        }

        return counted;
    }

    private static bool IsCertain(AssemblyHolders found) =>
        found.Listed.Count > 0
        && !found.Editor
        && !found.Unknown
        && found.Listed.All(origin => string.Equals(origin.Id, found.Listed[0].Id, StringComparison.Ordinal));

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

    private static string? Identity(string assemblyName)
    {
        try
        {
            return new AssemblyName(assemblyName).FullName;
        }
        catch (Exception failure) when (failure is ArgumentException or FileLoadException)
        {
            return null;
        }
    }

    private static string Simple(string identity) => new AssemblyName(identity).Name ?? string.Empty;

    private static Func<string, AssemblyHolders> Holders(IReadOnlyList<ExtensionOrigin> known)
    {
        Dictionary<string, List<AssemblyLoadContext>> held = new(StringComparer.Ordinal);
        foreach (AssemblyLoadContext context in AssemblyLoadContext.All)
        {
            foreach (Assembly assembly in context.Assemblies)
            {
                if (assembly.IsDynamic || Identity(assembly.FullName ?? string.Empty) is not { } identity)
                {
                    continue;
                }

                if (!held.TryGetValue(identity, out List<AssemblyLoadContext>? contexts))
                {
                    held[identity] = contexts = [];
                }

                if (!contexts.Contains(context))
                {
                    contexts.Add(context);
                }
            }
        }

        return identity =>
        {
            IReadOnlyList<AssemblyLoadContext> contexts = held.TryGetValue(identity, out List<AssemblyLoadContext>? found) ? found : [];
            string simple = Simple(identity);
            List<ExtensionOrigin> listed = [.. known.Where(origin => Holds(origin, contexts, simple))];
            List<AssemblyLoadContext> others = [.. contexts.Where(context => !listed.Any(origin => Owns(origin, context, simple)))];
            return new AssemblyHolders(listed, others.Contains(AssemblyLoadContext.Default), others.Any(context => context != AssemblyLoadContext.Default && !IsRetiredCopy(context, listed)));
        };
    }

    private static bool IsRetiredCopy(AssemblyLoadContext context, IReadOnlyList<ExtensionOrigin> listed) =>
        context.Name is { } name && listed.Any(origin => origin.Context is not null && string.Equals(origin.Id, name, StringComparison.Ordinal));

    private static bool Holds(ExtensionOrigin origin, IReadOnlyList<AssemblyLoadContext> contexts, string simple) =>
        origin.Context is null
            ? contexts.Contains(AssemblyLoadContext.Default) && string.Equals(origin.AssemblyName, simple, StringComparison.Ordinal)
            : contexts.Contains(origin.Context);

    private static bool Owns(ExtensionOrigin origin, AssemblyLoadContext context, string simple) =>
        origin.Context is null
            ? context == AssemblyLoadContext.Default && string.Equals(origin.AssemblyName, simple, StringComparison.Ordinal)
            : ReferenceEquals(origin.Context, context);
}
