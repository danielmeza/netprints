namespace NetPrints.Editor.StartPage;

/// <summary>Opens a web address in the user's browser.</summary>
internal interface IUrlLauncher
{
    /// <summary>Opens <paramref name="url"/>.</summary>
    /// <param name="url">An absolute http or https address.</param>
    void Open(string url);
}
