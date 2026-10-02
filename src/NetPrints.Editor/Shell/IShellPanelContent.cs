namespace NetPrints.Editor.Shell;

/// <summary>The view model of a panel that needs the shell: it is created before the layout, so it gets what it needs afterwards.</summary>
public interface IShellPanelContent
{
    /// <summary>Gives the panel the shell it lives in; called once, from <see cref="ShellViewModel.AttachPanels"/>.</summary>
    /// <param name="context">The shell services.</param>
    void Attach(PanelContext context);

    /// <summary>Releases what the panel follows and holds; called once, from <see cref="ShellViewModel.Dispose"/>.</summary>
    void Detach();
}
