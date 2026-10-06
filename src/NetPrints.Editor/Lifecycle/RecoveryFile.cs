namespace NetPrints.Editor.Lifecycle;

/// <summary>One backed-up file offered for recovery.</summary>
/// <param name="Path">The class path of the file (relative to the project, <c>/</c> separators).</param>
/// <param name="WrittenUtc">When the backup was written.</param>
/// <param name="IsOlderThanFile">Whether the file on disk was written after the backup.</param>
public sealed record RecoveryFile(string Path, DateTime WrittenUtc, bool IsOlderThanFile);
