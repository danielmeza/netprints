using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using NetPrints.Editor.Shell;
using NetPrints.Editor.State;
using NetPrints.Editor.UITests.Shell;
using NetPrints.Extensibility.Settings;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Editor.UITests.Theming;

/// <summary>FR-082: View › Theme switches the application's theme variant, keeps the choice, and the start-up applies it.</summary>
public class ThemeSettingTests
{
    private static EditorApp App => Assert.IsType<EditorApp>(Application.Current);

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [InlineData("theme.light", EditorTheme.Light)]
    [InlineData("theme.system", EditorTheme.System)]
    [InlineData("theme.dark", EditorTheme.Dark)]
    public async Task TheThemeCommandsSetTheVariantAndStoreTheChoice(string command, EditorTheme expected)
    {
        await using var app = ShellApp.Start();
        try
        {
            App.ApplyTheme(expected == EditorTheme.Dark ? EditorTheme.Light : EditorTheme.Dark);

            Assert.True(app.Commands.TryRun(app.Command(command)));

            Assert.Equal(VariantOf(expected), App.RequestedThemeVariant);
            await UiWait.UntilAsync(app.Driver, () => Task.FromResult(app.Composition.Context.Settings.Get(EditorSettings.Descriptor).Theme == expected),
                "theme stored", TestContext.Current.CancellationToken);
        }
        finally
        {
            App.ApplyTheme(EditorTheme.Dark);
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheThemeMenuChecksTheActiveThemeAndFollowsTheChoice()
    {
        await using var app = ShellApp.Start();
        try
        {
            CommandEntryViewModel[] choices = [.. Assert.IsType<MenuBarViewModel>(app.Shell.MenuBar).Menus.Single(menu => menu.Header == "View").Items.Single(entry => entry.IsSubmenu).Items ?? []];
            Assert.Equal(["Dark", "Light", "System"], choices.Select(choice => choice.Label));
            Assert.Equal([true, false, false], choices.Select(choice => choice.IsChecked));

            Assert.True(app.Commands.TryRun(app.Command("theme.light")));
            await UiWait.UntilAsync(app.Driver, () => Task.FromResult(choices[1].IsChecked), "Light checked", TestContext.Current.CancellationToken);

            Assert.Equal([false, true, false], choices.Select(choice => choice.IsChecked));

            Assert.True(app.Commands.TryRun(app.Command("theme.system")));
            await UiWait.UntilAsync(app.Driver, () => Task.FromResult(choices[2].IsChecked), "System checked", TestContext.Current.CancellationToken);

            Assert.Equal([false, false, true], choices.Select(choice => choice.IsChecked));
        }
        finally
        {
            App.ApplyTheme(EditorTheme.Dark);
        }
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [InlineData(EditorTheme.Light)]
    [InlineData(EditorTheme.System)]
    [InlineData(EditorTheme.Dark)]
    public void TheStartUpAppliesTheStoredTheme(EditorTheme stored)
    {
        try
        {
            App.ApplyTheme(stored == EditorTheme.Dark ? EditorTheme.Light : EditorTheme.Dark);

            App.ApplyThemeSetting(new ThemeSettings(stored));

            Assert.Equal(VariantOf(stored), App.RequestedThemeVariant);
        }
        finally
        {
            App.ApplyTheme(EditorTheme.Dark);
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void TheStartUpDefaultIsDark()
    {
        App.ApplyTheme(EditorTheme.Light);
        try
        {
            App.ApplyThemeSetting(new ThemeSettings(null));

            Assert.Equal(ThemeVariant.Dark, App.RequestedThemeVariant);
        }
        finally
        {
            App.ApplyTheme(EditorTheme.Dark);
        }
    }

    private static ThemeVariant VariantOf(EditorTheme theme) =>
        theme == EditorTheme.Light ? ThemeVariant.Light : theme == EditorTheme.Dark ? ThemeVariant.Dark : ThemeVariant.Default;

    private sealed class ThemeSettings(EditorTheme? theme) : ISettingsStore
    {
        public T Get<T>(ExtensionSettingsDescriptor<T> descriptor) =>
            theme is { } value && (object)new EditorSettings { Theme = value } is T settings ? settings : descriptor.Default;

        public ValueTask SetAsync<T>(ExtensionSettingsDescriptor<T> descriptor, T value, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
