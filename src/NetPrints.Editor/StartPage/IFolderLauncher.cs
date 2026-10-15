namespace NetPrints.Editor.StartPage;

/// <summary>Shows a folder in the operating system's file manager.</summary>
internal interface IFolderLauncher
{
    /// <summary>Opens <paramref name="path"/> in the file manager.</summary>
    /// <param name="path">An existing folder.</param>
    void Reveal(string path);
}
