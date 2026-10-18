using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Microsoft.Extensions.Logging;
using NetPrints.Editor.State;
using NetPrints.Editor.Theming;
using Semi.Avalonia;

namespace NetPrints.Editor.UITests.Theming;

/// <summary>FR-067 to FR-069, SC-009: the presets pass the contrast gates, the overlay stays inside its variant, and switching needs no restart.</summary>
public class ThemePresetTests
{
    public static TheoryData<string, string> PresetsByBase()
    {
        TheoryData<string, string> data = [];
        foreach (ThemePreset preset in ThemePresets.BuiltIn)
        {
            data.Add(preset.Base.ToString(), preset.Id);
        }

        return data;
    }

    private static Application App => Application.Current ?? throw new InvalidOperationException("No application.");

    private static ThemeVariant VariantOf(ThemeBase variant) => variant == ThemeBase.Dark ? ThemeVariant.Dark : ThemeVariant.Light;

    private static object? Resolve(string name, ThemeVariant variant) => App.TryGetResource(name, variant, out object? value) ? value : null;

    private static ThemePalette PaletteOf(ThemePreset preset)
    {
        Application app = App;
        ThemeVariant variant = VariantOf(preset.Base);
        using IDisposable restore = new ThemeOverlayScope(app);
        return new ThemePalette(preset, name => app.TryGetResource(name, variant, out object? value) ? value : null);
    }

