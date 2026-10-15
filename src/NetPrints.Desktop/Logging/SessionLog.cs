using System;
using System.Globalization;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;
using NLog.Targets;
using NLog.Targets.Wrappers;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace NetPrints.Desktop.Logging;

/// <summary>The session log file and the NLog factory that writes it (ADR-0033).</summary>
internal sealed class SessionLog : IDisposable
{
    /// <summary>The number of earlier session files the start-up sweep keeps.</summary>
    public const int DefaultRetainedSessions = 20;

    /// <summary>The capacity of the asynchronous queue; a full queue blocks the caller instead of dropping events.</summary>
    public const int QueueLimit = 10_000;

    /// <summary>The number of events written per batch.</summary>
    public const int BatchSize = 200;

    private const string FileNamePrefix = "netprints-";
    private const string FileNameExtension = ".log";
    private const string FileNameTimestamp = "yyyyMMdd-HHmmss";
    private const string LineLayout =
        "${longdate}|${level:uppercase=true:padding=-5}|${logger}|${message}${onexception:inner=${newline}${exception:format=tostring}}";

    private SessionLog(LogFactory factory, string filePath)
    {
        Factory = factory;
        FilePath = filePath;
    }

    /// <summary>Gets the isolated NLog factory; this instance owns and disposes it.</summary>
    public LogFactory Factory { get; }

    /// <summary>Gets the path of this session's log file.</summary>
    public string FilePath { get; }

    /// <summary>Gets the glob that matches the session files and only them (see <see cref="LogRetention"/>).</summary>
    internal static string FileNameGlob => FileNamePrefix + "*" + FileNameExtension;

    /// <summary>Starts a session log: sweeps the older files, then opens this session's file lazily on its first line.</summary>
    /// <param name="logDirectory">The folder of the session files.</param>
    /// <param name="time">The clock that names the file.</param>
    /// <param name="processId">The process id that names the file.</param>
    /// <param name="retainedSessions">How many earlier session files the start-up sweep keeps.</param>
    /// <returns>The started session log.</returns>
    public static SessionLog Start(string logDirectory, TimeProvider time, int processId, int retainedSessions)
    {
        ArgumentException.ThrowIfNullOrEmpty(logDirectory);
        ArgumentNullException.ThrowIfNull(time);
        LogRetention.Sweep(logDirectory, retainedSessions);

        string stamp = time.GetUtcNow().ToString(FileNameTimestamp, CultureInfo.InvariantCulture);
        string filePath = System.IO.Path.Combine(logDirectory, FormattableString.Invariant($"{FileNamePrefix}{stamp}-{processId}{FileNameExtension}"));

        var factory = new LogFactory { ThrowConfigExceptions = true };
        factory.Setup().LoadConfiguration(builder => builder.ForLogger()
            .WriteToFile(filePath, LineLayout, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), keepFileOpen: true)
            .WithAsync(AsyncTargetWrapperOverflowAction.Block, QueueLimit, BatchSize));
        return new SessionLog(factory, filePath);
    }

    /// <summary>Creates the Microsoft logging factory that writes to this session's file.</summary>
    /// <param name="minimumLevel">The lowest level logged.</param>
    /// <param name="configure">Adds further providers, or null.</param>
    /// <returns>The factory; the caller disposes it before this instance.</returns>
    public ILoggerFactory CreateLoggerFactory(LogLevel minimumLevel, Action<ILoggingBuilder>? configure = null) =>
        LoggerFactory.Create(builder =>
        {
            configure?.Invoke(builder);
            builder.AddNLog(new NLogProviderOptions(), _ => Factory);
            builder.SetMinimumLevel(minimumLevel);
            builder.AddFilter<NLogLoggerProvider>(null, minimumLevel);
        });

    /// <summary>Wraps a factory this instance then owns, for tests that bring their own targets.</summary>
    /// <param name="factory">The factory; this instance disposes it.</param>
    /// <param name="filePath">The path reported as the session file.</param>
    /// <returns>The session log.</returns>
    internal static SessionLog Own(LogFactory factory, string filePath) => new(factory, filePath);

    /// <summary>
    /// Flushes pending lines, then disposes the factory, waiting at most <paramref name="bound"/> for each step; used when the
    /// process is dying. Disposing flushes again, so it runs on its own thread and is abandoned when a stalled disk holds it.
    /// </summary>
    /// <param name="bound">The longest wait for the flush and for the disposal.</param>
    public void FlushAndClose(TimeSpan bound)
    {
        Factory.Flush(bound);
        var closer = new Thread(Factory.Dispose) { IsBackground = true, Name = "NetPrints log close" };
        closer.Start();
        closer.Join(bound);
    }

    /// <inheritdoc/>
    public void Dispose() => Factory.Dispose();
}
