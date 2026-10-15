namespace NetPrints.Editor.Dialogs;

/// <summary>What the user chose in the recovery dialog.</summary>
public enum RecoveryChoice
{
    /// <summary>Open the project as it is and keep the backups. The default, so closing the dialog any other way loses nothing.</summary>
    Later,

    /// <summary>Load the backed-up content as unsaved changes.</summary>
    Restore,

    /// <summary>Delete the backups.</summary>
    Discard,
}
