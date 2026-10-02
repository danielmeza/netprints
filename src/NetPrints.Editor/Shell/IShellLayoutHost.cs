namespace NetPrints.Editor.Shell;

/// <summary>
/// What hosts the shell's documents and tool panels. The docking adapter implements it, and the shell window binds the
/// area between its menu and its status bar to it, so neither the window nor <see cref="ShellViewModel"/> knows the
/// docking library.
/// </summary>
public interface IShellLayoutHost
{
}
