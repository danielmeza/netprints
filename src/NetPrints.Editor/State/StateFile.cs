namespace NetPrints.Editor.State;

/// <summary>A per-user state file that carries a schema version (state-files.md §2).</summary>
internal interface IStateFile
{
    /// <summary>Gets the schema version of the file; readers use the defaults for any other value than <see cref="StateFile.CurrentVersion"/>.</summary>
    int SchemaVersion { get; }
}

/// <summary>Constants shared by the state files.</summary>
internal static class StateFile
{
    /// <summary>The schema version this editor reads and writes.</summary>
    public const int CurrentVersion = 1;
}
