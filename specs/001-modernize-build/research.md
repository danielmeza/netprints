# Research: Modernize Build and Migrate Editor to Avalonia (P0)

**Date**: 2026-09-24 | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

## Method

Every decision below was checked by building and running code, not only by reading docs:

- Environment: Linux (kernel 7.0), .NET SDK 10.0.301 and 10.0.400 (`~/.dotnet`), runtimes 10.0.9 and 10.0.11.
- Scratch copies outside the git tree:
  1. `np/` — a copy of the repo with the proposed build changes (CPM, Core multi-target, Roslyn 4.14, MSTest 4, and so on).
  2. `spike/` — Avalonia 11.3.22 + Nodify.Avalonia 1.0.2 + CommunityToolkit.Mvvm + Material.Icons + Xaml.Behaviors, with MSTest 4 headless tests.
  3. `refspike/`, `reflspike/` — compile/run and reflection against the .NET runtime assemblies.

  The git branch itself was not modified.
- Versions were queried with `dotnet package search --exact-match`, the nuget.org flat-container
  API (nuspec dependency groups) and the GitHub API (action releases, repository history).

## Review follow-up 2026-09-25 (PR #1 reviews, owner decisions)

This section **supersedes** the MSTest-based test design in sections e and p, the `UiTest.RunAsync`
harness and the warm SC-005 figure below.

**Test framework: xUnit v3** (owner decision). All tests moved from MSTest 4 to xUnit v3 on
Microsoft.Testing.Platform:

| Project | Framework | Notes |
|--|--|--|
| NetPrints.Core.Tests (core) | xunit.v3 3.2.2 | no DI: nothing to inject |
| NetPrints.Editor.Tests (reflection, view models, hosts) | xunit.v3 3.2.2 + Xunit.DependencyInjection 11.3.2 | a `Startup` shares one loaded `IReflectionHost`; `TestEditor` is resolved per test class |
| NetPrints.Editor.UITests (new, headless UI) | xunit.v3 3.2.2 + Avalonia.Headless.XUnit 12.1.3 | `[AvaloniaFact(Timeout = …)]`; no DI |

Verified:
- **xUnit 4.x is not usable yet.** Avalonia.Headless.XUnit 12.1.3, the latest adapter, is built
  against `xunit.v3.extensibility.core` 3.2.2 and fails discovery under 4.0.1 (reviewer probe). No
  newer adapter exists, so xUnit stays on 3.2.2.
- **Xunit.DependencyInjection must stay out of the UI assembly.** Its build targets register its
  own test framework, and then every `[AvaloniaFact]` fails.
- **Timeouts really cancel.** Probe: `[AvaloniaFact(Timeout = 1000)]` around an 8 s
  `Task.Delay(…, TestContext.Current.CancellationToken)`, and around a condition wait, fails as
  "canceled" after 1.0 s. The MSTest `[Timeout(…, CooperativeCancellation = true)]` was a no-op,
  because nothing observed its token.
- `xUnit1051` is an error in every test project: every awaited call passes
  `TestContext.Current.CancellationToken`.
- TRX: `dotnet test … --report-xunit-trx --results-directory TestResults`.
- The custom `HeadlessUnitTestSession`/`UiTest.RunAsync` harness is deleted. `[AvaloniaFact]` runs
  the body on the UI thread and awaits it. `[assembly: AvaloniaTestApplication]` points at the real
  `EditorApp`.

**UI test structure** (owner rule `maui-ui-testing`):
- Page objects per feature (`MainWindowPage`, `ClassEditorPage`, `GraphCanvasPage`,
  `NodeSearchPage`, dialog pages) own every locator.
- Controls are found by `AutomationIds` constants that the XAML also uses (`x:Static`).
- Waits poll a condition; there are no sleeps.
- Before a pointer event, the driver forces a render tick, because Avalonia 12 hit-tests the render
  scene.

**Code layout** (owner rule `code-organisation`): `NetPrints.Editor` and all test projects are
organized by feature (`Main/ ClassEditor/ Graph/{Nodes,Pins,GetSet}/ Search/ Variables/ References/
Inspectors/ Dialogs/ UndoRedo/ ModelSync/ Hosting/{Avalonia}/`). The tests mirror these folders.

**SC-005, measured on the cold path.** The earlier "~0.85 s" was measured on warm caches.

- `SearchPerformanceTests` uses a fresh host, loads it, and times the first search of a new graph.
- Without a warm-up, the first search took 2.4 s on this machine (the reviewer measured 2.2–2.4 s),
  over the 2 s budget. Roslyn binds member symbols lazily, and the first enumeration of about 120k
  static methods pays for that binding.
- `ReflectionHost.ReloadAsync` now does that enumeration once, in the background load, right after
  a project opens. The search waits for `IReflectionHost.Loaded`.
- Measured (Release, this machine):

  | | Without warm-up | With warm-up |
  |--|--|--|
  | First search | 2.4 s | 1.46–1.59 s |
  | Background load | 1.7 s | 2.4–2.9 s |
  | Worst keystroke | 11–25 ms | 11–25 ms |

- The test asserts only a 3× regression bound (6 s / 900 ms), so a busy CI runner doesn't flake;
  the measured values go to the test output (TRX).

**Other changes:**
- `IReflectionHost` has an explicit loaded state (`IsLoaded`, `Loaded`); `Provider` throws before
  the first load (no silent empty provider).
- Graph view models refresh overloads, enum names and documentation on `Reloaded`.
- `EditorContext` has required `IScheduler Scheduler` and `Func<IMessenger> CreateMessenger` (no
  defaults). The search box uses Rx `Throttle` on that scheduler, and the tests use `TestScheduler`
  (virtual time).
