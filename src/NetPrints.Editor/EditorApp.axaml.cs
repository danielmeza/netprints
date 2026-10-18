using System.Globalization;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.Extensions.Logging;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.State;
using NetPrints.Extensibility.Settings;
using Semi.Avalonia;
using Ursa.Themes.Semi;

namespace NetPrints.Editor;

/// <summary>
/// The NetPrints editor application (dark Fluent theme, emerald accent, Nodify and Material icons).
/// Hosted by NetPrints.Desktop and by the headless UI tests.
/// </summary>
public partial class EditorApp : Application
{
    /// <summary>The embedded Inter font (Avalonia.Fonts.Inter), used as the default font family.</summary>
    public const string DefaultFontFamily = "avares://Avalonia.Fonts.Inter/Assets#Inter";

    private static readonly Dictionary<EditorTheme, ThemeVariant> Variants = new()
    {
        [EditorTheme.Dark] = ThemeVariant.Dark,
        [EditorTheme.Light] = ThemeVariant.Light,
        [EditorTheme.System] = ThemeVariant.Default,
    };

    private static readonly CultureInfo FallbackLocale = CultureInfo.GetCultureInfo("en-US");

    private static EditorHostServices? hostServices;

    private Style? noTransitions;

    private Theming.ThemeApplier? themeApplier;

    /// <summary>
    /// Process-wide services (logging, extensions, settings, host channel, MSBuild availability). Set by the host (<c>NetPrints.Desktop</c>'s <c>Program</c>) before
    /// <c>StartWithClassicDesktopLifetime</c> runs; read by
    /// <see cref="OnFrameworkInitializationCompleted"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Read before a host has set it.</exception>
    public static EditorHostServices HostServices
    {
        get => hostServices ?? throw new InvalidOperationException(
            $"{nameof(EditorApp)}.{nameof(HostServices)} was read before a host set it.");
        set => hostServices = value;
    }

