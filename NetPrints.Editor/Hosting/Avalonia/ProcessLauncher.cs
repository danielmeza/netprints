using System.Diagnostics;
using NetPrints.Editor.Hosting;

namespace NetPrints.Editor.Hosting.Avalonia;

/// <summary>Starts processes; their console output goes to the editor's terminal.</summary>
public sealed class ProcessLauncher : IProcessLauncher
{
    public void Start(string fileName, string? arguments) =>
        Process.Start(new ProcessStartInfo(fileName, arguments ?? "") { UseShellExecute = false })?.Dispose();
}
