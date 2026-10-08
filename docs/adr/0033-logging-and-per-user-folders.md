# 0033: Logging goes through NLog 6 behind `ILogger`, and per-user files live in per-OS folders

## Status

Accepted (2026-10-08, P3a planning; implemented by the sub-phase H logging task). No number was reserved for this
topic, so it takes the first free one after the reserved 0024-0032.

## Context

- The code logs through `Microsoft.Extensions.Logging` and `[LoggerMessage]` partials (AGENTS.md "Logging and
  naming"), but there is no file provider: users on Windows have no log at all, and an exception on a non-UI thread
  leaves no trace. Only `Dispatcher.UIThread.UnhandledException` and `TaskScheduler.UnobservedTaskException` are
  subscribed; nothing subscribes to `AppDomain.UnhandledException`.
- State and backups live today under `<ApplicationData>/NetPrints/{state,backups}` (`EditorDataPaths`): `~/.config`
  on Linux and the roaming `%APPDATA%` on Windows. Roaming is documented as not meant for large or
  device-specific data, and the XDG base directory specification 0.8 puts logs, history, layout, open files and undo
  history in the state directory. Moving only the logs would split the layout.
- A research pass and an adversarial audit compared NLog, ZLogger, Serilog, Karambolo.Extensions.Logging.File,
  NReco.Logging.File and a provider of our own. The editor runs JIT and untrimmed, so AOT behaviour is not a reason
  for or against a provider here. Measured on JIT, 100k events from a caller thread: ZLogger 54-83 ms, MEL console
  69-73 ms, NLog (blocking, queue 10 000) 155-172 ms and 660 B per message, Serilog (blocking) 212-234 ms. NLog's
  cost is about 1.5-1.9 microseconds per message, which no editor path approaches.

## Decision

### Provider

1. **NLog 6** (`NLog` and `NLog.Extensions.Logging` 6.2.1) behind the existing `ILogger` and `[LoggerMessage]` API.
   Nothing outside `NetPrints.Desktop` references NLog.
2. **Configured in code, with an isolated `LogFactory`.** `NLog.LogManager` is banned. `AddNLog(Func<..., LogFactory>)`
   still touches `LogManager.LogFactory` internally, so the ban stays useful.
3. **`AsyncTargetWrapper`**, queue 10 000, overflow action Block (the default, Discard, drops events under
   pressure), batch 200.
4. **One file per session** plus an owned retention sweep at start-up. `MaxArchiveFiles` defaults to -1 (disabled)
   and 0 means "keep none", so the sweep is ours and the setting is not relied on.
5. **Crash handler.** `AppDomain.CurrentDomain.UnhandledException` logs Critical and flushes with a bound
   (`LogFactory.Flush(TimeSpan)`, never the 15 s default, which waits on a stalled disk), then disposes the factory.
   `ProcessExit` is never raised on an unhandled exception, so NLog's own auto-shutdown does not help; measured
   without a handler, about 4.5% of events were lost with a JSON layout. Whether to keep the process alive after an
   exception on an extension thread, through the .NET 10 `ExceptionHandling.SetUnhandledExceptionHandler`, is a policy
   question left to the extension-isolation work (ADR-0031); the handler here only records and flushes.
6. **A configuration test** renders a line through the real configuration with `ThrowConfigExceptions` on, so a
   layout typo fails the test.
7. **Bans (RS0030) in `src/BannedSymbols.txt`** for ad-hoc debug output: `File.AppendAll*` (each overload on its own
   line, since the format has no wildcard), `Trace.Write*`, `Debug.Write*`, and `NLog.LogManager`. None has a current
   use. `Console` stays allowed in the entry points. A machine-path hygiene test (no `/home/`, `/mnt/` or scratch
   paths in repo files) complements them.
8. **`EditorApp.axaml.cs`** keeps its unhandled-startup message on **stderr**: its comment says the output is
   intentional ("fail loudly"), and MEL's console logger writes to stdout unless `LogToStandardErrorThreshold` is set.
   It is either left as is or routed through `[LoggerMessage]` with that threshold set; it is never moved to stdout.

### Why not the others

- **ZLogger:** no release since 2025-01-06, no retention option, and its size-based rolling splits the last record of
  every file across two files (invalid JSON lines). Its loss of parameterized events under AOT with JSON is a
  default-settings problem a source-generated resolver fixes; it does not occur under JIT, so it is not the reason.
- **Serilog:** the runner-up. `Serilog.Sinks.Async` drops events by default when its buffer is full (about 32% of a
  100k burst); blocking mode costs about 50% of its throughput.
- **Karambolo and NReco file loggers:** lighter, plug straight into `ILogger`, MIT, and either would avoid NLog's
  second configuration language. The owner kept NLog for its maturity and monthly releases; they stay the fallback.
- **OpenTelemetry:** .NET has no file exporter without a collector process.

### Folders

9. **Logs.** `NETPRINTS_LOG_DIR`, else `$NETPRINTS_STATE_DIR/logs`, else per OS:
   - Windows `%LOCALAPPDATA%\NetPrints\logs`;
   - macOS `~/Library/Logs/NetPrints` (the de facto convention, shown in the Console app; no Apple developer
     document backs it);
   - Linux `$XDG_STATE_HOME/NetPrints/logs`, else `~/.local/state/NetPrints/logs`.
   A pure resolver with tests produces it.
10. **State and backups move with the logs.** The state root is, per OS, Windows `%LOCALAPPDATA%\NetPrints` (not
    roaming), Linux `$XDG_STATE_HOME/NetPrints` (else `~/.local/state/NetPrints`), and on macOS the convention-based
    location the resolver documents. `NETPRINTS_STATE_DIR` still replaces the root. `state/` and `backups/` keep their
    names under it.
11. **Settings stay in the config folder** (`<ApplicationData>`: `~/.config` on Linux, roaming on Windows), where the
    user may sync or edit them.
12. **One-time migration** from `<ApplicationData>/NetPrints/{state,backups}`: on start, if the new location has no
    such folder and the old one does, move it (copy and verify when the volumes differ) and leave nothing behind. A
    failed migration never prevents start-up (FR-051): the editor falls back to defaults and logs the failure. The
    permission rule (FR-052: user-only access on Linux and macOS) applies to the new folders.
    The Help menu gains "Open logs folder".

## Consequences

- Windows users get a log; non-UI crashes leave a Critical line; E2E tests may read the session file instead of
  scraping the process output.
- A new dependency pair (NLog, NLog.Extensions.Logging), configured in `NetPrints.Desktop` only, with a second
  layout-string syntax to learn.
- The state location changes once per user; docs that name `~/.config/NetPrints` for state or backups (the saving
  and recovery guide, T104) must name the new folders.
- Per-message cost on the caller thread is higher than ZLogger's, so hot loops keep logging at Debug behind
  `IsEnabled` through `[LoggerMessage]`, which already guards it.
- Open proposals, taken as defaults unless the owner objects: the default levels table, "Open logs folder", and an
  E2E check of the session log file.
