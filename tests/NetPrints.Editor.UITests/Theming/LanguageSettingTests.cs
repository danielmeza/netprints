using System.Globalization;
using Avalonia;
using Avalonia.Headless.XUnit;
using NetPrints.Extensibility.Settings;
using Semi.Avalonia;
using Ursa.Themes.Semi;

namespace NetPrints.Editor.UITests.Theming;

/// <summary>FR-027, FR-044: <c>netprints.language</c> sets the UI culture at start, and Semi and Ursa follow the app culture where they have the locale, else English; a language change applies after a restart.</summary>
public class LanguageSettingTests
{
    private static EditorApp App => Assert.IsType<EditorApp>(Application.Current);

    private static SemiTheme Semi => App.Styles.OfType<SemiTheme>().Single();

    private static UrsaSemiTheme Ursa => App.Styles.OfType<UrsaSemiTheme>().Single();

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [InlineData("es-ES", "es-ES", "en-US")]
    [InlineData("de-DE", "de-DE", "de-DE")]
    [InlineData("en-US", "en-US", "en-US")]
    [InlineData("es-MX", "en-US", "en-US")]
    public void TheStoredLanguageSetsTheUiCultureAndTheThemeLocales(string language, string semiLocale, string ursaLocale)
    {
        CultureInfo? before = CultureInfo.DefaultThreadCurrentUICulture;
        CultureInfo current = CultureInfo.CurrentUICulture;
        try
        {
            App.ApplyLanguageSetting(new LanguageSettings(language));

            Assert.Equal(language, CultureInfo.CurrentUICulture.Name);
            Assert.Equal(language, CultureInfo.DefaultThreadCurrentUICulture?.Name);
            Assert.Equal(semiLocale, Semi.Locale?.Name);
            Assert.Equal(ursaLocale, Ursa.Locale?.Name);
        }
        finally
        {
            CultureInfo.DefaultThreadCurrentUICulture = before;
            CultureInfo.CurrentUICulture = current;
            App.ApplyLanguageSetting(new LanguageSettings("en-US"));
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void AnEmptyLanguageFollowsTheOperatingSystem()
    {
        CultureInfo? before = CultureInfo.DefaultThreadCurrentUICulture;
        CultureInfo current = CultureInfo.CurrentUICulture;
        try
        {
            App.ApplyLanguageSetting(new LanguageSettings("es-ES"));

            App.ApplyLanguageSetting(new LanguageSettings(""));

            Assert.Equal(CultureInfo.InstalledUICulture.Name, CultureInfo.CurrentUICulture.Name);
        }
        finally
        {
            CultureInfo.DefaultThreadCurrentUICulture = before;
            CultureInfo.CurrentUICulture = current;
            App.ApplyLanguageSetting(new LanguageSettings("en-US"));
        }
    }

    private sealed class LanguageSettings(string language) : ISettingsStore
    {
        public T Get<T>(ExtensionSettingsDescriptor<T> descriptor) =>
            (object)new NetPrintsSettings { Language = language } is T value ? value : descriptor.Default;

        public ValueTask SetAsync<T>(ExtensionSettingsDescriptor<T> descriptor, T value, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
