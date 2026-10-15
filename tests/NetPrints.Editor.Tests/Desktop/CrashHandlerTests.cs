using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NetPrints.Desktop.Logging;
using NLog.Config;
using NLog.Extensions.Logging;
using NLog.Targets;
using NLog.Targets.Wrappers;
using LogEventInfo = NLog.LogEventInfo;
using LogFactory = NLog.LogFactory;

namespace NetPrints.Editor.Tests.Desktop;

/// <summary>ADR-0033: an unhandled exception on any thread leaves a Critical line and flushes within a bound.</summary>
public sealed class CrashHandlerTests : IDisposable
{
    private readonly string temp = Path.Combine(Path.GetTempPath(), "netprints-crash-" + Guid.NewGuid().ToString("N"));

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

    [Fact]
    public void AnUnhandledExceptionWritesACriticalLineBeforeTheFactoryIsDisposed()
    {
        var session = SessionLog.Start(temp, TimeProvider.System, 1, 20);
        using ILoggerFactory loggers = session.CreateLoggerFactory(LogLevel.Information);
        var handler = new CrashHandler(loggers.CreateLogger("Crash"), session, TimeSpan.FromSeconds(5));

        handler.Handle(null, new UnhandledExceptionEventArgs(new InvalidOperationException("background boom"), isTerminating: true));

        string text = ReadShared(session.FilePath);
        Assert.Contains("FATAL", text, StringComparison.Ordinal);
        Assert.Contains("background boom", text, StringComparison.Ordinal);
        session.Dispose();
    }

    [Fact]
    public void ANonExceptionObjectIsStillRecorded()
    {
        var session = SessionLog.Start(temp, TimeProvider.System, 2, 20);
        using ILoggerFactory loggers = session.CreateLoggerFactory(LogLevel.Information);
        var handler = new CrashHandler(loggers.CreateLogger("Crash"), session, TimeSpan.FromSeconds(5));

        handler.Handle(null, new UnhandledExceptionEventArgs("a plain string", isTerminating: true));

        Assert.Contains("a plain string", ReadShared(session.FilePath), StringComparison.Ordinal);
        session.Dispose();
    }

    [Fact]
    public void AStalledTargetDelaysTheHandlerByTheBoundNotByTheDefaultFifteenSeconds()
    {
        using var gate = new ManualResetEventSlim();
        var stalled = new StalledTarget(gate) { Name = "stalled" };
        var wrapper = new AsyncTargetWrapper("async", stalled) { QueueLimit = 100, OverflowAction = AsyncTargetWrapperOverflowAction.Discard };
        var factory = new LogFactory();
        var configuration = new LoggingConfiguration(factory);
        configuration.AddTarget(wrapper);
        configuration.AddRuleForAllLevels(wrapper);
        factory.Configuration = configuration;
        using ILoggerFactory loggers = LoggerFactory.Create(builder => builder.AddNLog(new NLogProviderOptions(), _ => factory));
        var handler = new CrashHandler(loggers.CreateLogger("Crash"), SessionLog.Own(factory, "stalled.log"), TimeSpan.FromMilliseconds(300));

        var clock = Stopwatch.StartNew();
        handler.Handle(null, new UnhandledExceptionEventArgs(new InvalidOperationException("stuck"), isTerminating: true));
        clock.Stop();

        gate.Set();
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(8), $"The handler took {clock.Elapsed}.");
    }

    private sealed class StalledTarget(ManualResetEventSlim gate) : TargetWithLayout
    {
        protected override void Write(LogEventInfo logEvent) => gate.WaitHandle.WaitOne(TimeSpan.FromSeconds(30));
    }
}
