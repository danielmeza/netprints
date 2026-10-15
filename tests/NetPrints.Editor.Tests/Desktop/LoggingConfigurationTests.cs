using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NetPrints.Desktop.Logging;
using NLog.Targets.Wrappers;
using DesktopProgram = NetPrints.Desktop.Program;

namespace NetPrints.Editor.Tests.Desktop;

/// <summary>ADR-0033: the real NLog configuration, the one-file-per-session naming and the retention sweep.</summary>
public sealed class LoggingConfigurationTests : IDisposable
{
    private static readonly TimeSpan FlushBound = TimeSpan.FromSeconds(10);

    private readonly string temp = Path.Combine(Path.GetTempPath(), "netprints-logging-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(temp))
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    private static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private string Touch(string name)
    {
        Directory.CreateDirectory(temp);
        string path = Path.Combine(temp, name);
        File.WriteAllText(path, "x");
        return path;
    }

    [Fact]
    public void TheConfigurationIsAnAsyncBlockingQueueOf10000AndThrowsConfigExceptions()
    {
        using var session = SessionLog.Start(temp, new FakeTimeProvider(), 1, 20);

        Assert.True(session.Factory.ThrowConfigExceptions);
        var configuration = Assert.IsAssignableFrom<NLog.Config.LoggingConfiguration>(session.Factory.Configuration);
        var wrapper = Assert.Single(configuration.AllTargets.OfType<AsyncTargetWrapper>());
        Assert.Equal(10_000, wrapper.QueueLimit);
        Assert.Equal(AsyncTargetWrapperOverflowAction.Block, wrapper.OverflowAction);
        Assert.Equal(200, wrapper.BatchSize);
    }

    [Fact]
    public void ALoggedLineReachesTheSessionFileWithItsLevelCategoryAndMessage()
    {
        using var session = SessionLog.Start(temp, new FakeTimeProvider(), 1, 20);
        using ILoggerFactory loggers = session.CreateLoggerFactory(LogLevel.Information);

        loggers.CreateLogger("Sample.Category").LogWarning("hello {Name}", "world");
        session.Factory.Flush(FlushBound);

        string text = ReadShared(session.FilePath);
        Assert.Contains("WARN", text, StringComparison.Ordinal);
        Assert.Contains("Sample.Category", text, StringComparison.Ordinal);
        Assert.Contains("hello world", text, StringComparison.Ordinal);
        Assert.Equal(temp, Path.GetDirectoryName(session.FilePath));
    }

    [Fact]
    public void LinesBelowTheMinimumLevelAreNotWritten()
    {
        using var session = SessionLog.Start(temp, new FakeTimeProvider(), 1, 20);
        using ILoggerFactory loggers = session.CreateLoggerFactory(LogLevel.Warning);

        loggers.CreateLogger("Sample").LogInformation("quiet");
        loggers.CreateLogger("Sample").LogError("loud");
        session.Factory.Flush(FlushBound);

        string text = ReadShared(session.FilePath);
        Assert.DoesNotContain("quiet", text, StringComparison.Ordinal);
        Assert.Contains("loud", text, StringComparison.Ordinal);
    }

    [Fact]
    public void EachSessionWritesItsOwnFile()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 14, 9, 0, 0, TimeSpan.Zero));
        string first;
        using (var one = SessionLog.Start(temp, clock, 100, 20))
        {
            using ILoggerFactory loggers = one.CreateLoggerFactory(LogLevel.Information);
            loggers.CreateLogger("A").LogInformation("first session");
            one.Factory.Flush(FlushBound);
            first = one.FilePath;
        }

        clock.Advance(TimeSpan.FromMinutes(5));
        using var two = SessionLog.Start(temp, clock, 101, 20);
        using ILoggerFactory second = two.CreateLoggerFactory(LogLevel.Information);
        second.CreateLogger("A").LogInformation("second session");
        two.Factory.Flush(FlushBound);

        Assert.NotEqual(first, two.FilePath);
        Assert.Equal(2, Directory.GetFiles(temp).Length);
        Assert.DoesNotContain("second session", ReadShared(first), StringComparison.Ordinal);
        Assert.Contains("second session", ReadShared(two.FilePath), StringComparison.Ordinal);
    }

    [Fact]
    public void TheSweepKeepsTheNewestSessionFilesAndNeverTouchesOtherFiles()
    {
        string[] sessions =
        [
            "netprints-20261010-080000-10.log",
            "netprints-20261011-080000-11.log",
            "netprints-20261012-080000-12.log",
            "netprints-20261013-080000-13.log",
            "netprints-20261014-080000-14.log",
        ];
        foreach (string name in sessions)
        {
            Touch(name);
        }

        string foreign = Touch("notes.txt");
        string lookalike = Touch("netprints-latest.log");
        string other = Touch("other-20261001-080000-1.log");

        int deleted = LogRetention.Sweep(temp, 2);

        Assert.Equal(3, deleted);
        Assert.Equal(sessions[3..].Select(name => Path.Combine(temp, name)).Order(StringComparer.Ordinal),
            Directory.GetFiles(temp).Except([foreign, lookalike, other]).Order(StringComparer.Ordinal));
        Assert.True(File.Exists(foreign));
        Assert.True(File.Exists(lookalike));
        Assert.True(File.Exists(other));
    }

    [Fact]
    public void ACriticalLineGoesToStderrAndNormalOutputStaysOnStdout()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => DesktopProgram.AddConsole(builder));
        using ServiceProvider provider = services.BuildServiceProvider();

        LogLevel threshold = provider.GetRequiredService<IOptions<ConsoleLoggerOptions>>().Value.LogToStandardErrorThreshold;

        Assert.True(threshold <= LogLevel.Critical, "A Critical line must reach stderr (ADR-0033 decision 8).");
        Assert.True(threshold > LogLevel.Information, "Normal output must stay on stdout.");
    }

    [Fact]
    public void TheSweepOfAMissingFolderDoesNothing() =>
        Assert.Equal(0, LogRetention.Sweep(Path.Combine(temp, "missing"), 3));

    [Fact]
    public void StartingASessionSweepsOlderFilesBeyondTheRetention()
    {
        for (int day = 1; day <= 5; day++)
        {
            Touch($"netprints-2026100{day}-080000-1.log");
        }

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 14, 9, 0, 0, TimeSpan.Zero));
        using var session = SessionLog.Start(temp, clock, 7, 2);

        string[] names = [.. Directory.GetFiles(temp).Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal)];
        Assert.Equal(["netprints-20261004-080000-1.log", "netprints-20261005-080000-1.log"], names.Where(name => name != Path.GetFileName(session.FilePath)));
    }
}
