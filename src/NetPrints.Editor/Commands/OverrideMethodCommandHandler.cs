using NetPrints.Editor.Contributions;

namespace NetPrints.Editor.Commands;

/// <summary>The <c>overrideMethod</c> command: asks which base method of the target class to override and opens the override.</summary>
public sealed class OverrideMethodCommandHandler() : ClassCommandHandler((actions, cls, cancellationToken) => actions.OverrideMethodAsync(cls, cancellationToken));
