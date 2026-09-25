using Avalonia;
using Avalonia.Headless;
using Avalonia.Media;
using NetPrints.Editor.UITests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

// All UI tests share one headless UI thread.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace NetPrints.Editor.UITests;

/// <summary>The real <see cref="EditorApp"/> on the headless platform with Skia rendering.</summary>
public static class TestAppBuilder
{
    /// <summary>Per-test timeout of the UI tests, in milliseconds.</summary>
    public const int Timeout = 90_000;

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<EditorApp>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .WithInterFont()
            // No system fonts are needed (clean CI images and containers have none).
            .With(new FontManagerOptions { DefaultFamilyName = EditorApp.DefaultFontFamily })
            .AfterSetup(builder => ((EditorApp)builder.Instance!).DisableTransitions());
}
