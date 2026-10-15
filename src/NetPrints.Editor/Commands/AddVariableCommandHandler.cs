namespace NetPrints.Editor.Commands;

/// <summary>The <c>addVariable</c> command: adds a variable to the target class.</summary>
public sealed class AddVariableCommandHandler() : ClassCommandHandler((actions, cls) => actions.AddVariable(cls));
