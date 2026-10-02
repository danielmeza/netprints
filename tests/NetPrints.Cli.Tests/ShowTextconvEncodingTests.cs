using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NetPrints.Testing;
using Xunit;

namespace NetPrints.Cli.Tests;

/// <summary>US1 scenario 5: the built tool writes its text conversion as UTF-8 without a byte order mark on every OS, so a git diff of non-ASCII names is not mangled.</summary>
public sealed class ShowTextconvEncodingTests : IDisposable
{
    private readonly string _temp = Directory.CreateTempSubdirectory("np-textconv-").FullName;

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    [Fact]
    public async Task ShowTextconvWritesNonAsciiNamesAsUtf8WithoutABom()
    {
        string template = Path.Combine(LocalSdkLayout.FindRepositoryRoot(), "tests", "NetPrints.Core.Tests", "Fixtures", "HelloWorld", "HelloWorld.Program.netpc.json");
        string graph = Path.Combine(_temp, "Grüße.netpc.json");
        await File.WriteAllTextAsync(
            graph,
            (await File.ReadAllTextAsync(template, TestContext.Current.CancellationToken))
                .Replace("\"name\": \"Program\"", "\"name\": \"Grüße\"", StringComparison.Ordinal)
                .Replace("\"name\": \"Main\"", "\"name\": \"Größe\"", StringComparison.Ordinal)
                .Replace("Hello, World!", "日本語", StringComparison.Ordinal),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            TestContext.Current.CancellationToken);

        (int exit, byte[] bytes, string error) = await RunShowTextconvAsync(graph);

        Assert.True(exit == 0, error);
        string text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
        Assert.False(bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }), "The output starts with a UTF-8 byte order mark.");
        Assert.Contains("Grüße", text, StringComparison.Ordinal);
        Assert.Contains("Größe", text, StringComparison.Ordinal);
        Assert.Contains("日本語", text, StringComparison.Ordinal);
    }

    private static async Task<(int Exit, byte[] Output, string Error)> RunShowTextconvAsync(string graph)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in new List<string> { Path.Combine(AppContext.BaseDirectory, "NetPrints.Cli.dll"), "show", "--textconv", graph })
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start) ?? throw new InvalidOperationException("The netprints process did not start.");
        using var output = new MemoryStream();
        Task copy = process.StandardOutput.BaseStream.CopyToAsync(output, TestContext.Current.CancellationToken);
        Task<string> error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        await copy;
        return (process.ExitCode, output.ToArray(), await error);
    }
}
