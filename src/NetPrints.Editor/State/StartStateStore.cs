namespace NetPrints.Editor.State;

internal static class StartStateStore
{
    /// <summary>Reads <c>start.json</c> (the defaults when there is none), applies <paramref name="change"/> and writes it back, so the fields the caller does not touch are kept.</summary>
    /// <param name="store">The state store.</param>
    /// <param name="change">Returns the new state from the current one.</param>
    /// <param name="userChanged">Whether the user chose the change; a newer file is rewritten only then.</param>
    public static void Update(this IEditorStateStore store, Func<StartState, StartState> change, bool userChanged = true)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(change);
        store.SaveStart(change(store.LoadStart() ?? new StartState(StateFile.CurrentVersion, null)), userChanged);
    }
}
