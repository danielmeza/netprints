using System.Diagnostics;
using System.Text;

namespace NetPrints.Desktop.E2ETests.Hosting;

/// <summary>Runs X11 command-line tools (xdotool, xprop, import) against the private display, and logs every call.</summary>
public sealed class Tool(XServer server)
{
    private readonly StringBuilder log = new();

    public string Log
    {
        get
        {
            lock (log)
            {
                return log.ToString();
            }
        }
    }

    /// <summary>Runs a tool and returns its standard output (bytes), failing on a non-zero exit code.</summary>
    public async Task<byte[]> RunBytesAsync(string file, IEnumerable<string> arguments, CancellationToken cancellationToken, bool check = true,
        TimeSpan? timeout = null)
    {
        var info = new ProcessStartInfo(file)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        server.Apply(info.Environment);
        using var process = Process.Start(info) ?? throw new InvalidOperationException($"Cannot start {file}.");
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(timeout ?? TimeSpan.FromSeconds(30));
        using var output = new MemoryStream();
        var copy = process.StandardOutput.BaseStream.CopyToAsync(output, limit.Token);
        var error = process.StandardError.ReadToEndAsync(limit.Token);
        try
        {
            await process.WaitForExitAsync(limit.Token);
            await copy;
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        string stderr = await error;
        lock (log)
        {
            log.AppendLine($"$ {file} {string.Join(' ', info.ArgumentList)} -> {process.ExitCode} {stderr.Trim()}");
        }

        if (check && process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{file} {string.Join(' ', info.ArgumentList)} exited with {process.ExitCode}: {stderr}");
        }

        return output.ToArray();
    }

    public async Task<string> RunAsync(string file, CancellationToken cancellationToken, params string[] arguments) =>
        Encoding.UTF8.GetString(await RunBytesAsync(file, arguments, cancellationToken)).Trim();

    public async Task<(int ExitCode, string Output)> TryRunAsync(string file, CancellationToken cancellationToken, params string[] arguments)
    {
        var info = new ProcessStartInfo(file) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (string argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        server.Apply(info.Environment);
        using var process = Process.Start(info)!;
        string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        lock (log)
        {
            log.AppendLine($"$ {file} {string.Join(' ', arguments)} -> {process.ExitCode} {output.Trim().Replace('\n', ' ')[..Math.Min(output.Trim().Length, 300)]}");
        }

        return (process.ExitCode, output.Trim());
    }

    public Task<string> XdotoolAsync(CancellationToken cancellationToken, params string[] arguments) => RunAsync("xdotool", cancellationToken, arguments);
}
