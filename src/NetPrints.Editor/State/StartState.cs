namespace NetPrints.Editor.State;

/// <summary>The content of <c>start.json</c>: what the start page remembers between runs.</summary>
/// <param name="SchemaVersion">The schema version.</param>
/// <param name="WhatsNewSeenVersion">The product version whose release notes the user has been shown, or null for none.</param>
/// <param name="NewProjectLocation">The parent folder the user last created a project or copied a sample in, or null for the default.</param>
public sealed record StartState(int SchemaVersion, string? WhatsNewSeenVersion, string? NewProjectLocation = null) : IStateFile;
