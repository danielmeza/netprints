using System.Diagnostics;
using System.Text.RegularExpressions;
using NetPrints.Desktop.E2ETests.Hosting;

namespace NetPrints.Desktop.E2ETests.Driving;

/// <summary>
/// A function key reaches a client with only the modifiers of its chord, on a keymap whose function
/// key type lists Alt at the base level, as xkeyboard-config has since 2.46. The test adds that line
/// to any other keymap, so it holds on every host.
/// </summary>
public sealed partial class FunctionKeyTests
{
    private const string AltAtTheBaseLevel = "map[Alt]= Level1;";

    private static readonly TimeSpan DeliveryLimit = TimeSpan.FromSeconds(10);

    [GeneratedRegex(@"(type ""CTRL\+ALT"" \{\s*modifiers= [^;]*;)")]
    private static partial Regex FunctionKeyType();

    [GeneratedRegex(@"state (0x[0-9a-f]+), keycode \d+ \(keysym 0x[0-9a-f]+, (\w+)\)")]
    private static partial Regex KeyEvent();

    [Fact]
    public async Task AFunctionKeyArrivesWithOnlyTheModifiersOfItsChord()
    {
        Assert.SkipUnless(XServer.IsEnabled, $"Desktop E2E tests run with {XServer.EnableVariable}=1.");
        var token = TestContext.Current.CancellationToken;
        await using var server = new XServer();
        await server.InitializeAsync();
        var tool = new Tool(server);
        await PutAltAtTheBaseLevelAsync(server, tool, token);

        var info = new ProcessStartInfo("xev")
        {
            ArgumentList = { "-event", "keyboard" },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        server.Apply(info.Environment);
        using var receiver = Process.Start(info) ?? throw new InvalidOperationException("Cannot start xev.");
        try
        {
            string window = (await tool.XdotoolAsync(token, "search", "--sync", "--name", "^Event Tester$")).Split('\n')[0];
            await tool.XdotoolAsync(token, "windowactivate", "--sync", window);

            foreach (var (chord, key, state) in new[] { ("F7", "F7", "0x0"), ("F2", "F2", "0x0"), ("Shift+F5", "F5", "0x1") })
            {
                await tool.XdotoolAsync(token, "key", "--clearmodifiers", X11Driver.KeyOf(chord, server.DisplayName));
                Assert.Equal(state, await PressStateAsync(receiver.StandardOutput, key, token));
            }
        }
        finally
        {
            receiver.Kill();
            await receiver.WaitForExitAsync(CancellationToken.None);
        }
    }

    private static async Task PutAltAtTheBaseLevelAsync(XServer server, Tool tool, CancellationToken cancellationToken)
    {
        string path = Path.Combine(server.Home, "keymap.xkb");
        await tool.RunAsync("xkbcomp", cancellationToken, "-xkb", server.DisplayName, path);
        string keymap = await File.ReadAllTextAsync(path, cancellationToken);
        if (!keymap.Contains(AltAtTheBaseLevel, StringComparison.Ordinal))
        {
            // xkbcomp drops a base level entry that preserves nothing.
            await File.WriteAllTextAsync(path, FunctionKeyType().Replace(keymap, $"$1\n{AltAtTheBaseLevel}\npreserve[Alt]= Alt;"), cancellationToken);
            await tool.RunAsync("xkbcomp", cancellationToken, "-w", "0", path, server.DisplayName);
            await tool.RunAsync("xkbcomp", cancellationToken, "-xkb", server.DisplayName, path);
            Assert.Contains(AltAtTheBaseLevel, await File.ReadAllTextAsync(path, cancellationToken), StringComparison.Ordinal);
        }
    }

    /// <summary>Reads the receiver's output up to the press of <paramref name="key"/> and returns the modifier state that came with it.</summary>
    private static async Task<string> PressStateAsync(StreamReader output, string key, CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(DeliveryLimit);
        bool press = false;
        while (await output.ReadLineAsync(limit.Token) is { } line)
        {
            press = line.StartsWith("KeyPress", StringComparison.Ordinal) || (press && !line.StartsWith("KeyRelease", StringComparison.Ordinal));
            var match = KeyEvent().Match(line);
            if (press && match.Success && match.Groups[2].Value == key)
            {
                return match.Groups[1].Value;
            }
        }

        throw new InvalidOperationException($"xev exited before the press of {key}.");
    }
}
