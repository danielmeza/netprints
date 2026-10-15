#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.CodeAnalysis;
using NetPrints.Projects;

namespace NetPrints.Compilation;

/// <summary>
/// One Roslyn <see cref="MetadataReference"/> per assembly file, shared by every compilation that references
/// it. Each reference reads its assembly image into native memory the first time a compilation uses it, so a
/// reference per session or provider repeats that for every runtime assembly; a shared one reads it once.
/// </summary>
public static class SharedMetadataReferences
{
    private const int PruneStep = 512;

    private static readonly Lock Gate = new();
    private static readonly Dictionary<ReferenceKey, WeakReference<MetadataReference>> References = [];
    private static int pruneAt = PruneStep;

    /// <summary>
    /// Returns the reference for <paramref name="assembly"/>: the one already alive for the same file, documentation
    /// file and last write, else a new one. A reference nothing uses any more is collected.
    /// </summary>
    /// <param name="assembly">The assembly, which must exist.</param>
    /// <returns>A reference whose documentation comes from <see cref="ResolvedAssembly.DocumentationPath"/>, if that file exists.</returns>
    /// <exception cref="FileNotFoundException"><paramref name="assembly"/>'s file does not exist.</exception>
    public static MetadataReference For(ResolvedAssembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var key = new ReferenceKey(assembly.Path, assembly.DocumentationPath, File.GetLastWriteTimeUtc(assembly.Path).Ticks);

        lock (Gate)
        {
            if (References.TryGetValue(key, out WeakReference<MetadataReference>? existing) && existing.TryGetTarget(out MetadataReference? alive))
            {
                return alive;
            }

            MetadataReference created = Create(assembly);
            References[key] = new WeakReference<MetadataReference>(created);
            if (References.Count >= pruneAt)
            {
                Prune();
                pruneAt = References.Count + PruneStep;
            }

            return created;
        }
    }

    private static MetadataReference Create(ResolvedAssembly assembly)
    {
        DocumentationProvider? documentation = assembly.DocumentationPath is { } path && File.Exists(path)
            ? XmlDocumentationProvider.CreateFromFile(path)
            : null;

        return MetadataReference.CreateFromFile(assembly.Path, documentation: documentation);
    }

    private static void Prune()
    {
        List<ReferenceKey> dead = [];
        foreach ((ReferenceKey key, WeakReference<MetadataReference> reference) in References)
        {
            if (!reference.TryGetTarget(out _))
            {
                dead.Add(key);
            }
        }

        foreach (ReferenceKey key in dead)
        {
            References.Remove(key);
        }
    }

    private readonly record struct ReferenceKey(string Path, string? DocumentationPath, long LastWriteTicks);
}
