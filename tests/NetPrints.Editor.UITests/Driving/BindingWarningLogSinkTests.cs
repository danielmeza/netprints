using Avalonia.Logging;

namespace NetPrints.Editor.UITests.Driving;

public class BindingWarningLogSinkTests
{
    private const string Template = "An error occurred binding {Property} to {Expression} at {ExpressionErrorPoint}: {Message}";

    [Fact]
    public void AWarningWhoseTextMentionsLayoutIsUnexplained()
    {
        var sink = new BindingWarningLogSink();

        sink.Log(LogEventLevel.Warning, "Binding", new object(), Template, "Width", "Layout.Width", "Layout", "Value is null.");

        Assert.Single(sink.Unexplained);
    }

    [Fact]
    public void ADockWarningWithTheKnownSourceAndMessageIsExplainedButNotWithAnotherSource()
    {
        var sink = new BindingWarningLogSink();
        var known = new Dock.Avalonia.Controls.ToolChromeControl();

        sink.Log(LogEventLevel.Warning, "Binding", known, Template, "IsPinned", "ActiveDockable.OriginalOwner", "ActiveDockable", "Value is null.");
        sink.Log(LogEventLevel.Warning, "Binding", new object(), Template, "IsPinned", "ActiveDockable.OriginalOwner", "ActiveDockable", "Value is null.");

        Assert.Equal(typeof(object).FullName, Assert.Single(sink.Unexplained).Source);
    }
}
