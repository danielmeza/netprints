namespace NetPrints.Editor.Commands;

/// <summary>The <c>addEventGraph</c> command: adds an event graph to the target class and opens it.</summary>
public sealed class AddEventGraphCommandHandler() : ClassCommandHandler((actions, cls) => actions.AddEventGraph(cls));
