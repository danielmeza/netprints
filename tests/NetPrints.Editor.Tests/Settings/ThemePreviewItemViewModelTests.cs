using Avalonia.Media;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.Settings;
using NetPrints.Editor.State;
using NetPrints.Extensibility.Settings;

namespace NetPrints.Editor.Tests.Settings;

public sealed class ThemePreviewItemViewModelTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "netprints-theme-preview", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static object? Grey(string name) => name.Contains("Opacity", StringComparison.Ordinal) ? 0.78 : Color.Parse("#FF336699");

    private JsonFileSettingsStore NewStore() => new(Path.Combine(folder, "settings.json"), NullLogger<JsonFileSettingsStore>.Instance);

    [Fact]
    public void ThereIsOneRowPerVariantWithTheLabelOfItsPresetAndTheSwatches()
    {
        using var preview = new ThemePreviewItemViewModel(NewStore(), (name, _) => Grey(name), action => action());

        Assert.Equal(["Dark", "Light"], preview.Rows.Select(row => row.Variant));
        Assert.Equal(["NetPrints Dark", "NetPrints Light"], preview.Rows.Select(row => row.PresetLabel));
        Assert.All(preview.Rows, row => Assert.Equal(ThemePreviewRows.SwatchTokens.Count, row.Swatches.Length));
    }

    [Fact]
    public async Task APresetTokenOverridesTheBaseInItsSwatch()
    {
        JsonFileSettingsStore store = NewStore();
        await store.SetAsync(EditorSettings.Descriptor, new EditorSettings { DarkPreset = "high-contrast.dark" }, TestContext.Current.CancellationToken);

        using var preview = new ThemePreviewItemViewModel(store, (name, _) => Grey(name), action => action());

        Assert.Equal("High contrast", preview.Rows[0].PresetLabel);
        ThemeSwatch canvas = preview.Rows[0].Swatches.Single(swatch => swatch.Token == "Canvas.Background");
        Assert.Equal(Color.Parse("#FF000000"), Assert.IsAssignableFrom<ISolidColorBrush>(canvas.Brush).Color);
    }

    [Fact]
    public async Task APresetChangeRefreshesTheRowsAndAnUnknownIdShowsTheDefault()
    {
        JsonFileSettingsStore store = NewStore();
        using var preview = new ThemePreviewItemViewModel(store, (name, _) => Grey(name), action => action());

        await store.SetAsync(EditorSettings.Descriptor, new EditorSettings { LightPreset = "high-contrast.light", DarkPreset = "no.such" }, TestContext.Current.CancellationToken);

        Assert.Equal("High contrast", preview.Rows[1].PresetLabel);
        Assert.Equal("NetPrints Dark", preview.Rows[0].PresetLabel);
    }
}
