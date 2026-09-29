# 0008: CI code coverage uses static instrumentation only

## Status

Accepted (2026-09-28).

## Context

CI's `Build and test (Linux)` job intermittently lost the whole `NetPrints.Core.Tests` host to a
`Fatal error. System.AccessViolationException`, each time in a different trivial method of a
NetPrints assembly: `InMemoryTypeCatalog.Copy` (run 36413425390), the source-generated
`NetPrintsJsonContext.IReadOnlyListTypeRefSerializeHandler` (commit 6dd89f2). Batch D4 blamed
concurrent extension loading and serialized those tests; the crash came back.

The job runs with `--coverage` (Microsoft.Testing.Extensions.CodeCoverage 17.14.2). On Linux that
turns on both instrumentation modes:

- **Static**: before the test host starts, the coverage controller rewrites every assembly in the
  test output folder so each probe is `*(Begin + index) = 1`, where `Begin` points into a
  memory-mapped hit buffer, `/tmp/CodeCoverage.<session>.<module>`.
- **Dynamic**: a CLR profiler, enabled through `CORECLR_ENABLE_PROFILING`/`CORECLR_PROFILER*`
  environment variables. Child processes inherit them, so every `dotnet` process a test starts
  (MSBuild, and the `dotnet exec NetPrints.Generator.dll generate` the SDK targets run) is profiled
  too.

When such a child loads `NetPrints.Core`, `NetPrints.Serialization` or `NetPrints.Reflection` (the
generator's own copies, same module identity), the controller reopens the host's static buffer for
that module and runs `ftruncate(fd, 0)` then `ftruncate(fd, size)`. A host thread that hits a probe
of that module in between writes to a page past the end of the file: `SIGBUS`, which the runtime
reports as an `AccessViolationException`.

Evidence (reproduced locally, 2 crashes in about 40 full coverage runs):

- The crash dumps fault on a probe, `movb $0x1,0x24d(%rax)` with `rax` = `Begin`. The two probes
  just before it, on the same page, had succeeded.
- `Begin` is the start of the mapping of `NetPrints.Core`'s static buffer, and `createdump` could not
  read that mapping.
- Under `strace`, the controller re-creates and truncates that same file partway through the run,
  right as a generator child process opens it.

The closest upstream report is [microsoft/codecoverage#238](https://github.com/microsoft/codecoverage/issues/238):
a Linux AV from a coverage buffer mapping that becomes invalid partway through a method. It
describes the dynamic-instrumentation and `AssemblyLoadContext` variant. No upstream issue covers
this child-process case.

## Decision

CI passes `--coverage-settings tests/CodeCoverage.config`, which sets
`EnableDynamicManagedInstrumentation` to `False` and keeps `EnableStaticManagedInstrumentation`. The
test host then gets no profiler variables, so neither do its children. No process other than the
host maps its buffers, and the controller only creates them before the host starts (checked with
`strace`: no profiler variables in any `execve`, no dynamic buffers, no static buffer created after
the host starts).

Rejected alternatives:

- Stripping the profiler variables in `ExternalProcess`/`ProcessRunner`. Product code would have to
  know about a test-only tool, and every other child-launch path would stay exposed.
- Dropping coverage on Linux, or skipping the tests that start child processes.

## Consequences

- Every NetPrints assembly in a test project's output folder is still covered. The only coverage lost
  is code running in child processes (the generator runs that sample builds start, already covered
  in-process by the generator tests) and `NetPrints.TestExtension`, a test fixture loaded from outside
  the output folder.
- Child processes that tests start no longer run under the profiler, which also removes its overhead
  from every MSBuild node and generator process.
- If a later CodeCoverage release fixes the buffer re-creation, dynamic instrumentation can be turned
  back on in `tests/CodeCoverage.config`. Before relying on it, check with a loop of full
  `--coverage` runs of `NetPrints.Core.Tests`.
- Running `dotnet test -- --coverage` locally without the settings file keeps the default modes and
  can still crash this way.
