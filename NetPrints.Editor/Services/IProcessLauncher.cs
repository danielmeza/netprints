namespace NetPrints.Editor.Services;

/// <summary>
/// Starts external processes (running compiled projects).
/// </summary>
public interface IProcessLauncher
{
    void Start(string fileName, string? arguments);
}
