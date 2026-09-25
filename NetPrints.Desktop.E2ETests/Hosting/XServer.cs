using System.Diagnostics;
using System.Globalization;

namespace NetPrints.Desktop.E2ETests.Hosting;

/// <summary>
/// A private X server for the E2E run: Xvfb on a free display number (100 and up, never the
/// user's desktop) at 1600x1000x24, 96 DPI, with the openbox window manager. Started once per
/// run (collection fixture), like <c>xvfb-run -a</c>, plus a window manager.
/// </summary>
public sealed class XServer : IAsyncLifetime
{
    public const int Width = 1600;
    public const int Height = 1000;

    private Process? xvfb;
    private Process? windowManager;

    /// <summary>The variable that enables the E2E tests (they need Xvfb, openbox, xdotool, ImageMagick and GTK 3).</summary>
    public const string EnableVariable = "NETPRINTS_E2E";

    /// <summary>Whether E2E tests run in this environment; otherwise they are skipped.</summary>
    public static bool IsEnabled => Environment.GetEnvironmentVariable(EnableVariable) == "1";

    public int Display { get; private set; }

    public string DisplayName => ":" + Display.ToString(CultureInfo.InvariantCulture);

    /// <summary>A private home for the editor and GTK (settings, recent files), deleted at the end.</summary>
    public string Home { get; } = Directory.CreateTempSubdirectory("netprints-e2e-home-").FullName;

    /// <summary>
    /// The environment of every process on this display: the private DISPLAY, no D-Bus session
    /// (so file pickers are GTK dialogs on this display, not portals on the user's desktop), a
    /// private home, UTC and the invariant culture.
    /// </summary>
    public void Apply(IDictionary<string, string?> environment)
    {
        environment["DISPLAY"] = DisplayName;
        environment.Remove("DBUS_SESSION_BUS_ADDRESS");
        environment.Remove("WAYLAND_DISPLAY");
        environment.Remove("XAUTHORITY");
        environment["NO_AT_BRIDGE"] = "1";

        // A named cursor theme, so XFixes reports cursor names ("fleur", "left_ptr").
        environment["XCURSOR_THEME"] = "Adwaita";
        environment["XCURSOR_SIZE"] = "24";
        environment["GTK_USE_PORTAL"] = "0";
        environment["GDK_BACKEND"] = "x11";
        environment["HOME"] = Home;
        environment["XDG_CONFIG_HOME"] = Path.Combine(Home, ".config");
        environment["XDG_DATA_HOME"] = Path.Combine(Home, ".local", "share");
        environment["XDG_CACHE_HOME"] = Path.Combine(Home, ".cache");
        environment["XDG_RUNTIME_DIR"] = Path.Combine(Home, "run");
        environment["TZ"] = "UTC";
        environment["LANG"] = "C.UTF-8";
        environment["LC_ALL"] = "C.UTF-8";
        environment["DOTNET_SYSTEM_GLOBALIZATION_INVARIANT"] = "1";
        environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        environment["DOTNET_NOLOGO"] = "1";
    }

    private static int FreeDisplay()
    {
        for (int display = 100; display < 200; display++)
        {
            if (!File.Exists($"/tmp/.X{display}-lock") && !File.Exists($"/tmp/.X11-unix/X{display}"))
            {
                return display;
            }
        }

        throw new InvalidOperationException("No free X display number between 100 and 199.");
    }

    public async ValueTask InitializeAsync()
    {
        if (!IsEnabled)
        {
            return;
        }

        Directory.CreateDirectory(Path.Combine(Home, "run"));
        File.SetUnixFileMode(Path.Combine(Home, "run"), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        for (int attempt = 0; attempt < 5 && xvfb is null; attempt++)
        {
            Display = FreeDisplay();
            var info = new ProcessStartInfo("Xvfb")
            {
                ArgumentList = { DisplayName, "-screen", "0", $"{Width}x{Height}x24", "-dpi", "96", "-nolisten", "tcp", "-noreset" },
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
            var process = Process.Start(info)!;
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();

            // Infrastructure wait (the only kind that may retry): the X socket appears.
            var started = Stopwatch.StartNew();
            while (!File.Exists($"/tmp/.X11-unix/X{Display}") && !process.HasExited && started.Elapsed < TimeSpan.FromSeconds(10))
            {
                await Task.Delay(50);
            }

            if (process.HasExited || !File.Exists($"/tmp/.X11-unix/X{Display}"))
            {
                process.Dispose();
                continue; // another run took the display: try the next one
            }

            xvfb = process;
        }

        if (xvfb is null)
        {
            throw new InvalidOperationException("Xvfb did not start.");
        }

        var wm = new ProcessStartInfo("openbox") { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
        Apply(wm.Environment);
        windowManager = Process.Start(wm)!;
        windowManager.BeginErrorReadLine();
        windowManager.BeginOutputReadLine();

        // Infrastructure wait: the window manager owns the screen (it sets _NET_SUPPORTING_WM_CHECK).
        var tool = new Tool(this);
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < TimeSpan.FromSeconds(10))
        {
            var (code, output) = await tool.TryRunAsync("xprop", CancellationToken.None, "-root", "_NET_SUPPORTING_WM_CHECK");
            if (code == 0 && output.Contains("window id", StringComparison.Ordinal))
            {
                return;
            }

            await Task.Delay(50);
        }

        throw new InvalidOperationException("openbox did not start.");
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var process in new[] { windowManager, xvfb })
        {
            if (process is { HasExited: false })
            {
                // SIGTERM, so Xvfb removes its lock file and socket; SIGKILL if it does not exit.
                using (var term = Process.Start("kill", ["-TERM", process.Id.ToString(CultureInfo.InvariantCulture)]))
                {
                    await term.WaitForExitAsync();
                }

                using var grace = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try
                {
                    await process.WaitForExitAsync(grace.Token);
                }
                catch (OperationCanceledException)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync();
                }
            }

            process?.Dispose();
        }

        try
        {
            Directory.Delete(Home, true);
        }
        catch (IOException)
        {
            // Best effort.
        }
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DesktopCollection : ICollectionFixture<XServer>
{
    public const string Name = "Desktop";
}