    /// <summary>
    /// Loads the application's XAML (styles, resources) and configures Nodify's connector gestures
    /// once, before any <see cref="Graph.GraphEditorView"/> is created (REVIEW-NOTE, PR #6): the real
    /// desktop host and the headless UI tests both build this same <see cref="EditorApp"/>, so this is
    /// the one shared place, called once.
    /// </summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        GraphEditorGestures.Configure();
    }

    /// <summary>
    /// Removes all transitions (theme animations), so screenshots and pixel checks are taken in a
    /// settled state. Used by the UI tests and by the desktop host in automation mode.
    /// </summary>
    public void DisableTransitions()
    {
        if (noTransitions is not null)
        {
            return;
        }

        noTransitions = new Style(x => x.Is<Control>())
        {
            Setters = { new Setter(Animatable.TransitionsProperty, null) },
        };
        Styles.Add(noTransitions);
    }

    /// <summary>
    /// Restores the transitions that <see cref="DisableTransitions"/> removed; does nothing when they are on.
    /// </summary>
    public void EnableTransitions()
    {
        if (noTransitions is null)
        {
            return;
        }

        Styles.Remove(noTransitions);
        noTransitions = null;
    }

    /// <summary>
    /// Switches every transition on or off according to <see cref="NetPrintsSettings.EnableAnimations"/>, so a
    /// motion declared in a style needs no switch of its own.
    /// </summary>
    /// <param name="settings">The settings to read the animations flag from.</param>
    public void ApplyAnimationSetting(ISettingsStore settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.Get(NetPrintsSettings.Descriptor).EnableAnimations)
        {
            EnableTransitions();
        }
        else
        {
            DisableTransitions();
        }
    }

    /// <summary>Applies the language stored in the settings file; the operating system's culture when none is.</summary>
    /// <param name="settings">The settings to read the language from.</param>
    public void ApplyLanguageSetting(ISettingsStore settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        string language = settings.Get(NetPrintsSettings.Descriptor).Language;
        CultureInfo culture = language.Length == 0 ? CultureInfo.InstalledUICulture : CultureInfo.GetCultureInfo(language);
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;
        foreach (IStyle style in Styles)
        {
            if (style is SemiTheme semi)
            {
                semi.Locale = culture;
                if (!IsLocale(semi.Locale, culture))
                {
                    semi.Locale = FallbackLocale;
                }
            }
            else if (style is UrsaSemiTheme ursa)
            {
                ursa.Locale = culture;
                if (!IsLocale(ursa.Locale, culture))
                {
                    ursa.Locale = FallbackLocale;
                }
            }
        }
    }

    private static bool IsLocale(CultureInfo? locale, CultureInfo culture) =>
        string.Equals(locale?.Name, culture.Name, StringComparison.OrdinalIgnoreCase);

    /// <summary>Switches the application to a theme (FR-082); <see cref="EditorTheme.System"/> follows the operating system.</summary>
    /// <param name="theme">The theme to switch to.</param>
    public void ApplyTheme(EditorTheme theme) => RequestedThemeVariant = Variants[theme];

    /// <summary>Applies the appearance stored in the settings file: the theme (<see cref="EditorTheme.Dark"/> when none is) and the preset of each variant.</summary>
    /// <param name="settings">The settings to read the appearance from.</param>
    public void ApplyThemeSetting(ISettingsStore settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        themeApplier ??= new Theming.ThemeApplier(this, hostServices?.LoggerFactory.CreateLogger(nameof(EditorApp)) ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
        themeApplier.Apply(settings.Get(EditorSettings.Descriptor));
    }

    /// <summary>
    /// On a classic desktop lifetime: composes the editor's services, creates and shows the shell
    /// window, installs the unhandled-exception handler, and, when <c>NETPRINTS_AUTOMATION=1</c>,
    /// starts the automation agent (disabling UI transitions first, for settled screenshots) and
    /// exits loudly if it fails to start. Opens the project named on the command line, if any.
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            ApplyLanguageSetting(HostServices.Settings);
            ApplyAnimationSetting(HostServices.Settings);
            ApplyThemeSetting(HostServices.Settings);

            if (HostServices.Settings is ISettingsChangeSource changes)
            {
                IDisposable animations = changes.OnSectionChanged(NetPrintsSettings.SectionId, () => Avalonia.Threading.Dispatcher.UIThread.Post(() => ApplyAnimationSetting(HostServices.Settings)));
                IDisposable theme = changes.OnSectionChanged(EditorSettings.SectionId, () => Avalonia.Threading.Dispatcher.UIThread.Post(() => ApplyThemeSetting(HostServices.Settings)));
                desktop.Exit += (_, _) =>
                {
                    animations.Dispose();
                    theme.Dispose();
                };
            }

            var composition = new EditorComposition(HostServices);
            var exceptionHandler = composition.InstallUnhandledExceptionHandler();
            desktop.Exit += (_, _) => exceptionHandler.Dispose();
            var window = composition.CreateShellWindow();
            desktop.MainWindow = window;
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;

            var shutdownLogger = HostServices.LoggerFactory.CreateLogger(nameof(EditorApp));
            var shutdownCoordinator = new ShutdownCoordinator(
                async () =>
                {
                    await composition.FlushBackupsAsync();
                    composition.Dispose();
                    await HostServices.DisposeAsync();
                    Hosting.Log.HostServicesDisposed(shutdownLogger);
                },
                () => desktop.Shutdown(),
                shutdownLogger,
                () => composition.ConfirmExitAsync(CancellationToken.None));
            desktop.ShutdownRequested += (_, e) => shutdownCoordinator.OnShutdownRequested(e);

            // Automation mode (E2E tests only): settled screenshots and a read-only agent.
            if (AutomationAgent.IsEnabled(out string pipeName))
            {
                DisableTransitions();
                var tree = new AutomationTree();
                tree.Track(window);
                string? startupProject = Hosting.StartupArguments.Parse(desktop.Args).ProjectPath;
                AutomationAgent agent;
                try
                {
                    agent = new AutomationAgent(pipeName, tree, () => new AutomationStatus(
                        window.IsVisible,
                        composition.Shell?.Session is not null,
                        composition.Context.Reflection.IsLoaded,
                        startupProject,
                        Environment.ProcessId),
                        HostServices.LoggerFactory.CreateLogger<AutomationAgent>(),
                        composition.Context.RunState.Snapshot);
                }
                catch (Exception e)
                {
                    // NETPRINTS_AUTOMATION was asked for and could not be honored: fail loudly and
                    // fast (Critical goes to stderr through the Desktop console logger, then a
                    // non-zero exit) instead of leaving a caller waiting on a pipe that will never
                    // accept a connection. This runs before the dispatcher loop starts, so it never
                    // reaches the unhandled-exception dialog.
                    Hosting.Log.AutomationAgentStartFailed(HostServices.LoggerFactory.CreateLogger(nameof(EditorApp)), e, pipeName);
                    throw;
                }

                desktop.Exit += (_, _) =>
                {
                    agent.Dispose();
                    tree.Dispose();
                };
            }

            // Startup problems first, then a single command-line argument is a project to open (FR-016, PAR-05).
            // Not a command, and StartAsync has no catch of its own: route a fault to the error dialog too.
            composition.StartAsync(desktop.Args ?? []).Forget(composition.Context, "Failed to start the editor");
        }

        base.OnFrameworkInitializationCompleted();
    }
}