- The reference resolver's runtime fallback is all-or-nothing for framework references.
- Compiled output is deterministic: class sources are ordered, and emit is deterministic.
- `UnhandledExceptionHandler` reports exceptions that escape to the UI thread.
- `TreatWarningsAsErrors` is on for the editor, the desktop app and every test project.

## Scope update 2026-09-24 (constitution 1.2.0): net10.0 everywhere, latest dependencies

The project owner put Visual Studio / .NET Framework hosting out of scope. Constitution 1.2.0
(principle IV) makes `net10.0` the only target framework and allows the latest stable dependency
versions. This section **supersedes** every `netstandard2.0`, Roslyn-4.x, Avalonia-11 and
Nodify-1.0.2 statement below; those sections are kept as the record of the original analysis.

Re-verified by the implementer on Linux (SDK 10.0.400):

| Area | Before (sections a, b, c.3, h, j, m, n, p) | Now |
|------|---------------------------------------------|-----|
| Target frameworks | Core, Reflection `netstandard2.0;net10.0` | **every project `net10.0` only** |
| Roslyn | 4.14.0 (last line with ns2.0) | **Microsoft.CodeAnalysis.CSharp.Workspaces 5.9.0**; Core compiles unchanged, 11/11 core tests pass |
| HashCode polyfill | Microsoft.Bcl.HashCode 6.0.0 for ns2.0 | **dropped**; `System.HashCode` is built in |
| PropertyChanged.Fody | 3.4.1 | **4.1.0** (Fody 6.9.3); 11/11 tests pass; same `OnInputTypeChanged` warnings |
| Avalonia | 11.3.22 | **12.1.3** (Avalonia, Desktop, Themes.Fluent, Fonts.Inter, Skia, Headless) |
| Canvas | Nodify.Avalonia 1.0.2 (net7.0, unmaintained, Feb 2024) | **Nodify.Avalonia 2.0.0** (July 2026, MIT, a port of Nodify v7 to Avalonia 12, `lib/net8.0`) |
| Icons | Material.Icons.Avalonia 2.4.1 | **3.0.2** |
| Behaviors | Avalonia.Xaml.Behaviors 11.3.0.6 | **Xaml.Behaviors.Avalonia 12.0.7** (the package was renamed for Avalonia 12) |
| MVVM / search | CommunityToolkit.Mvvm 8.4.2, DynamicData 9.4.33 | unchanged (already latest) |
| Tests | MSTest 4.4.1 | unchanged (already latest) |

**Spike (Avalonia 12.1.3 + Nodify.Avalonia 2.0.0, MSTest 4.4.1, headless Skia, Linux)**: a window
with a `NodifyEditor` (2 nodes, 1 connection, `ItemContainerTheme` binding `Location`,
`NodeInput.Anchor` bound `OneWayToSource`) realizes 2 `ItemContainer`s at the bound locations and
1 `Connection`; anchors are pushed to the view model; `CaptureRenderedFrame()` returns a frame.
Theme registration is unchanged: `<StyleInclude Source="avares://Nodify.Avalonia/Themes/Controls.xaml"/>`.
`Node.Input`/`Node.Output` are item collections with `InputConnectorTemplate`/`OutputConnectorTemplate`.

**New headless gotcha (verified)**: `HeadlessUnitTestSession.Dispose()` hangs with Avalonia
12.1.3, even after every window is closed. The shared session is therefore **never disposed**;
process exit tears it down. The catch-and-rethrow `UiTest.RunAsync` helper and `[Timeout]` rules
from section e still apply.

**Implementation findings (headless UI tests)**:
- `HeadlessUnitTestSession.Dispatch` must be called with a `Func<Task<T>>`; with a plain async lambda
  the compiler binds an overload that does not await the body and the test passes vacuously
  (found by mutation-testing the UI tests).
- Avalonia 12 hit-tests the render scene, so after changing the layout the tests force a render
  tick (`AvaloniaHeadlessPlatform.ForceRenderTimerTick()`) before sending pointer input.
- Nodify captures the pointer on press; gesture targets (back button, right click, double click on
  a cable) are therefore resolved on press, not from the released event's source.
- A clean `mcr.microsoft.com/dotnet/sdk:10.0` container has no `libfontconfig` and no fonts. The
  test project uses `SkiaSharp.NativeAssets.Linux.NoDependencies` (3.119.4, matching Avalonia 12.1.3)
  instead of the default native assets, and the app sets the embedded Inter font as the default
  family. With that, all tests pass in the container with only the SDK (T084).
- SC-005 measured: the no-pin suggestion list is ~42k rows (public static members visible from the
  class; the "~119k" above counts all static methods), built and bound in ~0.85 s; worst keystroke
  filter step ~7 ms.

**Consequences**: risks R1 (ns2.0 editor for a VS host) and R3 (Avalonia 11.0.4 floor pulling
vulnerable transitive packages) no longer apply. R2 changes from "unmaintained 1.0.x" to "young
2.0.0 port" (mitigation unchanged: Nodify types stay in views). `RefKind.RefReadOnlyParameter`
still needs the mapping in `ReflectionConverter` (section a).

## Summary of verified results (original analysis)

