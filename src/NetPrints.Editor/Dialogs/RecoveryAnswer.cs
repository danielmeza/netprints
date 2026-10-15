namespace NetPrints.Editor.Dialogs;

/// <summary>What the user answered in the recovery dialog.</summary>
/// <param name="Choice">The button pressed; <see cref="RecoveryChoice.Later"/> when the dialog was dismissed.</param>
/// <param name="RestorePaths">With <see cref="RecoveryChoice.Restore"/>, the class paths the user chose to restore; the other offered files are discarded. Empty otherwise.</param>
public sealed record RecoveryAnswer(RecoveryChoice Choice, IReadOnlyCollection<string> RestorePaths)
{
    /// <summary>Gets the answer that changes nothing: the dialog was dismissed.</summary>
    public static RecoveryAnswer Later { get; } = new(RecoveryChoice.Later, []);
}
