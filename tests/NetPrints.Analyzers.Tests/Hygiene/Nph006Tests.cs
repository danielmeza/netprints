using System.Threading.Tasks;
using NetPrints.Analyzers.Tests.Infrastructure;
using Xunit;

namespace NetPrints.Analyzers.Tests.Hygiene;

/// <summary>NPH006: a string literal passed to a user-visible text sink.</summary>
public class Nph006Tests
{
    private const string Id = "NPH006";

    private const string Sinks =
        """
        namespace Sample
        {
            public readonly record struct LocalizedText(string Key, string? DefaultText = null)
            {
                public static implicit operator LocalizedText(string text) => new(text, text);
            }

            public sealed record CommandDescriptor(string Id, LocalizedText Label);

            public sealed record DialogDescriptor(string Id, LocalizedText Title);

            public interface IDialogService
            {
                void ShowNotification(string title, string message);
            }

            public interface IEditorDialogs
            {
                bool Confirm(string title, string message, string confirmLabel);
            }

            public sealed class ShellViewModel
            {
                public void ShowStatus(string message) { }
            }

            public sealed class StatusBarViewModel
            {
                public void Show(string message, System.TimeSpan? expiry = null) { }
            }
        }
        """;

    private static string Use(string body) =>
        Sinks + "\nnamespace Use\n{\n    using Sample;\n\n    class C\n    {\n        void M(IDialogService dialogs, IEditorDialogs editor, ShellViewModel shell, StatusBarViewModel status, string text, LocalizedText key)\n        {\n" + body + "\n        }\n    }\n}\n";

    [Fact]
    public async Task ALiteralCommandLabelIsTheNeutralDefaultOfItsConventionKey()
    {
        await HygieneHarness.AssertAsync(Use("            _ = new CommandDescriptor(\"a.b\", \"Save\");"), Id);
    }

    [Fact]
    public async Task ALiteralDialogTitleAndMessageAreReported()
    {
        await HygieneHarness.AssertAsync(
            Use(
                """
                            dialogs.ShowNotification({|NPH006:"Saved"|}, {|NPH006:"All files were saved."|});
                            editor.Confirm({|NPH006:"Remove"|}, text, {|NPH006:"Remove"|});
                """),
            Id);
    }

    [Fact]
    public async Task ALiteralDialogDescriptorTitleIsTheNeutralDefaultOfItsConventionKey()
    {
        await HygieneHarness.AssertAsync(Use("            _ = new DialogDescriptor(\"a.b\", \"About\");"), Id);
    }

    [Fact]
    public async Task ALiteralStatusTextIsReported()
    {
        await HygieneHarness.AssertAsync(Use("            shell.ShowStatus({|NPH006:\"Ready\"|});"), Id);
    }

    [Fact]
    public async Task ALiteralPassedToTheStatusBarIsReported()
    {
        await HygieneHarness.AssertAsync(Use("            status.Show({|NPH006:\"Saved\"|});\n            status.Show({|NPH006:\"Saved\"|}, System.TimeSpan.Zero);"), Id);
    }

    [Fact]
    public async Task AnInterpolatedStringWithTextIsReportedAtEverySink()
    {
        await HygieneHarness.AssertAsync(
            Use(
                """
                            status.Show({|NPH006:$"Opened {text}"|});
                            dialogs.ShowNotification(text, {|NPH006:$"Saved {text}"|});
                            shell.ShowStatus({|NPH006:$"Ready: {text}"|});
                """),
            Id);
    }

    [Fact]
    public async Task AKeyedTextAnInterpolationOfOnlyValuesAndAVariableAreAccepted()
    {
        await HygieneHarness.AssertAsync(
            Use(
                """
                            _ = new CommandDescriptor("a.b", key);
                            _ = new CommandDescriptor("a.b", new LocalizedText("Command.a.b.Label", "Save"));
                            dialogs.ShowNotification(text, $"{text}");
                            shell.ShowStatus(text);
                            status.Show(text);
                            status.Show($"{text}{text}");
                """),
            Id);
    }

    [Fact]
    public async Task AnIdentifierArgumentOfASinkTypeIsNotText()
    {
        await HygieneHarness.AssertAsync(Use("            _ = new CommandDescriptor(\"a.b\", key);\n            _ = new DialogDescriptor(\"a.b\", text);"), Id);
    }

    [Fact]
    public async Task ALedgerRowCoversTheLiteralInItsFile()
    {
        await HygieneHarness.AssertAsync(
            Use("            shell.ShowStatus(\"Ready\");\n            shell.ShowStatus({|NPH006:\"Busy\"|});"),
            Id,
            new HygieneInputs("/repo/src/Lib/Shell.cs", "NPH006 | src/Lib/Shell.cs | Ready"));
    }
}