| Check | Result |
|-------|--------|
| Baseline `dotnet build src/NetPrints.Core/NetPrints.Core.csproj` (unmodified, SDK 10.0.400) | **fails**: Fody 5.0.3 `Could not load file or assembly 'Mono.Cecil, Version=0.11.0.0'` + `AD0001` analyzer crashes (Roslyn 2.10) |
| Core + CLI + core tests with proposed build changes | build OK; **11/11 tests pass** on net10.0 (~0.7 s); TRX written |
| Core rebuilt twice from clean | `NetPrints.dll` bit-identical for both TFMs (deterministic) |
| Roslyn compile of C# against the 172 runtime assemblies + run via `dotnet X.exe` + generated `X.runtimeconfig.json` | success; prints expected output |
| Editor `ReflectionProvider` (moved as-is) over the runtime assembly set | **crashed** — `KeyNotFoundException: RefReadOnlyParameter`; after adding the mapping: ctor 1.7 s, 4,225 non-static types in 32 ms, **119,301 static methods** in 1.4 s, 106 `string` instance methods in 80 ms |
| Avalonia 11.3.22 + Nodify.Avalonia 1.0.2 on net10.0 | builds; headless window with 2 nodes + 1 connection realizes 2 `ItemContainer`s at the bound locations; `CaptureRenderedFrame` works with Skia |
| MSTest 4 + `HeadlessUnitTestSession` | 9/9 spike tests pass, **but** an exception thrown inside `session.Dispatch` hangs the test run; a capture-and-rethrow helper fixes it (verified) |
| CPM transitive pinning with `Avalonia.Desktop` 11.3.22 | removes NU1903 (SkiaSharp 2.88.3 → 2.88.9, Tmds.DBus.Protocol 0.15.0 → 0.21.3) pulled in by Nodify's Avalonia 11.0.4 minimum |

---

## a. Roslyn (Microsoft.CodeAnalysis) version and API breaks

**Decision**: `Microsoft.CodeAnalysis.CSharp.Workspaces` **4.14.0** as one central version (Core,
NetPrints.Reflection).

**Rationale**
- 4.14.0 is the last 4.x release. nuget.org then has 5.0.0 … 5.9.0, and the constitution mandates 4.x.
  Its nuspec has `.NETStandard2.0`, `net8.0` and `net9.0` groups, so it supports Core's
  `netstandard2.0` target and resolves the `net9.0` assets for `net10.0`.
- **Compile-time API breaks: none.** Core compiled unchanged:
  - `Translator/TranslatorUtil.cs` `Formatter.Format(root, new AdhocWorkspace()).NormalizeWhitespace()`
    works on Linux/net10.0; it is exercised by `ClassTranslatorTests` through `FormatCode`.
  - `Compilation/CodeCompiler.cs` (`CSharpCompilation.Create`, `Emit(path)`,
    `MetadataReference.CreateFromFile`) is unchanged.
  - `Reflection/ReflectionProvider.cs` (currently in the editor) compiles unchanged. The
    `(LanguageVersion)(int.MaxValue - 1)` hack equals `LanguageVersion.Preview` in 4.x.
