using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace NetPrints.Workspace.Templates;

/// <summary>
/// The file trees of the built-in project templates, embedded from <c>Templates/&lt;name&gt;/</c>. The project file itself comes from the project profile,
/// so a tree holds only the files written next to it. The editor and <c>netprints new</c> read the same trees.
/// </summary>
public static class BuiltInTemplateTrees
{
    private const string Root = "Templates/";

    /// <summary>The name of the Console app tree.</summary>
    public const string Console = "console";

    /// <summary>The name of the Class library tree; it has no files besides the project file.</summary>
    public const string Library = "library";

    /// <summary>Reads the files of a built-in tree, with LF line endings, in path order.</summary>
    /// <param name="name">The tree name: <see cref="Console"/> or <see cref="Library"/>.</param>
    /// <returns>The path relative to the project folder (with <c>/</c> separators) and the text of each file; empty for a tree with no files.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
    public static IReadOnlyList<TemplateTreeFile> Read(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Assembly assembly = typeof(BuiltInTemplateTrees).Assembly;
        string prefix = Root + name + "/";
        List<TemplateTreeFile> files = [];
        foreach (string resource in assembly.GetManifestResourceNames().Order(StringComparer.Ordinal))
        {
            string path = resource.Replace('\\', '/');
            if (!path.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            using Stream stream = assembly.GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException($"The embedded template file '{resource}' could not be opened.");
            using var reader = new StreamReader(stream);
            files.Add(new TemplateTreeFile(path[prefix.Length..], reader.ReadToEnd().ReplaceLineEndings("\n")));
        }

        return files;
    }
}

/// <summary>A file of a built-in template tree.</summary>
/// <param name="Path">The path relative to the project folder, with <c>/</c> separators.</param>
/// <param name="Content">The text of the file.</param>
public sealed record TemplateTreeFile(string Path, string Content);