    private static object?[] Snapshot(ThemeBase variant) =>
        [.. ThemeTokenCatalogue.All.Select(token => Resolve(token.Name, VariantOf(variant)))];

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [MemberData(nameof(PresetsByBase))]
    public void EveryBuiltInPresetPassesEveryContrastGate(string variant, string id)
    {
        ThemePreset preset = ThemePresets.Resolve(Enum.Parse<ThemeBase>(variant), id, out bool known);
        Assert.True(known);

        IReadOnlyList<ContrastCheck> failures = [.. ContrastGates.Evaluate(PaletteOf(preset)).Where(check => !check.Passed)];

        Assert.True(failures.Count == 0, $"{id}: " + string.Join("; ", failures.Select(check => $"{check.Pair} {check.Ratio:F2} < {check.Minimum:F2}")));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void TheGatesCoverTheRolesThePinFamiliesAndTheCard()
    {
        ThemePalette palette = PaletteOf(ThemePresets.Resolve(ThemeBase.Dark, EditorSettings.DefaultDarkPreset, out _));

        IReadOnlyList<ContrastCheck> checks = ContrastGates.Evaluate(palette);

        Assert.Contains(checks, check => check.Gate == ContrastGates.HeaderGate);
        Assert.Contains(checks, check => check.Gate == ContrastGates.PinGate);
        Assert.Contains(checks, check => check.Gate == ContrastGates.WireGate);
        Assert.Contains(checks, check => check.Gate == ContrastGates.CardGate);
        Assert.Contains(checks, check => check.Gate == ContrastGates.TitleGate);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ADarkPresetChangesNoLightTokenAndAnOverlayBeatsTheStyles()
    {
        var overlay = new ThemeOverlay(App);
        using IDisposable restore = new ThemeOverlayScope(App);
        object?[] lightBefore = Snapshot(ThemeBase.Light);
        object? darkCanvas = Resolve("Canvas.Background", ThemeVariant.Dark);

        overlay.Apply(ThemeBase.Dark, ThemePresets.Resolve(ThemeBase.Dark, "classic.dark", out _));

        Assert.Equal(lightBefore, Snapshot(ThemeBase.Light));
        Assert.NotEqual(darkCanvas, Resolve("Canvas.Background", ThemeVariant.Dark));
        Assert.Equal(Color.Parse("#FF000000"), Assert.IsType<Color>(Resolve("Canvas.Background", ThemeVariant.Dark)));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ClassicDarkGivesTheBlackCanvasTheTrianglePinsAndTheTranslucentCard()
    {
        var overlay = new ThemeOverlay(App);
        using IDisposable restore = new ThemeOverlayScope(App);

        overlay.Apply(ThemeBase.Dark, ThemePresets.Resolve(ThemeBase.Dark, "classic.dark", out _));

        Assert.Equal(Color.Parse("#FF000000"), Assert.IsType<Color>(Resolve("Canvas.Background", ThemeVariant.Dark)));
        Assert.Equal(Color.Parse("#AA202020"), Assert.IsAssignableFrom<ISolidColorBrush>(Resolve("Node.CardBackground", ThemeVariant.Dark)).Color);
        Assert.Same(Resolve("Pin.TypeShape.Triangle", ThemeVariant.Dark), Resolve("Pin.TypeShape", ThemeVariant.Dark));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ApplyingAnotherPresetRestoresWhatTheFirstOverrode()
    {
        var overlay = new ThemeOverlay(App);
        using IDisposable restore = new ThemeOverlayScope(App);
        object?[] before = Snapshot(ThemeBase.Dark);

        overlay.Apply(ThemeBase.Dark, ThemePresets.Resolve(ThemeBase.Dark, "classic.dark", out _));
        overlay.Apply(ThemeBase.Dark, ThemePresets.Resolve(ThemeBase.Dark, EditorSettings.DefaultDarkPreset, out _));

        Assert.Equal(before, Snapshot(ThemeBase.Dark));
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [InlineData("high-contrast.dark", "Aquatic")]
    [InlineData("high-contrast.light", "Desert")]
    public void HighContrastRunsOnSemisVariantAndKeepsItsTunedTokens(string id, string semi)
    {
        var logger = new ListLogger();
        var applier = new ThemeApplier(App, logger);
        ThemeBase variant = id.EndsWith("light", StringComparison.Ordinal) ? ThemeBase.Light : ThemeBase.Dark;
        using IDisposable restore = new ThemeOverlayScope(App);
        EditorSettings settings = variant == ThemeBase.Dark
            ? new EditorSettings { Theme = EditorTheme.Dark, DarkPreset = id }
            : new EditorSettings { Theme = EditorTheme.Light, LightPreset = id };

        try
        {
            applier.Apply(settings);

            Assert.Equal(semi, App.RequestedThemeVariant?.Key);
            Assert.Equal(VariantOf(variant), App.RequestedThemeVariant?.InheritVariant);
            Color canvas = Assert.IsType<Color>(Resolve("Canvas.Background", App.RequestedThemeVariant ?? ThemeVariant.Default));
            Assert.Equal(variant == ThemeBase.Dark ? Color.Parse("#FF000000") : Color.Parse("#FFFFFFFF"), canvas);
            Assert.Empty(logger.Messages);
        }
        finally
        {
            App.RequestedThemeVariant = ThemeVariant.Dark;
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void SwitchingPresetsAppliesAtOnceInBothVariants()
    {
        var applier = new ThemeApplier(App, new ListLogger());
        using IDisposable restore = new ThemeOverlayScope(App);
        try
        {
            applier.Apply(new EditorSettings { Theme = EditorTheme.Dark, DarkPreset = "classic.dark", LightPreset = "high-contrast.light" });
            Assert.Equal(Color.Parse("#FF000000"), Assert.IsType<Color>(Resolve("Canvas.Background", ThemeVariant.Dark)));
            Assert.Equal(Color.Parse("#FFFFFFFF"), Assert.IsType<Color>(Resolve("Canvas.Background", ThemeVariant.Light)));
            Assert.Equal(ThemeVariant.Dark, App.RequestedThemeVariant);

            applier.Apply(new EditorSettings { Theme = EditorTheme.Light });
            Assert.Equal(Color.Parse("#FF262626"), Assert.IsType<Color>(Resolve("Canvas.Background", ThemeVariant.Dark)));
            Assert.Equal(ThemeVariant.Light, App.RequestedThemeVariant);

            applier.Apply(new EditorSettings { Theme = EditorTheme.System });
            Assert.Equal(ThemeVariant.Default, App.RequestedThemeVariant);
        }
        finally
        {
            App.RequestedThemeVariant = ThemeVariant.Dark;
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void AnUnknownDarkPresetFallsBackWithALogEntryAndKeepsTheOtherKeys()
    {
        var logger = new ListLogger();
        var applier = new ThemeApplier(App, logger);
        using IDisposable restore = new ThemeOverlayScope(App);
        try
        {
            applier.Apply(new EditorSettings { Theme = EditorTheme.Light, DarkPreset = "no.such.preset", LightPreset = "high-contrast.light" });

            Assert.Equal(Color.Parse("#FF262626"), Assert.IsType<Color>(Resolve("Canvas.Background", ThemeVariant.Dark)));
            Assert.Equal(SemiTheme.Desert, App.RequestedThemeVariant);
            Assert.Contains("no.such.preset", Assert.Single(logger.Messages), StringComparison.Ordinal);
        }
        finally
        {
            App.RequestedThemeVariant = ThemeVariant.Dark;
        }
    }

    private sealed class ThemeOverlayScope : IDisposable
    {
        private readonly ThemeOverlay overlay;

        public ThemeOverlayScope(Application app)
        {
            overlay = new ThemeOverlay(app);
            overlay.Clear();
        }

        public void Dispose() => overlay.Clear();
    }

    private sealed class ListLogger : ILogger
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
