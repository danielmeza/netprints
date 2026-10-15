namespace NetPrints.Editor.Commands;

/// <summary>The <c>addMethod</c> command: adds a method to the target class and opens it.</summary>
public sealed class AddMethodCommandHandler() : ClassCommandHandler((actions, cls) => actions.AddMethod(cls));
