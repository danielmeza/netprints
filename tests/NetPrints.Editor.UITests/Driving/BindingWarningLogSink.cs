using Avalonia.Logging;

namespace NetPrints.Editor.UITests.Driving;

/// <summary>One recorded binding warning: the type of the log source and the formatted message.</summary>
/// <param name="Source">The full name of the source object's type, or an empty string when the log had no source.</param>
/// <param name="Message">The message with its property values substituted.</param>
internal sealed record BindingWarning(string Source, string Message)
{
    public override string ToString() => $"{Source}: {Message}";
}

/// <summary>Records every Avalonia "Binding" area log message at Warning or above (batch D1's "Value is
/// null" noise, and batch X2a's compiled-binding regressions). Assembly-wide sequential tests
/// (<c>TestAppBuilder.cs</c>), so a global sink swap is safe.</summary>
internal sealed class BindingWarningLogSink : ILogSink
{
    private const string Template = "An error occurred binding {Property} to {Expression} at {ExpressionErrorPoint}: {Message}";
    private const string NoValue = "Value is null.";
    private const string Unnamed = "(unknown)";
    private const string AvaloniaButton = "Avalonia.Controls.Button";
    private const string ToolChrome = "Dock.Avalonia.Controls.ToolChromeControl";
    private const string ActiveDockable = "ActiveDockable";

    /// <summary>The warnings Dock's own templates log for the tool dock a float empties: its <c>ActiveDockable</c> is null, and making the
    /// dock collapsable does not stop them. Each is matched by source type and exact message, so any other warning stays unexplained.</summary>
    private static readonly HashSet<BindingWarning> DockOwnWarnings =
    [
        Known(AvaloniaButton, Unnamed, "ActiveDockable.CanClose", ActiveDockable, NoValue),
        Known(AvaloniaButton, Unnamed, "ActiveDockable.CanPin", ActiveDockable, NoValue),
        Known(AvaloniaButton, Unnamed, "ActiveDockable.DockCapabilityOverrides.CanClose", ActiveDockable, NoValue),
        Known(AvaloniaButton, Unnamed, "ActiveDockable.DockCapabilityOverrides.CanPin", ActiveDockable, NoValue),
        Known("Avalonia.Controls.TextBlock", "Text", "ActiveDockable.Title", ActiveDockable, NoValue),
        Known(ToolChrome, "IsPinned", "ActiveDockable.OriginalOwner", ActiveDockable, NoValue),
    ];

    public List<BindingWarning> Warnings { get; } = [];

    /// <summary>Gets the warnings that are not one of Dock's own known ones: a NetPrints warning, or a new Dock or Avalonia one.</summary>
    public IEnumerable<BindingWarning> Unexplained => Warnings.Where(warning => !DockOwnWarnings.Contains(warning));

    public bool IsEnabled(LogEventLevel level, string area) => level >= LogEventLevel.Warning && area == "Binding";

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate) =>
        Warnings.Add(new BindingWarning(source?.GetType().FullName ?? string.Empty, messageTemplate));

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues) =>
        Warnings.Add(new BindingWarning(source?.GetType().FullName ?? string.Empty, $"{messageTemplate} {string.Join(' ', propertyValues)}"));

    private static BindingWarning Known(string source, string property, string expression, string errorPoint, string message) =>
        new(source, $"{Template} {property} {expression} {errorPoint} {message}");

}
