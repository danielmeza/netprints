namespace NetPrints.Editor.Dialogs;

/// <summary>What the user chose in the unsaved changes dialog.</summary>
public enum UnloadChoice
{
    /// <summary>Keep the project and its changes. The default, so closing the dialog any other way cancels.</summary>
    Cancel,

    /// <summary>Save every unsaved file, then unload.</summary>
    Save,

    /// <summary>Unload without saving.</summary>
    Discard,
}
