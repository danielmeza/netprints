using System;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace NetPrints.Desktop.Logging;

/// <summary>Records an unhandled exception on any thread as a Critical entry, flushes with a bound and disposes the factory (ADR-0033).</summary>
/// <param name="logger">The logger the entry is written to.</param>
/// <param name="session">The session log flushed and closed; the handler is its last user when the process dies.</param>
/// <param name="flushTimeout">The longest the handler waits for pending lines, in place of NLog's 15 second default.</param>
internal sealed class CrashHandler(ILogger logger, SessionLog session, TimeSpan flushTimeout)
{
    /// <summary>The flush bound used by the editor.</summary>
    public static readonly TimeSpan DefaultFlushTimeout = TimeSpan.FromSeconds(3);

    /// <summary>Handles <see cref="AppDomain.UnhandledException"/>.</summary>
    /// <param name="sender">The raiser, ignored.</param>
    /// <param name="e">The exception object and whether the runtime terminates.</param>
    public void Handle(object? sender, UnhandledExceptionEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        Exception? exception = e.ExceptionObject as Exception;
        Log.UnhandledException(logger, exception, e.IsTerminating, exception?.Message ?? Convert.ToString(e.ExceptionObject, CultureInfo.InvariantCulture) ?? string.Empty);
        session.FlushAndClose(flushTimeout);
    }
}
