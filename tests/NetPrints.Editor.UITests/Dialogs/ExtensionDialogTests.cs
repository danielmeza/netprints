using Avalonia.Headless.XUnit;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.UITests.Hosting;
using NetPrints.Editor.UITests.Shell;
using NetPrints.Extensibility.Loading;
using NetPrints.Testing.Ui.Dialogs;
using NetPrints.Testing.Ui.Driving;
using NetPrints.Testing.Ui.Shell;

namespace NetPrints.Editor.UITests.Dialogs;

public class ExtensionDialogTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ExtensionLoadFailuresAreListedAndTheEditorStaysUsable() // ED-T11
    {
        string folder = Path.Combine(Path.GetTempPath(), "netprints-ui-tests-ext-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllText(Path.Combine(folder, ExtensionManifest.FileName), "{ this is not a manifest");
            await using var app = ShellApp.Start([folder]);

            await app.Composition.StartAsync([]);

            var page = new IssuesDialogPage(app.Driver);
            string row = Assert.Single(await page.RowsAsync(Token));
            Assert.StartsWith("NPX001: ", row);
            Assert.Contains("was not loaded", row);

            await page.OkButton.ClickAsync(Token);
            await UiWait.UntilAsync(app.Driver, async () => !await page.ExistsAsync(Token), "issues dialog closed", Token);

            await new ShellPage(app.Driver).Menu.OpenAsync("File", ShellCommands.NewProject, Token); // still usable
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task NoFailuresShowNoDialog()
    {
        await using var app = HeadlessApp.Start();

        await app.Composition.StartAsync([]);

        Assert.False(await new IssuesDialogPage(app.Driver).ExistsAsync(Token));
        Assert.Empty(app.Dialogs.IssueDialogs);
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TrustDialogAnswersWithTheButtonPressed(bool trust)
    {
        using var ui = HeadlessUi.Create();
        var dialog = ui.Show(new TrustDialog("/work/P.csproj", ["/work/ext-a", "/work/ext-b"]));
        var page = new TrustDialogPage(ui.Driver);
        bool closed = false;
        dialog.Closed += (_, _) => closed = true;

        Assert.Contains("/work/P.csproj", await page.Prompt.TextAsync(Token));
        await (trust ? page.TrustButton : page.DontLoadButton).ClickAsync(Token);

        Assert.True(closed);
        Assert.Equal(trust, dialog.Result);
    }
}
