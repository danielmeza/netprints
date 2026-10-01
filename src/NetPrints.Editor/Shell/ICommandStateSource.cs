namespace NetPrints.Editor.Shell;

/// <summary>Raises one notification when anything the project session contributes to a command's enabled state or label may have changed.</summary>
public interface ICommandStateSource
{
    /// <summary>Raised, on the UI thread, when the session is replaced or its command state changes (see <see cref="ProjectSessionViewModel.CommandStatesChanged"/>).</summary>
    event EventHandler? CommandStatesChanged;
}
