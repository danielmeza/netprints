using Avalonia.Logging;

namespace NetPrints.Editor.UITests.Driving;

/// <summary>Records every Avalonia "Binding" area log message at Warning or above (batch D1's "Value is
/// null" noise, and batch X2a's compiled-binding regressions). Assembly-wide sequential tests
/// (<c>TestAppBuilder.cs</c>), so a global sink swap is safe.</summary>
internal sealed class BindingWarningLogSink : ILogSink
{
    public List<string> Messages { get; } = [];

    public bool IsEnabled(LogEventLevel level, string area) => level >= LogEventLevel.Warning && area == "Binding";

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate) => Messages.Add(messageTemplate);

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues) =>
        Messages.Add($"{messageTemplate} {string.Join(' ', propertyValues)}");
}
