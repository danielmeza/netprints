using System.Diagnostics;

namespace NetPrints.Editor.StartPage;

/// <summary>Opens a folder with the operating system's file manager.</summary>
internal sealed class ShellFolderLauncher : IFolderLauncher
{
    /// <inheritdoc/>
    public void Reveal(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        using Process? started = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
}