- **Runtime behavior break found**: `ReflectionConverter.refKindToPassType` has no entry for
  `RefKind.RefReadOnlyParameter` (C# 12 `ref readonly` parameters, used across the .NET 10 BCL).
  Enumerating static methods throws `KeyNotFoundException`. **Fix**: map it to
  `MethodParameterPassType.In` (callers may pass `in` to `ref readonly` parameters). Verified.
- New analyzer warnings `RS1024` (10 sites, `Dictionary<ITypeSymbol,…>`) come from
  `Microsoft.CodeAnalysis.Analyzers` 3.11. They are warnings only; fix in P1.
- The Roslyn 2.10 `AD0001` analyzer crash disappears.

**Alternatives**: 5.9.0 (still has netstandard2.0, but violates the constitution); an older 4.x
(no benefit, and older C# parsing).

## b. Fody / PropertyChanged.Fody with the .NET 10 SDK

**Decision**: Core only: **Fody 6.9.3** + **PropertyChanged.Fody 3.4.1**, both `PrivateAssets="all"`.
The editor no longer uses Fody (CommunityToolkit.Mvvm source generators). P1 removes Fody from Core.

**Rationale**
- Fody 5.0.3 fails under .NET 10 MSBuild (Mono.Cecil load error, above). Fody 6.9.3 weaves both
  Core targets on Linux.
- PropertyChanged.Fody 3.4.1 (last 3.x) and 4.1.0 both pass the 11 tests. 3.4.1 is the smaller
  behavioral step from 3.0.1-beta.1. Version 4.x raised its minimum .NET Framework to net472 and
  changed defaults, so migrating to it is wasted work before P1 removes Fody.
- Both emit 14 harmless warnings, `Unsupported signature for a On_PropertyName_Changed method:
  OnInputTypeChanged`, on `Node` and its subclasses. That method is an event handler, not a property
  hook. They stay as warnings in P0.

## c. Core tests on Linux and the reference-assembly fix

**Decision**: 11/11 core tests pass after these minimal fixes:
1. Fody 6.9.3 + PropertyChanged.Fody 3.4.1 (b).
2. Tests move to `net10.0` + MSTest 4.4.1 (e).
3. `CS0103 'HashCode'` in `Core/TypeSpecifier.cs:228` and `Core/MethodSpecifier.cs:214` for
   `netstandard2.0`: **Gapotchenko.FX was not unused** — it supplied the `System.HashCode` polyfill.
   Replace it with `Microsoft.Bcl.HashCode` **6.0.0**, conditional on `netstandard2.0`, in Core and
   NetPrints.Reflection (`IReflectionProvider.cs` also uses `HashCode.Combine`).
4. `CS0411` in `tests/NetPrints.Core.Tests/TypeTests.cs:29-30`: MSTest 4 removed
   `Assert.AreEqual(object, object)`. Use `Assert.AreEqual<BaseType>(…)`; the semantics are identical.

**Reference assemblies**: the tests do not touch the hard-coded `ProgramFilesX86 … .NETFramework\v4.5`
paths (`Core/Project.cs:46-48`, `Core/FrameworkAssemblyReference.cs:34`,
`NetPrintsEditor/Reflection/ReflectionProvider.cs:173`, `…/DocumentationUtil.cs:63`). **But the
new editor does**: every project created with default references gets
`.NETFramework/v4.5/{System,System.Core,mscorlib}.dll`. On Linux
`SpecialFolder.ProgramFilesX86` is `""`, so the path does not exist,
`MetadataReference.CreateFromFile` throws, and the reflection provider — hence the whole editor —
fails. Compile/Run (PAR-09/10) needs references too. A minimal P0 fix is therefore required
(spec FR-008..010).

**Decision (minimal, format-preserving)**: add `NetPrints.Core.ReferenceAssemblyResolver` in Core.
- `IEnumerable<string> ResolveAssemblyPaths(IEnumerable<AssemblyReference> refs, ICollection<string> warnings)`:
  - A path that exists is used as-is (Windows with targeting packs: unchanged behavior).
  - A `FrameworkAssemblyReference` whose file is missing expands, once, to every managed
    `*.dll` in `RuntimeEnvironment.GetRuntimeDirectory()`. That is 172 assemblies on 10.0.11,
    including the `mscorlib.dll`/`System.dll`/`System.Core.dll` facades that type-forward into
    `System.Private.CoreLib`; the full set is needed for forwarded types to resolve.
  - Any other missing path is skipped and a warning is added. The caller shows it in the compile
    errors or status; it no longer throws.
- `bool UsesRuntimeAssemblies` tells the compile step to write `{Name}.runtimeconfig.json`
  (`Microsoft.NETCore.App`, the running major.minor version) next to the output.
  `Project.RunProject` starts `dotnet "{exe}"` when that file exists, and otherwise uses
  `Process.Start(exe)` as today.
- Used by `Project.CompileProject` (so the CLI benefits too) and by the editor's reflection
  reload. `DocumentationUtil`: the runtime directory has no XML docs, so tooltips show no
  documentation (no crash).
- Verified in `refspike`: a hello-world compiled against the runtime set and run with
  `dotnet Hello.exe` + runtimeconfig prints the expected text.
- **Stays for P1**: real ref-pack / targeting-pack resolution, target selection per project,
  documentation from ref packs, removing the Windows path logic.

## d. Solution layout while Windows-only projects exist

**Decision**: `NetPrints.slnx` stays the single entry point and contains only Linux-buildable
projects, so `dotnet build`/`dotnet test` at the root work everywhere:

- `NetPrintsEditor` (WPF) and `NetPrintsEditorUnitTests` are **deleted**; git history keeps them.
- `NetPrintsVSIX` is **removed from the solution** but kept on disk with a `README.md`:
  "pending P4 rework; does not build; not in CI". It is excluded from CPM
  (`ManagePackageVersionsCentrally=false`, set in `Directory.Build.props` by project name; MSBuild
  imports `Directory.Build.props` at `Microsoft.Common.props` line 34, before `NuGet.props`
  imports `Directory.Packages.props`).
- New projects are added.

**Evidence**: before the scope change, the WPF editor itself built on Linux with
`EnableWindowsTargeting=true`. The VSIX never builds on Linux (`MSB4019 … Microsoft.VsSDK.targets`
not found).

**Alternatives**: a solution filter (`NetPrints.Linux.slnf`) was the answer for the build-only
scope, and is no longer needed. `.slnx` works with SDK 10, but a format change adds churn with no
P0 benefit. Revisit in P4 when the VS extension returns.

## e. Test framework

**Decision**: **MSTest 4.4.1** (`MSTest` metapackage) on **Microsoft.Testing.Platform** for every
test project: `OutputType=Exe`, `EnableMSTestRunner=true`, and `global.json`
`"test": { "runner": "Microsoft.Testing.Platform" }` (the .NET 10 `dotnet test` MTP mode).

**Rationale**
- Least churn for the 11 existing tests: same attributes and assertions; only 2 lines change.
- Built-in TRX: `--report-trx --results-directory TestResults` works.
- **Avalonia headless with MSTest**: Avalonia ships `Avalonia.Headless.XUnit`/`.NUnit` integrations
  but no MSTest one. `Avalonia.Headless`'s `HeadlessUnitTestSession.StartNew(typeof(TestAppBuilder))`
  + `session.Dispatch(...)` works from MSTest 4 (verified: Nodify canvas, window rendering,
  `CaptureRenderedFrame` with `UseSkia()` + `UseHeadlessDrawing=false`).
- **Gotcha (verified)**: an exception or failed `Assert` *inside* `session.Dispatch` hangs the run
  (Avalonia 11.3.22). Rule: UI tests call a helper `UiTest.RunAsync(Action|Func<Task>)` that catches
  inside the dispatcher, returns an `ExceptionDispatchInfo`, and rethrows outside (verified). Every
  UI test also gets `[Timeout(60000, CooperativeCancellation = true)]`, and UI test classes use
  `[DoNotParallelize]`.
- xUnit v3 + `Avalonia.Headless.XUnit` was rejected: two test frameworks in one repo, a rewrite of
  the 11 core tests, and the Avalonia xUnit integration targets xUnit v2 in 11.x.

## f. Shared build settings

| Setting | Value | Notes |
|---------|-------|-------|
| `LangVersion` | `latest` | Replaces `preview`; C# 14 on SDK 10; verified on both Core targets |
| `Nullable` | `enable` default | `disable` in Core, NetPrints.Core.Tests and NetPrints.Reflection (moved code; measured ~100 unique warnings in Core); new editor, desktop and test code enables it |
| `Deterministic` | `true` | verified bit-identical |
| `ContinuousIntegrationBuild` | `true` when `$(CI)=='true'` | set on GitHub Actions |
| `TreatWarningsAsErrors` | `false` | Fody, RS1024 and MSTEST0017 warnings exist; ratchet per project later |
| `ManagePackageVersionsCentrally` | `true`, except `NetPrintsVSIX` | see d |
| `CentralPackageTransitivePinningEnabled` | `true` | lifts vulnerable transitive packages (NU1903) |
| `AvaloniaUseCompiledBindingsByDefault` | `true` (editor/desktop) | build-time binding checks; needs `x:DataType` |

## g. CLI

**Decision**: `NetPrints.Cli` targets `net10.0` only. CommandLineParser 2.5.0 → **2.9.1**, with the
same API. There is no parser migration (Spectre is P2). The existing exit codes are unchanged:
`--help` and `--version` return 1, and compile returns 1 on success. The P2 redesign fixes them.
With the reference resolver (c), `NetPrints.Cli -p x.netpp` now compiles on Linux too.

## h. Dependencies removed or replaced

| Dependency | Where | Finding | Decision |
|------------|-------|---------|----------|
| Gapotchenko.FX 2019.1.151 | Core | only the `HashCode` polyfill | remove; `Microsoft.Bcl.HashCode` 6.0.0 for ns2.0 |
| System.Management 4.5.0 | editor | no usages | removed with the WPF editor |
| `System.Windows.Forms` | editor | `FolderBrowserDialog` in `ReferenceListWindow.xaml.cs:54` | replaced by Avalonia `StorageProvider.OpenFolderPickerAsync` |
| MahApps.Metro(.IconPacks) alphas | editor, VSIX | UI toolkit | Fluent theme + Material.Icons.Avalonia; VSIX untouched |
| MvvmLightLibsStd10 5.4.1.1 | editor | `ViewModelBase`, `Messenger` | CommunityToolkit.Mvvm `ObservableObject`/`IMessenger` |
| PropertyChanged.Fody (editor) | editor | 5 convention hooks (see k) | explicit `[ObservableProperty]` + `partial On…Changed` |
| MSTest 1.4 / `Microsoft.NET.Test.Sdk` preview | tests | obsolete | MSTest 4.4.1 metapackage |

## i. CI design (Linux only; contract for P4)

**Decision**: `.github/workflows/ci.yml`, **`name: CI`** (a stable contract; P4 chains
`on: workflow_run: workflows: ["CI"], types: [completed]` with a success check, path-filtered, on
`windows-latest`). One job `build-test` runs on `ubuntu-latest`. Triggers: push and pull_request
to `master`, and `workflow_dispatch`. There is a concurrency group per ref, and `permissions:
contents: read`. Environment: `DOTNET_NOLOGO=1`, `DOTNET_CLI_TELEMETRY_OPTOUT=1`.

Steps:
1. `actions/checkout@v7`
2. `actions/setup-dotnet@v6` with `dotnet-version: 10.0.x`
3. `dotnet restore NetPrints.slnx`
4. `dotnet build NetPrints.slnx -c Release --no-restore`
5. `dotnet test --solution NetPrints.slnx -c Release --no-build --report-trx --results-directory TestResults`
   (core + editor tests; headless Avalonia needs no display server)
6. CLI smoke: `dotnet run --project src/NetPrints.Cli -c Release --no-build -- --version | grep NetPrints.Cli`
   (tolerates exit code 1)
7. `actions/upload-artifact@v7` with `TestResults/**/*.trx`, `if: always()`

No Windows jobs and no VSIX workflow in P0 (see `contracts/ci-workflow.md`). Expected wall time is
4–8 min, under SC-003's 15 min.

---

## j. Avalonia version: 11 vs 12

**Decision**: **Avalonia 11.3.22** (latest 11.x): `Avalonia`, `Avalonia.Desktop`,
`Avalonia.Themes.Fluent`, `Avalonia.Fonts.Inter`, `Avalonia.Skia`, `Avalonia.Headless`.

**Evidence**: Avalonia 12.1.3 (latest) has only `net8.0` and `net10.0` groups. 11.3.22 has
`net6.0`, `net8.0` and `.NETStandard2.0`. The P4 VS host runs in Visual Studio's .NET Framework 4.8
process, so it needs `netstandard2.0` Avalonia assemblies. Choosing 12 now would block P4.
Companion packages must match the major version:

| Package | Avalonia 11 line (chosen) | Avalonia 12 line (rejected) |
|---------|---------------------------|-----------------------------|
| Nodify.Avalonia | **1.0.2** (net7.0, Avalonia ≥ 11.0.4) | 2.0.0 (net8.0, Avalonia 12.0.5) |
| Material.Icons.Avalonia | **2.4.1** (ns2.0, Avalonia ≥ 11.0.0) | 2.4.2+ / 3.x (net8.0, Avalonia 12) |
| Behaviors | **Avalonia.Xaml.Behaviors 11.3.0.6** (ns2.0/net6/net8) | Xaml.Behaviors.Avalonia 12.x |
| ReactiveUI glue | Avalonia.ReactiveUI 11.3.9 (not used, see l) | ReactiveUI.Avalonia 12.x |

## k. MVVM: CommunityToolkit.Mvvm (replacing MvvmLight + Fody)

**Decision**: **CommunityToolkit.Mvvm 8.4.2** (ns2.0/net8.0). Use `ObservableObject`,
`[ObservableProperty]`, `[RelayCommand]`, and `IMessenger` (`WeakReferenceMessenger`, injected so
tests can use a private instance).

**Migration traps found in the WPF VMs** (Fody conventions that become explicit):
- `MainEditorVM.OnProjectChanged()` — Fody hook that wires reference/compile events and **reloads
  the reflection provider** (PAR-15). Becomes `partial void OnProjectChanged(Project? value)`.
- `ClassEditorVM.OnClassChanged()` — rebuilds the Methods/Constructors/Variables collections.
- `NodePinVM.OnPinChanged()` — subscribes to the pin's `PropertyChanged`.
- `SuggestionListVM.OnItemsChanged()` / `OnSearchTextChanged()` — refresh the list and split
  search terms.
- Fody also raised change notifications for computed getters (`IsProjectOpen`, `CanCompile`,
  `CanCompileAndRun`, `BorderBrush`, `ZIndex`, …). Each needs `[NotifyPropertyChangedFor]` or an
  explicit `OnPropertyChanged`.
- `MessengerInstance` (MvvmLight global) messages: `OpenGraphMessage`, `NodeSelectionMessage`,
  `AddNodeMessage` → `IMessenger` with `IRecipient<T>`.
- WPF types in VMs to replace (principle II): `NodePinVM` (`Point` ×8 properties, `Brush`
  ×3), `NodeVM` (`SolidColorBrush` ×15), `ClassEditorVM` (`Dispatcher.CurrentDispatcher`,
  `System.Timers.Timer`). Replacements: `GraphPoint` record struct, a `NodeColor`/`PinColor`
  ARGB `uint` (or an enum key) + view converters, `IUiDispatcher`, and an async debounce loop.
- Static `App.ReflectionProvider` / `App.NonStaticTypes` (used by VMs, controls and converters) →
  injected `IReflectionHost` service.
- VMs that open dialogs (`SuggestionListVM` → `SelectTypeDialog`/`SelectMethodDialog`) → an
  injected `IEditorDialogs` service. This fixes the existing "TODO: Move dialogs to view".

## l. DynamicData / ReactiveUI

**Decision**: **DynamicData 9.4.33** (ns2.0/net10.0; brings System.Reactive 6.1.0), used only for
the node-search list: a `SourceList<SuggestionItem>`, with `Filter(IObservable<Func<…>>)` driven by
the search text (throttled about 100 ms), then `Bind` to a `ReadOnlyObservableCollection`.
**No ReactiveUI**: one MVVM framework only (CommunityToolkit).

**Rationale**: the full runtime reference set gives about 119k entries in the "no pin" suggestion
list (measured). WPF used `ListCollectionView.Refresh()` over a much smaller .NET Framework set.
Incremental filtering plus a virtualized list is needed for SC-005.

## m. Graph canvas: Nodify.Avalonia 1.0.2

**Decision**: **Nodify.Avalonia 1.0.2** (MIT). Nodify's types are used only in views
(`src/NetPrints.Editor/Views/Graph/*`); VMs stay framework-free.

**Verified in the spike (Linux, headless, net10.0, Avalonia 11.3.22)**
- Namespaces: `Nodify.Avalonia` (`NodifyEditor`, `ItemContainer`, `NodifyCanvas`,
  `DecoratorContainer`), `Nodify.Avalonia.Nodes` (`Node`, `NodeInput`, `NodeOutput`, `KnotNode`,
  `GroupingNode`, `StateNode`), `Nodify.Avalonia.Connections` (`Connection`, `LineConnection`,
  `CircuitConnection`, `Connector`, `PendingConnection`). There is no `XmlnsDefinition`; use
  `clr-namespace:`.
- **Theme registration** (otherwise no templates → 0 realized items):
  `<StyleInclude Source="avares://Nodify.Avalonia/Themes/Controls.xaml"/>` in `Application.Styles`,
  plus `<ResourceInclude Source="avares://Nodify.Avalonia/Themes/Dark.xaml"/>` in resources.
  (`Themes/Nodify.xaml` is a ResourceDictionary of colors, not styles.)
- `NodifyEditor` properties include `ItemsSource`, `Connections`, `ConnectionTemplate`,
  `PendingConnection`, `PendingConnectionTemplate`, `SelectedItems`, `GridCellSize`,
  `ViewportZoom`, `MinViewportZoom`, `MaxViewportZoom`, `ViewportLocation`, `DisablePanning`,
  `DisableZooming`, `EnableRealtimeSelection`, `ConnectionStartedCommand`,
  `ConnectionCompletedCommand`, `DisconnectConnectorCommand`, `RemoveConnectionCommand`,
  `ItemsDragStartedCommand`, `ItemsDragCompletedCommand`, `ItemsSelectStartedCommand` and
  `ItemsSelectCompletedCommand`. These map onto PAR-46…51: `GridCellSize=28`,
  `MinViewportZoom=0.3`, `MaxViewportZoom=1.0`, snap on `ItemsDragCompletedCommand`, and search
  on a pending connection without a target.
- The node location binds via a Style (`<Style Selector="nodify|ItemContainer" x:DataType="…">
  <Setter Property="Location" Value="{Binding Location, Mode=TwoWay}"/>`). `ItemContainerTheme`
  `BasedOn` also works once the theme is registered.

**Risks (recorded)**
- **R1 (P4 blocker risk)**: 1.0.2 ships only `lib/net7.0`, so the editor library cannot
  multi-target `netstandard2.0` for the VS host while it uses Nodify. The upstream repository
  (`trrahul/nodify-avalonia`) was rewritten for 2.0/Avalonia 12 (a single "Initial commit"), so the
  1.0.x source is not on GitHub. Mitigation: Nodify stays behind the graph views. In P4, either
  (a) build a `netstandard2.0` copy of the 1.0.x canvas (MIT) or of the upstream WPF Nodify
  (miroiu/nodify, MIT), (b) move to NodeEditorAvalonia 11.3.x (ns2.0, MIT) for the VS host, or
  (c) re-evaluate the host strategy. Record as a P4 follow-up.
- **R2**: 1.0.2 was released in February 2024 and is unmaintained. The fallback is a small custom
  canvas; the WPF implementation is about 500 LOC to port.
- **R3**: its Avalonia 11.0.4 dependency floor pulls vulnerable transitive packages; transitive
  pinning fixes this (verified).

**Alternatives**: NodeEditorAvalonia 11.3.11 (ns2.0, maintained, but its own node/connector model
needs adapters); a custom port of the WPF canvas (lowest dependency risk, but contradicts the
agreed stack and gives up Nodify's selection, dragging and pending-connection features);
Nodify.Avalonia 2.0.0 (requires Avalonia 12).

## n. Icons, behaviors, dialogs, theme

- **Material.Icons.Avalonia 2.4.1**: `<mi:MaterialIconStyles/>` in App styles, and
  `<mi:MaterialIcon Kind="Minus|Plus"/>` replaces `iconPacks:PackIconMaterial`. Verified headless.
- **Triggers**: the "~41 triggers" are 35 in `App.xaml`, **all commented out (dead)**, plus 3 live
  `IsMouseOver` triggers in `PinControl.xaml` and 1 `EventSetter` in `SearchableComboBox.xaml`.
  Live triggers become Avalonia `:pointerover` style selectors and style classes. The EventSetter
  and the mouse gestures (double-click, drag start, middle-click, XButton1) become
  **Avalonia.Xaml.Behaviors 11.3.0.6** `EventTriggerBehavior` → `InvokeCommandAction` where a
  command exists, and code-behind otherwise (views only).
- **Dialogs**: MahApps `MetroWindow`/`CustomDialog`/`ShowProgressAsync`/`ShowMessageAsync`/Flyouts
  become Avalonia `Window` dialogs (`ShowDialog<T>(owner)`), an in-window overlay for progress,
  `SplitView` panes for the Project and Settings flyouts, the built-in `ToggleSwitch` (with On/Off
  content) and `TextBox.Watermark`. File and folder pickers use
  `TopLevel.StorageProvider.OpenFilePickerAsync/SaveFilePickerAsync/OpenFolderPickerAsync` behind
  `IFilePickerService`. `Clipboard` uses `TopLevel.Clipboard` behind `IClipboardService`.
- **Theme**: `FluentTheme` with `RequestedThemeVariant="Dark"` and an emerald accent
  (`SystemAccentColor` `#FF008A00`, MahApps "Emerald"). Fonts come from `Avalonia.Fonts.Inter`
  (`.WithInterFont()`), for consistent headless rendering on CI.
- **Resources**: the 16 PNG icons move to `src/NetPrints.Editor/Assets/` as `AvaloniaResource`, with
  `avares://src/NetPrints.Editor/Assets/{icon}` replacing `pack://application:,,,/…`.

## o. Project structure for the editor

**Decision**
- `src/NetPrints.Reflection/` (new, `netstandard2.0;net10.0`, `Nullable=disable`): moved
  `IReflectionProvider`, `ReflectionProvider`, `MemoizedReflectionProvider`, `Memoization`,
  `ReflectionConverter`, `DocumentationUtil` and `DefaultOperatorSpecifiers`. The namespace changes
  `NetPrintsEditor.Reflection` → `NetPrints.Reflection`. The only behavioral change is the
  `RefReadOnlyParameter` mapping plus the resolver-based reference loading (missing files skipped).
- `src/NetPrints.Editor/` (new, `net10.0`, library): `EditorApp` (`App.axaml`, theme, composition
  root: manual wiring, no DI container), `ViewModels/`, `Services/` (interfaces) and
  `Services/Avalonia/` (the `TopLevel`-based implementations), `Messages/`, `Commands/` (undo/redo),
  `Views/` (axaml), `Converters/`, `Assets/`. It is net10.0-only in P0 because of risk R1.
  Keeping `EditorApp` here lets the headless tests start the real app without referencing an exe.
- `src/NetPrints.Desktop/` (new, `net10.0`, `WinExe`): `Program.cs` only (`BuildAvaloniaApp()` →
  `AppBuilder.Configure<EditorApp>().UsePlatformDetect().WithInterFont()`, command-line argument
  hand-off) and the app icon.
- `tests/NetPrints.Editor.Tests/` (new, `net10.0`, MSTest 4 + Avalonia.Headless + Avalonia.Skia):
  `Reflection/`, `ViewModels/`, `Ui/` (headless, `TestAppBuilder` → `EditorApp`), `Samples/`.
- `samples/HelloWorld/` (new): `HelloWorld.netpp` + `HelloWorld.Program.netpc` (a static `Main`
  that calls `Console.WriteLine("Hello, World!")`), produced once by a test helper through the Core
  API and checked in. A test asserts that it loads, compiles and runs.
- Existing names (`NetPrints`, `NetPrints.Cli`, `NetPrints.Core.Tests`) are kept to limit churn.
  Renames are P1/P2.

## p. Package version summary (Directory.Packages.props)

| Package | Version | Used by |
|---------|---------|---------|
| Microsoft.CodeAnalysis.CSharp.Workspaces | 4.14.0 | Core, Reflection |
| Fody / PropertyChanged.Fody | 6.9.3 / 3.4.1 | Core only |
| Microsoft.Bcl.HashCode | 6.0.0 | Core, Reflection (ns2.0 only) |
| CommandLineParser | 2.9.1 | CLI |
| MSTest | 4.4.1 | all test projects |
| Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent, Avalonia.Fonts.Inter, Avalonia.Skia, Avalonia.Headless | 11.3.22 | Editor, Desktop, Editor.Tests |
| Nodify.Avalonia | 1.0.2 | Editor (views) |
| CommunityToolkit.Mvvm | 8.4.2 | Editor |
| DynamicData | 9.4.33 | Editor (search list) |
| Avalonia.Xaml.Behaviors | 11.3.0.6 | Editor (views) |
| Material.Icons.Avalonia | 2.4.1 | Editor (views) |

SDK: `global.json` `10.0.100` + `latestFeature` (resolves to 10.0.400 locally). Actions:
checkout v7, setup-dotnet v6, upload-artifact v7 (latest majors on 2026-09-24).

## q. Risks and follow-ups (summary)

| ID | Risk | Mitigation / owner |
|----|------|--------------------|
| R1 | ~~Nodify.Avalonia 1.0.2 is net7.0-only, which blocks a ns2.0 editor for the VS host~~ | Obsolete since constitution 1.2.0 (VS hosting out of scope) |
| R2 | Nodify.Avalonia 2.0.0 is a young port (single release) | Nodify types only in views; pin version; fallback custom canvas |
| R3 | ~~Headless `Dispatch` hangs on exceptions~~ | Obsolete: Avalonia.Headless.XUnit `[AvaloniaFact]` with real timeouts (review follow-up) |
| R4 | 119k suggestion entries over the runtime set | DynamicData filter + throttle + virtualized list; SC-005 test |
| R5 | Runtime-assembly fallback changes compile semantics on Linux (net10 instead of netfx) | Documented; P1 adds explicit target/ref-pack selection |
| R6 | ~~Parity items with pointer gestures (XButton1, middle-click, right-drag vs right-click) are hard to automate headless~~ | Automated: headless input through page objects, plus desktop E2E on Xvfb for OS drag and drop, the window manager, GTK pickers and the real cursor (§r) |
| R7 | Governance: constitution/roadmap amendments pending user approval | plan.md → Pending governance amendments |
| R8 | VSIX source no longer compiles against the new editor | Marked pending P4; excluded from build and CI |

## r. UI test automation (replaces the manual walkthrough, T076)

**Decision**: one set of page objects over a driver abstraction, a thin hand-rolled Screenplay layer
on top, and two drivers: headless (Avalonia.Headless) and X11 (real desktop editor on Xvfb).

- **Projects**: `NetPrints.Testing.Ui` (`IUiDriver`, `UiElement`, `UiWait`, screen and component
  objects, snapshot comparison, Screenplay, shared smoke scenarios);
  `NetPrints.Editor.UITests` (`HeadlessDriver`, headless tests, snapshot baselines);
  `NetPrints.Desktop.E2ETests` (`X11Driver`, GTK file dialogs, XFixes cursor reader, E2E tests).
- **Locating**: only by `AutomationIds`, through `AutomationTree` (NetPrints.Editor), which both
  drivers use, so headless and desktop see the same elements and properties (bounds, screen bounds,
  text, enabled/visible, pseudo-classes, cursor, text overflow, viewport, window state).
- **Desktop agent**: with `NETPRINTS_AUTOMATION=1` the desktop editor serves a read-only local pipe
  (a Unix domain socket): `status` (ready signal: window shown, project and types loaded), `find`,
  `dump`, `settle`. Input stays real: xdotool through the X server and openbox.
  - **Decision (review round 3)**: the agent ships in every configuration, including the Release
    `NetPrints.Desktop` build, rather than being compiled out or moved to a separate assembly.
    Reasons: the E2E suite must exercise the real Desktop build, not a test-only variant, so a
    build-time split would either duplicate the composition root or make E2E test a binary nobody
    ships; and with `PipeOptions.CurrentUserOnly` on both ends plus a per-user, per-process default
    path (`$XDG_RUNTIME_DIR/netprints-automation-<pid>.sock` on Linux), an idle listener gated by
    an environment variable that defaults unset is low-risk. Revisit if the P4 VS extension host
    turns out to need a narrower surface than the desktop app does.
- **Anti-flakiness**: no sleeps (condition waits; headless `RunJobs` + forced render tick; desktop
  `settle` round trips); transitions disabled; invariant culture and UTC; fixed window sizes
  (headless class windows get the 1600x1000 E2E screen size); a fresh editor, temp folder and (E2E)
  private home per test; one private Xvfb per run on a free display ≥ 100, never the user's
  desktop, and no D-Bus session, so pickers are GTK dialogs on that display; the desktop driver
  moves the pointer through intermediate points (a jump onto a pin's tooltip window swallowed the
  next click); per-test diagnostics (screenshots, tree dump, editor stdout/stderr, xdotool log) as CI
  artifacts; no retries except infrastructure waits (X socket, window manager).
- **Snapshots**: per-pixel threshold 32/255, at most 0.5 % differing pixels, masks for carets.
  Baselines are committed in `tests/NetPrints.Editor.UITests/Snapshots/Baselines` and regenerated with
  `NETPRINTS_UPDATE_SNAPSHOTS=1`; new baselines are reviewed before acceptance.

### Screenplay library: Boa.Constrictor evaluated, hand-rolled instead

Timeboxed evaluation of `Boa.Constrictor.Screenplay` 4.0.0 (the core package):

| Criterion | Result |
|---|---|
| Builds on net10.0 | Yes (netstandard2.0) |
| Maintained | Core package last released 2023-06 (the umbrella `boa.constrictor` 4.2.0 is 2026-05) |
| Async with cancellation | No: `ITaskAsync.PerformAsAsync(IActor)` and `IQuestionAsync<T>.RequestAsAsync(IActor)` take no `CancellationToken`, so xUnit's `TestContext.Current.CancellationToken` (required, xUnit1051 is an error) cannot flow |
| Waiting | `AbstractWait` is a synchronous Stopwatch busy loop and `Retries.RetryOnException` uses `Thread.Sleep`: both block the headless UI thread and break the no-sleep rule |

**Decision**: hand-roll the core (`Actor`, `IAbility`, `ITask`, `IQuestion<T>`, about 150 lines) in
`tests/NetPrints.Testing.Ui/Screenplay`, with cancellation tokens and a journal for diagnostics. Tasks
and questions only call page objects; the ability `UseNetPrints` wraps the root page object over
`IUiDriver`, so the same tasks run on both drivers.

### What stays manual

Nothing functional. Visual judgement (colors, icons, layout) is covered by the snapshot baselines
and the E2E screenshots, which the coordinator reviews; a Windows or macOS run is outside the
Linux-only CI (constitution).
