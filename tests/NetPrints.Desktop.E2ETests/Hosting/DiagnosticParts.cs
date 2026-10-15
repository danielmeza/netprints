using System.Globalization;
using System.Text;
using System.Text.Json;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Desktop.E2ETests.Hosting;

/// <summary>What the failure diagnostics read from the editor under test; a fake in the unit tests.</summary>
public interface ICapturedEditor
{
    int ProcessId { get; }

    bool HasExited { get; }

    int ExitCode { get; }

    string Output { get; }

    string Errors { get; }

    /// <summary>Opens a connection of its own to the automation agent.</summary>
    Task<AutomationClient> ConnectAsync(TimeSpan timeout, CancellationToken cancellationToken);
}

/// <summary>The files of the failure diagnostics (contracts/ci.md §3), written from a leased editor.</summary>
public static class DiagnosticParts
{
    private const int EditorLogLines = 400;
    private const int StderrLines = 100;
    private static readonly TimeSpan ToolLimit = TimeSpan.FromSeconds(9);
    private static readonly TimeSpan ConnectLimit = TimeSpan.FromSeconds(5);

    /// <summary>Creates the parts for one failing test.</summary>
    /// <param name="kind">The failure kind (exception, assertion, timeout, editor exited).</param>
    /// <param name="testClass">The failing test class.</param>
    /// <param name="steps">The test's step timer.</param>
    /// <param name="moment">Where the test was when it failed.</param>
    /// <param name="display">The worker's display name.</param>
    /// <param name="editor">The editor the test holds.</param>
    /// <param name="screenshot">Takes a PNG of the display.</param>
    public static IReadOnlyList<CapturePart> Create(string kind, string testClass, StepTimer steps, FailureMoment moment, string display, ICapturedEditor editor, Func<CancellationToken, Task<byte[]>> screenshot)
    {
        return
        [
            new("summary", "summary.md", (path, token) => File.WriteAllTextAsync(path, Summary(kind, testClass, moment, display, editor), token)),
            new("timings", "timings.md", (path, token) => File.WriteAllTextAsync(path, steps.Timings(moment), token)),
            new("ui tree", "ui-tree.json", (path, token) => WriteUiTreeAsync(path, editor, token)),
            new("display", "display.png", async (path, token) => await File.WriteAllBytesAsync(path, await screenshot(token), token)),
            new("editor log", "editor.log", (path, token) => File.WriteAllTextAsync(path, EditorLog(editor), token)),
            new("run state", "run-state.json", (path, token) => WriteRunStateAsync(path, editor, token)),
            new("process", "process.txt", (path, token) => File.WriteAllTextAsync(path, editor.HasExited ? $"exited {editor.ExitCode}" : "running", token)),
        ];
    }

    /// <summary>Takes the screenshot of the worker's display with the X11 tools.</summary>
    public static Func<CancellationToken, Task<byte[]>> Screenshot(Tool tool) =>
        token => tool.RunBytesAsync("import", ["-window", "root", "png:-"], token, timeout: ToolLimit);

    private static string Summary(string kind, string testClass, FailureMoment moment, string display, ICapturedEditor editor)
    {
        return new StringBuilder()
            .AppendLine(CultureInfo.InvariantCulture, $"# {testClass}").AppendLine()
            .AppendLine(CultureInfo.InvariantCulture, $"- failure: {kind}")
            .AppendLine(CultureInfo.InvariantCulture, $"- test: {TestContext.Current.TestMethod?.MethodName ?? "test"}")
            .AppendLine(CultureInfo.InvariantCulture, $"- step: {moment.Step ?? "(none)"} ({Math.Round(moment.Elapsed.TotalSeconds).ToString("0", CultureInfo.InvariantCulture)} s)")
            .AppendLine(CultureInfo.InvariantCulture, $"- display: {display}")
            .AppendLine(CultureInfo.InvariantCulture, $"- editor pid: {editor.ProcessId}")
            .AppendLine(CultureInfo.InvariantCulture, $"- time: {DateTime.UtcNow:O}")
            .ToString();
    }

    private static string EditorLog(ICapturedEditor editor)
    {
        static string Tail(string text, int lines) =>
            string.Join(Environment.NewLine, text.Split('\n', StringSplitOptions.TrimEntries).TakeLast(lines));

        return Tail(editor.Output, EditorLogLines) + Environment.NewLine + "--- stderr ---" + Environment.NewLine + Tail(editor.Errors, StderrLines) + Environment.NewLine;
    }

    private static async Task WriteRunStateAsync(string path, ICapturedEditor editor, CancellationToken cancellationToken)
    {
        if (editor.HasExited)
        {
            await File.WriteAllTextAsync(path, $"{{\"editorExitCode\":{editor.ExitCode.ToString(CultureInfo.InvariantCulture)}}}", cancellationToken);
            return;
        }

        await using var client = await editor.ConnectAsync(ConnectLimit, cancellationToken);
        var state = await client.RunStateAsync(cancellationToken);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(state, AutomationJsonContext.Default.RunStateSnapshot), cancellationToken);
    }

    private static async Task WriteUiTreeAsync(string path, ICapturedEditor editor, CancellationToken cancellationToken)
    {
        await using var file = File.Create(path);
        await using var json = new Utf8JsonWriter(file, new JsonWriterOptions { Indented = true });
        json.WriteStartObject();
        if (editor.HasExited)
        {
            json.WriteNull("focused");
            json.WriteNumber("editorExitCode", editor.ExitCode);
            json.WriteStartArray("windows");
            json.WriteEndArray();
            json.WriteEndObject();
            return;
        }

        await using var client = await editor.ConnectAsync(ConnectLimit, cancellationToken);
        var elements = await client.TreeAsync(cancellationToken);
        var focused = elements.FirstOrDefault(e => e[AutomationPropertyNames.IsFocused] == "True");
        json.WriteString("focused", focused is null ? null : focused.AutomationId.Length > 0 ? focused.AutomationId : focused[AutomationPropertyNames.Type]);
        json.WriteStartArray("windows");
        foreach (var window in elements.GroupBy(e => e.Window))
        {
            json.WriteStartObject();
            json.WriteString("window", window.Key);
            json.WriteString("title", window.First()[AutomationPropertyNames.WindowTitle]);
            json.WriteStartArray("elements");
            foreach (var element in window)
            {
                json.WriteStartObject();
                json.WriteString("automationId", element.AutomationId);
                json.WriteString("name", element.Name);
                json.WriteString("type", element[AutomationPropertyNames.Type]);
                json.WriteStartObject("bounds");
                json.WriteNumber("x", element.Bounds.X);
                json.WriteNumber("y", element.Bounds.Y);
                json.WriteNumber("width", element.Bounds.Width);
                json.WriteNumber("height", element.Bounds.Height);
                json.WriteEndObject();
                json.WriteBoolean("isVisible", element.IsVisible);
                json.WriteBoolean("isEnabled", element.IsEnabled);
                json.WriteBoolean("hasFocus", element[AutomationPropertyNames.IsFocused] == "True");
                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteEndObject();
        }

        json.WriteEndArray();
        json.WriteEndObject();
    }
}
