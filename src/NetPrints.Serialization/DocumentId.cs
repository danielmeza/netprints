#nullable enable
using System;

namespace NetPrints.Serialization;

/// <summary>
/// Relative, '/'-separated path identifying a document inside an <c>IDocumentStore</c>
/// (document-format.md §2.1). Ordinal equality; case is preserved but not normalized.
/// </summary>
public readonly record struct DocumentId
{
    /// <summary>
    /// Creates a document id from a relative path.
    /// </summary>
    /// <param name="path">Relative, '/'-separated path.</param>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty, rooted (starts with
    /// '/' or a drive letter), contains a backslash, or has a <c>".."</c> or <c>"."</c> segment.</exception>
    public DocumentId(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            throw new ArgumentException("A document id path must not be empty.", nameof(path));
        }

        if (path.Contains('\\'))
        {
            throw new ArgumentException($"Document id path '{path}' must not contain a backslash.", nameof(path));
        }

        if (path.StartsWith('/') || System.IO.Path.IsPathRooted(path))
        {
            throw new ArgumentException($"Document id path '{path}' must be relative.", nameof(path));
        }

        foreach (string segment in path.Split('/'))
        {
            if (segment is "." or "..")
            {
                throw new ArgumentException($"Document id path '{path}' must not contain a '{segment}' segment.", nameof(path));
            }
        }

        Path = path;
    }

    /// <summary>
    /// The relative, '/'-separated path.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// The last '/'-separated segment of <see cref="Path"/>.
    /// </summary>
    public string FileName => Path[(Path.LastIndexOf('/') + 1)..];

    /// <summary>
    /// Returns the id of another file named <paramref name="fileName"/> in the same directory as
    /// this one.
    /// </summary>
    /// <param name="fileName">File name (no directory separators) of the sibling document.</param>
    /// <returns>The sibling document's id.</returns>
    public DocumentId Sibling(string fileName)
    {
        int slash = Path.LastIndexOf('/');
        return new DocumentId(slash < 0 ? fileName : $"{Path[..slash]}/{fileName}");
    }

    /// <summary>
    /// Returns <see cref="Path"/>.
    /// </summary>
    /// <returns><see cref="Path"/>.</returns>
    public override string ToString() => Path;
}
