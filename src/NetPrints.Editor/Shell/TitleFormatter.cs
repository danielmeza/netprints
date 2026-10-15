namespace NetPrints.Editor.Shell;

/// <summary>Formats the shell window title (contracts/shell.md section 4).</summary>
public static class TitleFormatter
{
    /// <summary>The product name, the whole title with no project open.</summary>
    public const string ProductName = "NetPrints";

    private const string Separator = " – ";
    private const string UnsavedMark = "*";

    /// <summary>Builds the title: <c>"&lt;document&gt; – &lt;project&gt;[*] – NetPrints"</c>, or just the product name with no project.</summary>
    /// <param name="projectName">The open project's name, or null with no project open.</param>
    /// <param name="documentTitle">The active document's title, or null or empty to leave it out.</param>
    /// <param name="hasUnsavedFiles">Whether any file of the project is unsaved.</param>
    /// <returns>The title.</returns>
    public static string Format(string? projectName, string? documentTitle, bool hasUnsavedFiles)
    {
        if (string.IsNullOrEmpty(projectName))
        {
            return ProductName;
        }

        string project = hasUnsavedFiles ? projectName + UnsavedMark : projectName;
        return string.IsNullOrEmpty(documentTitle)
            ? string.Concat(project, Separator, ProductName)
            : string.Concat(documentTitle, Separator, project, Separator, ProductName);
    }
}
