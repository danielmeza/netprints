namespace NetPrints.Editor.Commands;

/// <summary>The <c>addConstructor</c> command: adds a constructor to the target class and opens it.</summary>
public sealed class AddConstructorCommandHandler() : ClassCommandHandler((actions, cls) => actions.AddConstructor(cls));
