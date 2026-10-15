namespace NetPrints.Editor.StartPage;

/// <summary>The release notes bundled with the editor (<c>StartPage/WhatsNew.md</c>, an embedded resource).</summary>
internal static class WhatsNewResource
{
    /// <summary>The embedded resource's name.</summary>
    public const string Name = "NetPrints.Editor.StartPage.WhatsNew.md";

    /// <summary>Reads the bundled release notes.</summary>
    /// <returns>The Markdown text.</returns>
    /// <exception cref="InvalidOperationException">The resource is missing from the assembly.</exception>
    public static string Read()
    {
        using Stream stream = typeof(WhatsNewResource).Assembly.GetManifestResourceStream(Name)
            ?? throw new InvalidOperationException($"The embedded resource '{Name}' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
