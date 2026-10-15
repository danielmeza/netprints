using System.Text.Json;
using NetPrints.Desktop.E2ETests.Hosting;
using NetPrints.Testing.Ui.Dialogs;
using NetPrints.Testing.Ui.Shell;

namespace NetPrints.Desktop.E2ETests.Scenarios;

/// <summary>An editor killed with unsaved changes: the next start offers the backup, and restoring then saving writes the pre-kill content (FR-025, SC-002).</summary>
public sealed class CrashRecoveryTests(DesktopWorkerPool pool) : ProjectEditorTestBase(pool)
{
    private const int ShortBackupDelay = 300;
    private const string BackupSuffix = ".bak.json";

    private string ListedBackupFile()
    {
        if (!Directory.Exists(BackupsDirectory))
        {
            return "";
        }

        foreach (string manifestPath in Directory.EnumerateFiles(BackupsDirectory, "manifest.json", SearchOption.AllDirectories))
        {
            try
            {
                using JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
                foreach (JsonElement entry in manifest.RootElement.GetProperty("files").EnumerateArray())
                {
                    string backupPath = Path.Combine(Path.GetDirectoryName(manifestPath) ?? "", entry.GetProperty("backupPath").GetString() ?? "");
                    if (backupPath.EndsWith(BackupSuffix, StringComparison.Ordinal) && File.Exists(backupPath))
                    {
                        return backupPath;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException or KeyNotFoundException or InvalidOperationException)
            {
            }
        }

        return "";
    }

    private string BackupsDirectory => Path.Combine(StateDirectory, "backups");

    protected override int? BackupDelayMilliseconds => ShortBackupDelay;

    [Fact]
    public Task RestoreAfterAKillSavesTheContentFromBeforeIt() => RunScenarioAsync(async token =>
    {
        await StartAsync(token);
        await WaitForProjectAsync(token);
        byte[] original = await File.ReadAllBytesAsync(ClassFile, token);
        byte[] expected;

        using (Step("edit and wait for the backup"))
        {
            await AddAVariableAsync(token);
            string BackupFile() => ListedBackupFile();
            await WaitForAsync(() => BackupFile().Length > 0, "the backup file", token);
            expected = await File.ReadAllBytesAsync(BackupFile(), token);
            Assert.Equal(original, await File.ReadAllBytesAsync(ClassFile, token));
        }

        using (Step("kill the editor"))
        {
            ExpectEditorExit();
            LeasedEditor.Kill();
            await LeasedEditor.Exited.WaitAsync(token);
        }

        var recover = default(RecoverDialogPage);
        using (Step("restart and restore"))
        {
            await RestartEditorAsync(new EditorStart(Path.Combine(SampleDirectory, "HelloWorld.csproj"), Environment(), WaitForProject: false), token);
            recover = new RecoverDialogPage(Driver);
            await recover.WaitVisibleAsync(token);
            await recover.RestoreButton.ClickAsync(token);
            await recover.WaitHiddenAsync(token);
            await WaitForProjectAsync(token);
            await Shell.Tree.RevealAsync(Shell.Tree.Variable("Variable"), ProjectTreePage.VariablesGroup, token);
        }

        using (Step("save"))
        {
            await Shell.Menu.InvokeAsync("File", ShellCommands.SaveAll, token);
            await WaitForAsync(() => !original.AsSpan().SequenceEqual(File.ReadAllBytes(ClassFile)), "the saved file", token);
        }

        Assert.Equal(expected, await File.ReadAllBytesAsync(ClassFile, token));
    });
}
