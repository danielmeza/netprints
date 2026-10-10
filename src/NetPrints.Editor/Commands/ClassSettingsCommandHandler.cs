namespace NetPrints.Editor.Commands;

/// <summary>The <c>classSettings</c> command: shows the settings of the target class.</summary>
public sealed class ClassSettingsCommandHandler() : ClassCommandHandler((actions, cls) => actions.ShowClassSettings(cls));
