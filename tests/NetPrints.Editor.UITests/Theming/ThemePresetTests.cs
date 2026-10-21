using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Microsoft.Extensions.Logging;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.State;
using NetPrints.Editor.Theming;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UITests.Shell;
using NetPrints.Testing.Ui.Driving;
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

    private static CancellationToken Token => TestContext.Current.CancellationToken;

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

        overlay.Apply(ThemeBase.Dark, ThemePresets.Resolve(ThemeBase.Dark, "high-contrast.dark", out _));

        Assert.Equal(lightBefore, Snapshot(ThemeBase.Light));
        Assert.NotEqual(darkCanvas, Resolve("Canvas.Background", ThemeVariant.Dark));
        Assert.Equal(Color.Parse("#FF000000"), Assert.IsType<Color>(Resolve("Canvas.Background", ThemeVariant.Dark)));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ApplyingAnotherPresetRestoresWhatTheFirstOverrode()
    {
        var overlay = new ThemeOverlay(App);
        using IDisposable restore = new ThemeOverlayScope(App);
        object?[] before = Snapshot(ThemeBase.Dark);

        overlay.Apply(ThemeBase.Dark, ThemePresets.Resolve(ThemeBase.Dark, "high-contrast.dark", out _));
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
            applier.Apply(new EditorSettings { Theme = EditorTheme.Dark, DarkPreset = "high-contrast.dark", LightPreset = "high-contrast.light" });
            Assert.Equal(Color.Parse("#FF000000"), Assert.IsType<Color>(Resolve("Canvas.Background", ThemeVariant.Dark)));
            Assert.Equal(Color.Parse("#FFFFFFFF"), Assert.IsType<Color>(Resolve("Canvas.Background", ThemeVariant.Light)));
            Assert.Equal(ThemeVariant.Dark, App.RequestedThemeVariant?.InheritVariant);

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
    public void APresetCanSetEveryNewKindOfToken()
    {
        var overlay = new ThemeOverlay(App);
        using IDisposable restore = new ThemeOverlayScope(App);

        overlay.Apply(
            ThemeBase.Dark,
            new ThemePreset(
                "test.sizes",
                "Sizes",
                ThemeBase.Dark,
                null,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Font.Body"] = "18",
                    ["Space.S"] = "10",
                    ["Radius.Control"] = "6",
                    ["Inspector.LabelColumnWidth"] = "200",
                    ["Font.Mono"] = "Courier New",
                    ["Motion.Normal"] = "0:0:0.2",
                    ["Motion.Easing"] = "0.25,0.1,0.25,1",
                }));

        Assert.Equal(18d, Resolve("Font.Body", ThemeVariant.Dark));
        Assert.Equal(new Thickness(10), Resolve("Space.S", ThemeVariant.Dark));
        Assert.Equal(new CornerRadius(6), Resolve("Radius.Control", ThemeVariant.Dark));
        Assert.Equal(new GridLength(200), Resolve("Inspector.LabelColumnWidth", ThemeVariant.Dark));
        Assert.Equal("Courier New", Assert.IsType<FontFamily>(Resolve("Font.Mono", ThemeVariant.Dark)).Name);
        Assert.Equal(TimeSpan.FromSeconds(0.2), Resolve("Motion.Normal", ThemeVariant.Dark));
        Assert.IsType<SplineEasing>(Resolve("Motion.Easing", ThemeVariant.Dark));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task APresetSizeTokenReachesARenderedViewAtOnce()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        await session.Page.Tree.SelectAsync(session.Page.Tree.Class("Program"), Token);
        await new UiElement(session.Driver, new AutomationQuery(AutomationIds.ClassInspector)).WaitVisibleAsync(Token);
        Grid inspector = session.Window.GetVisualDescendants().OfType<Grid>().Single(grid => AutomationProperties.GetAutomationId(grid) == AutomationIds.ClassInspector);
        Assert.Equal(new GridLength(120), inspector.ColumnDefinitions[0].Width);

        using (IDisposable restore = new ThemeOverlayScope(App))
        {
            new ThemeOverlay(App).Apply(
                ThemeBase.Dark,
                new ThemePreset(
                    "test.sizes",
                    "Sizes",
                    ThemeBase.Dark,
                    null,
                    new Dictionary<string, string>(StringComparer.Ordinal) { ["Inspector.LabelColumnWidth"] = "200" }));
            HeadlessDriver.Pump();

            Assert.Equal(new GridLength(200), inspector.ColumnDefinitions[0].Width);
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
