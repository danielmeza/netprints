using NetPrints.Compilation;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Contributions.BuiltIn;

/// <summary>The built-in segments of the status bar: error and warning counts, selection count, build state, run state and zoom.</summary>
public static class StatusBarContributions
{
    /// <summary>Id of the error count segment.</summary>
    public const string ErrorsId = IdPrefix + "errors";

    /// <summary>Id of the warning count segment.</summary>
    public const string WarningsId = IdPrefix + "warnings";

    /// <summary>Id of the selection count segment.</summary>
    public const string SelectionId = IdPrefix + "selection";

    /// <summary>Id of the build state segment.</summary>
    public const string BuildId = IdPrefix + "build";

    /// <summary>Id of the run state segment.</summary>
    public const string RunId = IdPrefix + "run";

    /// <summary>Id of the zoom segment.</summary>
    public const string ZoomId = IdPrefix + "zoom";

    private const string IdPrefix = "netprints.status.";
    private const int PercentScale = 100;
    private const int First = 0;
    private const int Second = 1;
    private const int Third = 2;

    /// <summary>Registers the segments.</summary>
    /// <param name="registry">The registry to add to.</param>
    public static void Register(IEditorContributions registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        Add(registry, ErrorsId, StatusBarAlignment.Left, First, shell => Count(shell, CodeDiagnosticSeverity.Error));
        Add(registry, WarningsId, StatusBarAlignment.Left, Second, shell => Count(shell, CodeDiagnosticSeverity.Warning));
        Add(registry, SelectionId, StatusBarAlignment.Left, Third, Selected);
        Add(registry, BuildId, StatusBarAlignment.Right, First, shell => shell.StatusBar.BuildState == BuildState.Building ? shell.StatusBar.BuildStateText : "", (shell, read) => new BuildStatusSegmentViewModel(shell, read), always: true);
        Add(registry, RunId, StatusBarAlignment.Right, Second, shell => shell.StatusBar.BuildState == BuildState.Running ? shell.StatusBar.BuildStateText : null);
        Add(registry, ZoomId, StatusBarAlignment.Right, Third, Zoom);
    }

    private static void Add(
        IEditorContributions registry,
        string id,
        StatusBarAlignment alignment,
        int order,
        Func<ShellViewModel, string?> read,
        Func<ShellViewModel, Func<ShellViewModel, string?>, StatusSegmentViewModel>? create = null,
        bool always = false) =>
        registry.AddStatusBarItem(new StatusBarItemDescriptor(
            id,
            alignment,
            order,
            context => create?.Invoke(ShellOf(context), read) ?? new StatusSegmentViewModel(ShellOf(context), read),
            context => always || !string.IsNullOrEmpty(read(ShellOf(context)))));

    private static ShellViewModel ShellOf(IStatusBarContext context) =>
        (context as ShellStatusBarContext)?.Source ?? throw new InvalidOperationException("The editor's own status bar segments run only in the editor's shell.");

    /// <summary>Gets the text of a diagnostic count, with the singular form for one.</summary>
    /// <param name="count">The number of diagnostics; at least 1.</param>
    /// <param name="severity">Error or warning.</param>
    /// <returns>The count and the noun, such as <c>1 error</c> or <c>3 warnings</c>.</returns>
    internal static string CountText(int count, CodeDiagnosticSeverity severity)
    {
        bool error = severity == CodeDiagnosticSeverity.Error;
        string format = (error, count == 1) switch
        {
            (true, true) => Strings.Status_ErrorsOne,
            (true, false) => Strings.Status_ErrorsOther,
            (false, true) => Strings.Status_WarningsOne,
            (false, false) => Strings.Status_WarningsOther,
        };
        return TextFormat.Of(format, count);
    }

    /// <summary>Gets the text of the selection count.</summary>
    /// <param name="count">The number of selected nodes.</param>
    /// <returns>The count and the word, such as <c>2 selected</c>.</returns>
    internal static string SelectedText(int count) => TextFormat.Of(Strings.Status_Selected, count);

    /// <summary>Gets the text of the zoom level.</summary>
    /// <param name="zoom">The zoom factor, where 1 is 100%.</param>
    /// <returns>The rounded percentage, such as <c>150%</c>.</returns>
    internal static string ZoomText(double zoom) => TextFormat.Of(Strings.Status_Zoom, Math.Round(zoom * PercentScale));

    private static string? Count(ShellViewModel shell, CodeDiagnosticSeverity severity)
    {
        int count = shell.Session?.Project.LastDiagnostics.Count(diagnostic => diagnostic.Severity == severity) ?? 0;
        return count == 0 ? null : CountText(count, severity);
    }

    private static string? Selected(ShellViewModel shell)
    {
        int count = (shell.ActiveDocument as GraphDocumentViewModel)?.Graph.SelectedNodes.Count() ?? 0;
        return count == 0 ? null : SelectedText(count);
    }

    private static string? Zoom(ShellViewModel shell) =>
        shell.ActiveDocument is IViewportDocument { ViewportZoom: not 1 and var zoom } ? ZoomText(zoom) : null;
}
