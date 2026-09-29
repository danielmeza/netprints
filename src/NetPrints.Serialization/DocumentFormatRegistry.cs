#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace NetPrints.Serialization;

/// <summary>
/// Immutable set of registered <see cref="IDocumentFormat"/>s, resolved by a document id's file
/// extension (document-format.md §2.2). Thread-safe: both the registry and every registered format are
/// immutable.
/// </summary>
public sealed class DocumentFormatRegistry
{
    private readonly IReadOnlyList<IDocumentFormat> formats;

    /// <summary>
    /// Creates a registry from <paramref name="formats"/>.
    /// </summary>
    /// <param name="formats">Formats to register.</param>
    /// <exception cref="ArgumentException">Two formats share an <see cref="IDocumentFormat.Id"/>, two
    /// formats claim the same class extension, or no format (or more than one) has
    /// <see cref="IDocumentFormat.Id"/> <c>"json"</c>.</exception>
    public DocumentFormatRegistry(IReadOnlyList<IDocumentFormat> formats)
    {
        ArgumentNullException.ThrowIfNull(formats);

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var seenExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (IDocumentFormat format in formats)
        {
            if (!seenIds.Add(format.Id))
            {
                throw new ArgumentException($"Duplicate document format id '{format.Id}'.", nameof(formats));
            }

            foreach (string extension in format.ClassExtensions)
            {
                if (!seenExtensions.Add(extension))
                {
                    throw new ArgumentException($"Extension '{extension}' is claimed by more than one document format.", nameof(formats));
                }
            }
        }

        IDocumentFormat[] jsonFormats = formats.Where(f => f.Id == "json").ToArray();
        if (jsonFormats.Length != 1)
        {
            throw new ArgumentException("Exactly one document format with Id 'json' must be registered.", nameof(formats));
        }

        this.formats = formats;
        Default = jsonFormats[0];
    }

    /// <summary>Every registered format, in registration order.</summary>
    public IReadOnlyList<IDocumentFormat> Formats => formats;

    /// <summary>The registered format with <see cref="IDocumentFormat.Id"/> <c>"json"</c>, used to write
    /// new documents.</summary>
    public IDocumentFormat Default { get; }

    /// <summary>
    /// Finds the format whose <see cref="IDocumentFormat.ClassExtensions"/> contains the longest suffix
    /// of <paramref name="id"/>'s file name (<c>OrdinalIgnoreCase</c>), so a more specific extension
    /// (<c>.netpc.json</c>) is preferred over a shorter one that is also a suffix of it (<c>.netpc</c>
    /// is not, but a hypothetical shared suffix would be).
    /// </summary>
    /// <param name="id">Document id to resolve a format for.</param>
    /// <param name="kind">Kind of document <paramref name="id"/> refers to. Only <see cref="DocumentKind.Class"/>
    /// documents are resolved through this registry; project documents are handled by <c>IProjectSystem</c>
    /// instead (project-system.md).</param>
    /// <returns>The best-matching format, or <see langword="null"/> if none of the registered formats'
    /// extensions match.</returns>
    public IDocumentFormat? Find(DocumentId id, DocumentKind kind)
    {
        if (kind != DocumentKind.Class)
        {
            return null;
        }

        string fileName = id.FileName;

        IDocumentFormat? best = null;
        int bestLength = -1;

        foreach (IDocumentFormat format in formats)
        {
            foreach (string extension in format.ClassExtensions)
            {
                if (extension.Length > bestLength && fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                {
                    best = format;
                    bestLength = extension.Length;
                }
            }
        }

        return best;
    }
}
