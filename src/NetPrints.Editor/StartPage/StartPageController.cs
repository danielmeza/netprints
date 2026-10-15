using NetPrints.Editor.Shell;

namespace NetPrints.Editor.StartPage;

/// <summary>Keeps the start page document open exactly while no project is open.</summary>
/// <param name="shell">The shell API that opens and closes documents.</param>
internal sealed class StartPageController(IShell shell)
{
    /// <summary>Opens the start page when no project is open and closes it when one is.</summary>
    /// <param name="projectOpen">Whether a project is open.</param>
    public void Sync(bool projectOpen)
    {
        if (projectOpen)
        {
            shell.CloseDocument(DocumentId.StartPage);
        }
        else
        {
            shell.OpenDocument(DocumentId.StartPage);
        }
    }
}
