namespace NetPrints.Editor.ClassEditor;

/// <summary>
/// Requests that the class editor shows the inspector for <paramref name="Target"/> (a
/// <see cref="Variables.MemberVariableViewModel"/>), sent instead of the child view
/// model calling back into its owning <see cref="ClassEditorViewModel"/> directly (FR-038).
/// </summary>
/// <param name="Target">The view model to select and show the inspector for.</param>
public sealed record SelectInspectorMessage(Variables.MemberVariableViewModel Target);
