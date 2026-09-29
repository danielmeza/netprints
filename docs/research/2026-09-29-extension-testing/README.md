# Extension testing for NetPrints: survey and recommendation

Date: 2026-09-29. Scope: (1) how to test NetPrints extensions against other extensions, and (2) how other
plugin ecosystems handle testing, both the kit they give authors and the host's own compatibility and
conformance testing. This is research only. Planning is in the roadmap (`docs/research/2026-09-29-extension-testing/` maps to roadmap bullets P2 and P2/P3).

---

---

## 0. TL;DR

- Every mature host ships **the host itself as the test fixture**. The kit starts a real host (VS Code
  Extension Development Host, IntelliJ headless platform, Backstage `createExtensionTester`,
  Grafana/JupyterLab Playwright fixtures) and does not mock it. The closest .NET analogue is Roslyn's
  `Microsoft.CodeAnalysis.Testing`: a `Test` object with declarative `TestState` and expected
  diagnostics, then `RunAsync()`.
- **Nobody ships a real cross-extension interop matrix.** Multi-plugin safety comes from four cheaper
  mechanisms:
  1. structural ID namespacing and uniqueness checks at the registry or marketplace (Obsidian, VS Code
     `publisher.name`, IntelliJ plugin ids);
  2. declared dependencies that the test runner installs (`extensionDependencies` / `installExtensions`,
     Gradle `plugin()` / `bundledPlugin()`, Unity `testables`, Tycho target platform);
  3. **static verifiers** (IntelliJ Plugin Verifier, bnd/PDE API baselining);
  4. runtime triage tools (VS Code Extension Bisect).
- Binary and API compatibility is handled in two halves. The **host side** runs API baselining (ApiCompat,
  bnd baseline, PDE API Tools, IntelliJ `@ApiStatus`). The **consumer side** verifies an extension's
  compiled references against each target host build (IntelliJ Plugin Verifier, Grafana
  `e2e-versions` matrix, Fab/UAT `BuildPlugin` per engine version).
- Recommendation for NetPrints:
  - **(a)** A `NetPrints.Extensibility.Testing` package in Roslyn style: `ExtensionTest<TExtension>` plus
    a reusable **conformance suite** that includes a "noisy neighbour" co-load check.
  - **(b)** A host-side **multi-extension scenario suite** built on fixture extensions: conflicting ids,
    private-dependency version skew, extension-on-extension types, order-permutation invariance, and
    missing-extension documents.
  - **(c)** `PublicApiAnalyzers` now, then package validation with a baseline once the package ships,
    and later a small **`netprints-verify`** tool modelled on IntelliJ Plugin Verifier.
  - **(d)** Leave P1 as it is. Do (b) and the API tracking in P2, the kit in P2/P3, and the verifier
    before the first third-party (NetPrintsUnreal) release.

---

## 1. What NetPrints has today (facts that shape the recommendation)

| Area | Finding | Where |
|---|---|---|
| Load contexts | **Not collectible.** The brief says "collectible `ExtensionLoadContext`", but the code and R8 say `isCollectible: false`, and unloading is out of scope ("hot reload of extensions is not a P1 goal"). So unload tests are **future** work. | `Loading/ExtensionLoadContext.cs:8,33`; `specs/003-core-refactor/research.md:210-218` |
| Shared assemblies | Sharing is decided **by name prefix only**: `NetPrints`, `Microsoft.Build`, `Microsoft.CodeAnalysis`, `CommunityToolkit.Mvvm`, `System.Reactive`, `DynamicData`, `Avalonia`, `Microsoft.Extensions.*.Abstractions`, plus TPA names. R8 says "already present in the Default context … whose name is `NetPrints*` …", but the code does not check that the assembly is present. | `ExtensionLoadContext.cs:12-20,58-72` |
| **Latent hazard 1** | A third-party *private* dependency whose name starts with `NetPrints` (for example `NetPrintsUnreal.Runtime.dll`) or `Avalonia` (for example a third-party `Avalonia.Xaml.Behaviors` the host doesn't ship) is deferred to Default, where it doesn't exist, so the load fails with `NPX007` or a `FileNotFoundException` at first use. NetPrintsUnreal will hit this unless its assemblies avoid the prefix. | same |
| **Latent hazard 2** | `dependsOn` only **orders** loading. Nothing lets extension B resolve extension A's assembly. B's `AssemblyDependencyResolver` can't find `A.dll`, because it isn't in B's folder when B references A with `Private=false`, so the load falls back to Default and fails. If B ships its own copy of `A.dll`, B gets a **second type identity** for A's types. "Extension depends on extension's types" is therefore unsupported today. | `ExtensionLoader.cs:173-225` (topological sort only) |
| ID conflicts | Node kinds are **structurally namespaced**: a non-built-in kind must start with `<manifestId>/`, which makes cross-extension kind collisions impossible by construction. Duplicate extension id: first wins, the other gets `NPX004`. Profiles, host-channel factory ids and settings sections: first wins, others get `NPX006`. There are also sets for `nodeTypes` and `documentTypes`. | `RegistryBuilder.cs:57-97,140-200`; `ExtensionLoader.cs:92,156` |
| API version gate | `netprintsApi` major must equal the host major and minor must be ≤ host minor (`NPX002`). `ExtensionApi.Version = 1.0`. This is a **declared** check only; no binary verification exists. | `ExtensionLoader.cs:160-165`; `ExtensionApi.cs` |
| Existing tests | `ExtensionLoaderTests` already **compiles fixture extensions with Roslyn at test time** into temp folders. It covers NPX001–007, dependency cycles, and ordering (EX-T10, EX-T11). `ExtensionTestSupport` has `DelegateExtension`, `CollectingLoggerFactory`, and `Ping`/`Pong` node fixtures. `InMemoryHostChannel` ships in the product assembly. | `tests/NetPrints.Core.Tests/Extensibility/*` |
| Packaging | `NetPrints.Extensibility` is **not packable** (`Directory.Build.props` sets `IsPackable=false`; only Core, Reflection, Sdk and Cli opt in). Core and Reflection have `EnablePackageValidation`, but **no `PackageValidationBaselineVersion`** exists anywhere, so no breaking-change check runs yet. No `PublicAPI.*.txt` files. | csproj grep |
| CI | Linux only: Core, Editor, headless UI, E2E (Xvfb) and CLI smoke jobs. | `.github/workflows/ci.yml` |

These gaps mean the most valuable multi-extension tests are the ones that **pin hazard 1 and hazard 2**
before NetPrintsUnreal (likely several cooperating extensions) is written.

---

## 2. Survey by ecosystem

### 2.1 VS Code
1. **Author kit:**
   - `@vscode/test-cli` and `@vscode/test-electron` download a real VS Code build (`version: 'stable' |
     'insiders' | '1.x.y'`) and run Mocha tests inside the **Extension Development Host**, with the full
     API.
   - Config: `files`, `workspaceFolder`, `extensionDevelopmentPath`, `mocha`, `launchArgs`.
2. **Conformance:** none as tests. The manifest is validated by `vsce` at package and publish time.
3. **Multi-extension:**
   - `installExtensions` installs extra extensions before the tests run, and "any `extensionDependencies`
     from the package.json are automatically installed".
   - `--disable-extensions` isolates the extension under test.
   - Nothing runs an interop matrix. **Extension Bisect** exists because conflicts are found in the field:
     it binary-searches the installed set to find the culprit.
   - IDs are unique by marketplace construction (`publisher.name`).
4. **Cross-version:** a declared `engines.vscode` range, plus running tests against `insiders` and
   specific versions. No binary verifier, since JS has no binary compatibility.
5. Sources:
   - https://code.visualstudio.com/api/working-with-extensions/testing-extension
   - https://github.com/microsoft/vscode-test-cli
   - https://code.visualstudio.com/api/references/extension-manifest
   - https://code.visualstudio.com/blogs/2021/02/16/extension-bisect

### 2.2 JetBrains IntelliJ Platform
1. **Author kit:**
   - The test framework is added with `testFramework(TestFrameworkType.Platform)` and other types in the
     IntelliJ Platform Gradle Plugin 2.x.
   - Tests are **model-level functional tests** in a headless environment that uses "real production
     implementations for most components".
   - Base classes: `BasePlatformTestCase` (light, reuses the project), heavy tests (a new project each
     time), and `CodeInsightTestFixture` with testdata directories and markup.
2. **Conformance:** `@ApiStatus` annotations (`Experimental`, `Internal`, `ScheduledForRemoval`,
   `NonExtendable`, `OverrideOnly`) enforced by DevKit inspections. Dynamic-plugin eligibility (unload
   without restart) is checked automatically on the Marketplace.
3. **Multi-extension:**
   - Tests declare plugin dependencies in Gradle (`bundledPlugin(...)`, `plugin(...)`) and those
     plugins load into the test IDE.
   - The **Plugin Verifier** reports "missing plugin dependencies … when plugin A depends on plugin B, but
     plugin B doesn't have a build that's compatible with this IDE".
   - Unload failures are diagnosed with `ide.plugins.snapshot.on.unload.fail`, which writes a heap
     snapshot, and every retention chain "is a memory leak".
4. **Cross-version:** the **Plugin Verifier**:
   - `check-plugin` runs against several IDE builds, and `check-trunk-api` compares a release with trunk.
   - It detects `NoSuchMethodError` and `NoClassDefFoundError`-class problems statically, plus use of
     deprecated, experimental or internal API.
   - It runs through the Gradle `verifyPlugin` task, GitHub Actions, and automatically on the
     Marketplace for every new IDE build.
   - JetBrains also publishes an "Incompatible Changes" list.
5. Sources:
   - https://plugins.jetbrains.com/docs/intellij/testing-plugins.html
   - https://plugins.jetbrains.com/docs/intellij/verifying-plugin-compatibility.html
   - https://github.com/JetBrains/intellij-plugin-verifier
   - https://plugins.jetbrains.com/docs/intellij/dynamic-plugins.html
   - https://plugins.jetbrains.com/docs/intellij/tools-intellij-platform-gradle-plugin-dependencies-extension.html

### 2.3 Roslyn (`Microsoft.CodeAnalysis.Testing`), the closest .NET analogue
1. **Author kit:**
   - One NuGet package per role and language: `Microsoft.CodeAnalysis.CSharp.Analyzer.Testing`,
     `.CodeFix.Testing`, `.CodeRefactoring.Testing` and `.SourceGenerators.Testing`.
   - Test types: `CSharpAnalyzerTest<TAnalyzer, TVerifier>`, `CSharpCodeFixTest<…>` and
     `CSharpSourceGeneratorTest<…>`, all derived from framework-agnostic `AnalyzerTest<TVerifier>`. The
     current recommendation is `DefaultVerifier`; the older per-framework `XUnitVerifier`/`NUnitVerifier`
     packages are superseded.
   - Pattern: set `TestState.Sources`, `AdditionalFiles`, `AdditionalReferences`, `ReferenceAssemblies`
     (for example `ReferenceAssemblies.Net.Net80`), then `ExpectedDiagnostics` / `FixedState` /
     `GeneratedSources`, then `await RunAsync()`. Markup such as `[|…|]` and `{|ID:…|}` marks
     expected spans.
2. **Conformance:** built into `RunAsync`. It checks, beyond the author's assertions, that the fix
   converges (iterations), that the fix-all providers agree, that generator output compiles, and that
   there are no unexpected diagnostics. The author gets "contract" checks for free. This is the key idea
   to copy.
3. **Multi-extension:** the test can override `GetDiagnosticAnalyzers()` or `GetSourceGenerators()` to
   return several analyzers or generators, and can add references. There is no ecosystem-wide
   interop test: analyzer id collisions are left to the `PREFIX####` convention.
4. **Cross-version:**
   - Analyzers pin a minimum `Microsoft.CodeAnalysis` version, and the SDK warns when an analyzer
     references a newer compiler than the host.
   - .NET API baselining exists separately (see 2.8): `PublicApiAnalyzers` (`PublicAPI.Shipped.txt`)
     and ApiCompat / package validation.
5. Sources:
   - https://github.com/dotnet/roslyn-sdk/tree/main/src/Microsoft.CodeAnalysis.Testing (the repository
     is now archived and moved into dotnet/roslyn)
   - https://deepwiki.com/dotnet/roslyn-sdk/2-analyzer-testing-framework
   - https://github.com/dotnet/roslyn-sdk/blob/main/src/Microsoft.CodeAnalysis.Testing/Microsoft.CodeAnalysis.CSharp.Analyzer.Testing/CSharpAnalyzerTest%602.cs

### 2.4 Eclipse / OSGi (Tycho, bnd)
1. **Author kit:**
   - `tycho-surefire-plugin` runs JUnit inside a real OSGi runtime. That runtime contains the test
     bundle, its transitive dependencies resolved from the **target platform**, extra requirements, and
     the harness.
   - `bnd-testing-maven-plugin` does the same for bndtools.
2. **Conformance:** OSGi's resolver *is* the conformance check. Unresolvable `Import-Package` /
   `Require-Bundle` means the bundle doesn't start. `bnd-resolver-maven-plugin` resolves a `.bndrun`
   against a repository index in CI.
3. **Multi-extension:** resolution happens **across all bundles in the runtime**:
   - version ranges on imports;
   - several versions of the same package can coexist, with the resolver wiring each consumer to a
     compatible exporter;
   - `uses:` constraints detect class-space inconsistencies.
   This is the strongest built-in "several plugins with conflicting dependency versions" check in any
   ecosystem. Tycho integration tests typically bring up a whole product (a "feature") to test bundles
   together.
4. **Cross-version:** **semantic-versioning baselining**. `bnd-baseline-maven-plugin` and
   `tycho-apitools:verify` (PDE API Tools) compare against the last release and fail the build when a
   package or bundle version bump is too small for the API change.
5. Sources:
   - https://tycho.eclipseprojects.io/doc/latest/tycho-surefire-plugin/test-mojo.html
   - https://wiki.eclipse.org/Tycho/Testing_with_Surefire
   - https://github.com/bndtools/bnd/blob/master/maven-plugins/README.md
   - https://bnd.bndtools.org/chapters/180-baselining.html
   - https://tycho.eclipseprojects.io/doc/main/tycho-apitools-plugin/verify-mojo.html

### 2.5 Unreal Engine (relevant for NetPrintsUnreal)
1. **Author kit:**
   - The Automation Test Framework lives in C++: simple and complex tests, latent commands,
     **Automation Spec** (BDD), Automation Driver (input), **Functional Testing** (tests as level actors),
     screenshot comparison, and Python/Blueprint editor tests.
   - Plugins carry their own tests and are filtered in *Plugins > Testing*. Runs are headless with
     `-ExecCmds="Automation RunTests …"`, or orchestrated with **Gauntlet**.
2. **Conformance:** none for plugins as such. Epic states principles instead: "don't assume engine
   state; clean up; treat each test as potentially following failures".
3. **Multi-extension:** `.uplugin` `Plugins: [{Name, Enabled, Optional}]` dependencies, which are enabled
   transitively. There is no interop matrix; integrators test their own project's plugin set.
4. **Cross-version:** C++ ABI changes with every minor engine version, so plugins are **rebuilt per
   engine version**. Fab/Marketplace submission requires `RunUAT BuildPlugin -Plugin=… -Package=…`
   against each supported engine, and community CI tools automate that matrix.
5. Sources:
   - https://dev.epicgames.com/documentation/en-us/unreal-engine/automation-test-framework-in-unreal-engine
   - https://dev.epicgames.com/documentation/unreal-engine/automation-spec-in-unreal-engine
   - https://dev.epicgames.com/documentation/unreal-engine/functional-testing-in-unreal-engine
   - https://dev.epicgames.com/documentation/unreal-engine/gauntlet-automation-framework-in-unreal-engine
   - https://github.com/MuddyTerrain/unreal-ci-cd-for-fab

### 2.6 Unity (packages and assembly definitions)
1. **Author kit:**
   - The Unity Test Framework (NUnit) finds tests in any asmdef that references `TestAssemblies`.
   - Packages ship `Tests/Editor` and `Tests/Runtime` asmdefs. A consuming project enables them with
     `"testables": ["com.x.pkg", …]` in `Packages/manifest.json`.
2. **Conformance:** none. Unity's internal "Package Validation Suite" exists but isn't a public author
   kit.
3. **Multi-extension:**
   - `testables` can list **other** packages, so their tests run in the same project. This is the
     "run my neighbours' tests next to mine" idea.
   - Optional inter-package dependencies use asmdef **`versionDefines`** (for example define
     `HDRP_7_1_0_OR_NEWER` when package X ≥ 7.1.0) and **`defineConstraints`**, which compile an
     assembly only when a symbol is defined. Authors test with and without the optional package.
4. **Cross-version:** a declared `unity` / `unityRelease` in `package.json`. Authors test by opening the
   package in several editor versions (CI with game-ci Docker images).
5. Sources:
   - https://docs.unity3d.com/Manual/cus-tests.html
   - https://docs.unity3d.com/Packages/com.unity.test-framework@1.1/manual/workflow-create-test-assembly.html
   - https://docs.unity3d.com/Manual/class-AssemblyDefinitionImporter.html

### 2.7 Godot (GUT / GdUnit4)
1. **Author kit:** the engine has none. Community frameworks (GUT, **GdUnit4**, which supports GDScript
   and C#) run inside the editor or headless through a CLI, with JUnit XML output and a GitHub Action.
2. **Conformance:** none.
3. **Multi-extension:** none beyond enabling several `addons/` in the test project.
4. **Cross-version:** a matrix of Godot versions in CI (the GdUnit4 runner supports several).
5. Sources:
   - https://github.com/godot-gdunit-labs/gdUnit4
   - https://godot-gdunit-labs.github.io/gdUnit4/latest/

This is the counter-example: without a host-owned kit, every author reinvents the harness.

### 2.8 .NET `AssemblyLoadContext` plugin hosts (McMaster `DotNetCorePlugins`) and .NET API compatibility tooling
1. **Author kit:** none. The library is host-side: `PluginLoader.CreateFromAssemblyFile(path,
   sharedTypes: [...], isUnloadable, …)`, plus `EnterContextualReflection()`.
2. **Conformance:** none.
3. **Multi-extension:** this project's **own test suite** is the useful model. Its `test/TestProjects/`
   has purpose-built fixture pairs:
   - `Libv1/v2/v3`, `PrivateDepv1/v2/v3`, `ReferencedLibv1/v2`;
   - `SharedAbstraction.v1/v2`, `TransitiveDep.v1/v2`;
   - `NativeDependency`;
   - `WithOurPluginsPluginContract` / `WithOwnPluginsContract`.
   Tests load two plugins with different private versions of the same library and assert that each sees
   its own version, while shared contract types have a single identity.
4. **Cross-version (Microsoft tooling):**
   - **Package validation** (`EnablePackageValidation`) has three validators: baseline version
     (`PackageValidationBaselineVersion`), compatible runtime, and compatible framework. Suppressions go
     in `CompatibilitySuppressions.xml`.
   - `Microsoft.DotNet.ApiCompat.Tool` does assembly-to-assembly comparison.
   - `Microsoft.CodeAnalysis.PublicApiAnalyzers` tracks `PublicAPI.Shipped.txt` / `Unshipped.txt`
     (RS0016/RS0017).
   - Unloadability testing: `WeakReference` to the ALC plus a loop of
     `GC.Collect(); GC.WaitForPendingFinalizers();` until `!IsAlive`. Known pinning causes: statics,
     strong GC handles, threads, ALC-subclass fields, and cached `MethodInfo`.
5. Sources:
   - https://github.com/natemcmaster/DotNetCorePlugins
   - https://github.com/natemcmaster/DotNetCorePlugins/tree/main/test/TestProjects
   - https://learn.microsoft.com/en-us/dotnet/fundamentals/apicompat/package-validation/overview
   - https://learn.microsoft.com/en-us/dotnet/standard/assembly/unloadability

### 2.9 ReSharper / Rider SDK
1. **Author kit:**
   - `JetBrains.ReSharper.SDK.Tests` has base classes such as `BaseTestWithSingleProject`, and test data
     files with gold output.
   - A test environment is declared with `[ZoneDefinition]` (a zone inheriting `ITestsZone`) and an
     `ExtensionTestEnvironmentAssembly<TZone>` as the `[SetUpFixture]`, while `[ZoneMarker]` /
     `IRequire<…>` scope the components.
2. **Conformance:** the **zone** system is a declared-dependency contract. Components activate only when
   their required zones are active, and tests activate exactly the zones they declare.
3. **Multi-extension:** zones double as the mechanism for testing a plugin together with only the parts
   of the product it depends on. Extra plugins come in through zone requirements.
4. **Cross-version:** plugins are built per wave (SDK version). Marketplace verification is shared with
   IntelliJ for Rider.
5. Sources:
   - https://www.jetbrains.com/help/resharper/sdk/Plugins_Testing.html
   - https://www.jetbrains.com/help/resharper/sdk/Testing_Zones.html
   - https://www.jetbrains.com/help/resharper/sdk/ProjectStructure.html

### 2.10 Grafana plugins (`@grafana/plugin-e2e`)
1. **Author kit:** Playwright fixtures, page models (`panelEditPage`, `explorePage`, …) and custom
   `expect` matchers. The package "checks what version of Grafana is under test and resolves the
   selectors" for that version, and the API is "guaranteed to work with all the latest minor versions of
   Grafana since 8.5.0".
2. **Conformance:** the `@grafana/plugin-validator` / `create-plugin` tooling validates the plugin archive
   and metadata for signing and submission.
3. **Multi-extension:** none.
4. **Cross-version:** the **`e2e-versions` GitHub Action** reads `grafanaDependency` from `plugin.json`
   and produces a matrix (up to 6 versions, latest patches, plus `grafana-dev`). The `playwright-tests`
   job then runs per version. This is the best example of the host shipping a **ready-made
   version-matrix CI** to authors.
5. Sources:
   - https://grafana.com/developers/plugin-tools/e2e-test-a-plugin/
   - https://grafana.com/developers/plugin-tools/e2e-test-a-plugin/ci
   - https://github.com/grafana/plugin-tools/blob/main/packages/plugin-e2e/README.md

### 2.11 Backstage
1. **Author kit:**
   - `@backstage/frontend-test-utils` has **`createExtensionTester(extension)`**, which "starts up an
     entire frontend harness, complete with a number of default features". `.add(otherExtension)` adds
     more extensions, and there are overrides and `renderInTestApp()`.
   - The backend side has `@backstage/backend-test-utils` (`startTestBackend`, mock services).
2. **Conformance:** none as a suite. Extension data refs are typed, so wiring mistakes surface as
   harness errors.
3. **Multi-extension:** `.add()` is an explicit, first-class way to test **an extension with specific
   neighbours** in one harness. This is the direct model for NetPrints' `With(...)`.
4. **Cross-version:** `backstage-cli versions:bump`, with release-line compatibility by convention.
5. Sources:
   - https://backstage.io/docs/frontend-system/building-plugins/testing/
   - https://backstage.io/docs/backend-system/building-plugins-and-modules/testing/

### 2.12 JupyterLab and Obsidian (only what they add)
- **JupyterLab:**
  - **Galata** (`@jupyterlab/galata`) provides Playwright fixtures that start a real JupyterLab, with
    state isolation per test and screenshot baselines.
  - The official `extension-template` scaffolds `ui-tests/`, so the kit arrives **by default in the
    template**. Tests run with every other installed extension present, and there is no isolation
    matrix.
  - Sources: https://github.com/jupyterlab/jupyterlab/tree/main/galata,
    https://www.npmjs.com/package/@jupyterlab/galata
- **Obsidian:**
  - There is no test kit.
  - Multi-plugin safety is **registry-side**: the `obsidian-releases` validation workflow requires `id`
    to be globally unique and not contain "obsidian", and requires `id`, `name` and `description` to
    match `manifest.json`.
  - ID-conflict detection **at publication**, not at runtime.
  - Sources: https://github.com/obsidianmd/obsidian-releases,
    https://docs.obsidian.md/Plugins/Releasing/Submit+your+plugin

---

## 3. Comparison table

| Ecosystem | Author kit (host-provided) | Conformance / contract checks | Several extensions together | Cross-host-version compatibility |
|---|---|---|---|---|
| VS Code | `@vscode/test-cli` + `test-electron`: real host, Mocha | `vsce` manifest validation | `extensionDependencies` auto-installed; `installExtensions`; `--disable-extensions`; Extension Bisect in the field; id = `publisher.name` | Test against `stable`/`insiders`/pinned version; `engines.vscode` |
| IntelliJ | Test framework: light/heavy fixtures, `CodeInsightTestFixture`, testdata | `@ApiStatus` + DevKit inspections; dynamic-plugin checks | Gradle `plugin()`/`bundledPlugin()` into the test IDE; Verifier checks the dependency plugin's compatibility | **Plugin Verifier** (static binary check vs N IDE builds, CI + Marketplace), incompatible-changes list |
| Roslyn | `*.Analyzer/CodeFix/SourceGenerators.Testing`: `Test` + `TestState` + `RunAsync` | Built into `RunAsync` (fix convergence, fix-all, no unexpected diagnostics) | `GetDiagnosticAnalyzers()`/`GetSourceGenerators()` return several | `PublicApiAnalyzers`, ApiCompat, package validation |
| Eclipse/OSGi | Tycho surefire / bnd-testing in a real OSGi runtime | The OSGi resolver (imports, version ranges, `uses:`) | Resolution across the whole runtime; multiple package versions coexist | bnd baseline / PDE API Tools semantic-version baselining |
| Unreal | Automation framework, Spec, Functional tests, Gauntlet | None (guidelines) | `.uplugin` dependencies; project-level integration | Rebuild per engine version (`BuildPlugin`); Fab requires it |
| Unity | UTF + `Tests/*` asmdefs + `testables` | None public | `testables` runs neighbours' tests; `versionDefines`/`defineConstraints` for optional deps | Declared `unity` version; multi-editor CI |
| Godot | None (community GUT/GdUnit4) | None | None | Community CI matrix |
| McMaster plugins | None (host library) | None | **Fixture pairs** for private/shared/transitive version skew | n/a |
| ReSharper/Rider | SDK.Tests base classes + zones | Zones as declared dependency contract | Zone activation scopes neighbours | Per-wave rebuild; Marketplace verification |
| Grafana | `@grafana/plugin-e2e` (Playwright, version-aware selectors) | Plugin validator for submission | None | **`e2e-versions` action** builds a version matrix from `grafanaDependency` |
| Backstage | `createExtensionTester`, `startTestBackend` | Typed extension data | **`.add(neighbour)`** in one harness | Release-line bump tooling |
| JupyterLab / Obsidian | Galata in the template / none | — / registry bot | All installed / **registry-side unique-id check** | — |

---

## 4. Patterns that recur

1. **The real host is the fixture.** Every mature kit boots real production code headless (VS Code EDH,
   IntelliJ light fixture, Tycho OSGi runtime, Backstage harness, Galata) and doesn't hand-roll mocks.
   Mocks appear only at the edges: I/O, UI, network.
2. **A declarative test object plus one `Run`.** Roslyn's `TestState → Expected… → RunAsync()` and
   IntelliJ's testdata-in, gold-out pattern. Authors describe inputs and expectations, and the kit adds
   **implicit contract checks** on every run. The implicit checks are where conformance actually lives.
3. **Golden / "gold" files for generated output.** IntelliJ testdata, ReSharper `.gold`, Roslyn
   `GeneratedSources`, Galata screenshots. This maps directly to NetPrints' translated C#.
4. **Declared dependencies drive the test runtime.** `extensionDependencies` → auto-install, Gradle
   `plugin()`, Unity `testables`, Tycho target platform, ReSharper zones. The same manifest that the host
   uses at runtime tells the kit which neighbours to load.
5. **Named, explicit neighbours, never "all extensions".** Backstage `.add()`, VS Code
   `installExtensions`, Unity `testables`. Nobody tests against an unbounded set; they test against
   *declared* dependencies plus, optionally, a small **curated** set.
6. **Structural namespacing beats conflict detection.** Ids are prefixed by publisher or plugin id
   (VS Code, Obsidian, IntelliJ, Roslyn `PREFIX####`). Conflicts are checked once, centrally, at the
   registry or marketplace. NetPrints already does this for node kinds.
7. **Static verification across host versions, separate from tests.** The IntelliJ Plugin Verifier,
   bnd/PDE baselining, and .NET ApiCompat: a *tool*, not a test suite, run in CI and at the marketplace
   for every new host build.
8. **Two-sided compatibility:** host-side API baselining (don't break) plus consumer-side verification
   (does *this* extension still link against build X).
9. **A version matrix handed to authors** (Grafana `e2e-versions`, VS Code `version:`, Unreal per-engine
   `BuildPlugin`), derived from the *declared* host range in the manifest.
10. **Coexisting dependency versions are tested with purpose-built fixture pairs** (McMaster `Libv1/v2`,
    OSGi multiple exporters). Real third-party extensions are never used for this.

## 5. Anti-patterns seen

- **No host kit, so every author builds their own harness** (Godot, Obsidian). Quality varies, and the
  host can't evolve without breaking unknown harnesses.
- **A full N×N interop matrix of real third-party extensions.** No ecosystem does it, because it is
  combinatorial and flaky. Conflicts get found in the field, hence VS Code's bisect.
- **Testing only against mocks of the host API.** Such tests pass while packaging and type-identity bugs
  (the ALC class of bugs) ship. McMaster's own tests use real assemblies for exactly this reason.
- **A declared version gate with no binary check** (NetPrints today: `netprintsApi` only). A declared
  `1.0` extension can still reference a member removed in a 1.x host if the host broke the API. IntelliJ
  built the Verifier because declared ranges lied.
- **Silent "first wins" without surfacing the loser.** IntelliJ and OSGi report every rejected wiring.
  NetPrints does emit `NPX004`/`NPX006` with the loser named, which is good, and the kit should assert
  on these.
- **Unload promised without a leak test.** IntelliJ needed heap-snapshot tooling because dynamic unload
  silently leaked. If NetPrints ever makes ALCs collectible, it needs the `WeakReference` + GC-loop test
  from day one. JSON resolver caches, Avalonia styles and static events are the likely roots.
- **Test assemblies copied into the extension folder** (`Private=true` on host references) cause
  type-identity splits. Unity asmdef hygiene and the NetPrints `Private=false` rule exist for this, and it
  should be a *checked* rule, not only a documented one.

---

## 6. Recommendation for NetPrints

### (a) Author-facing kit: `NetPrints.Extensibility.Testing` (NuGet)

**Shape** (modelled on Roslyn plus Backstage):

```
NetPrints.Extensibility.Testing        (framework-agnostic; throws ExtensionTestException, like Roslyn DefaultVerifier)
  ExtensionTest<TExtension>            declarative test object → RunAsync()
    .TestState.Extension               InProcess (fast, same ALC) | FromFolder(path) (real ExtensionLoadContext)
    .TestState.Neighbours              additional extensions: folders, in-process, or KnownNeighbours.*
    .TestState.Graphs / Documents      .netprints inputs (testdata files)
    .TestState.Settings / Project      settings JSON, MSBuild properties, profile id
    .ExpectedLoadResults               e.g. Loaded("my.ext"), Failed("x", "NPX006")
    .ExpectedIssues                    contribution issues (NPX006) — default: none
    .ExpectedTranslation               golden C# per class (update with NETPRINTS_UPDATE_GOLDENS=1)
    .ImplicitChecks                    on by default, opt-out flags (see conformance list)
  ExtensionHarness                     lower-level fixture: builds a real ExtensionRegistry + TranslationEnvironment,
                                       exposes Registry, Translate(graph), RoundTrip(doc), Compile(csharp),
                                       HostChannel (InMemoryHostChannel), Settings store
  ExtensionConformance                 static entry: ExtensionConformance.VerifyAsync(folder | TExtension, options)
                                       → report; used by the ExtensionConformanceTests base below
  KnownNeighbours                      shipped fixture extensions: BuiltIns, NoisyNeighbour, (later) popular third-party ids
NetPrints.Extensibility.Testing.XUnit  (optional thin adapter)
  ExtensionConformanceTests<TExtension> abstract class with one [Fact] per conformance rule, so failures are
                                       reported by name. Authors derive once:
                                       public sealed class Conformance : ExtensionConformanceTests<MyExtension> { }
```

Authors dogfood-proof: `tests/NetPrints.TestExtension` should be tested **only** through this kit. That's
how Roslyn keeps its own kit honest.

**Conformance specs** (each is an implicit check in `RunAsync`, and a named test in the base class):

| # | Spec | Catches |
|---|---|---|
| C1 | Manifest: schema, id charset, `netprintsApi` ≤ host, `assembly` exists, exactly one public `INetPrintsExtension` with a public parameterless ctor | NPX001/002 before users see them |
| C2 | Packaging: no shared-prefix assemblies (`NetPrints*`, `Avalonia*`, `Microsoft.CodeAnalysis*`, …) in the extension folder; `.deps.json` present (`EnableDynamicLoading`); **no private dependency whose name matches a shared prefix** (hazard 1) | Type-identity splits; NetPrintsUnreal naming trap |
| C3 | Loads in a real `ExtensionLoadContext` (FromFolder) and `typeof(Node)` identity equals the host's (EX-T01 generalised) | `Private=true` mistakes |
| C4 | `Register` is repeatable and pure: two fresh builders give equivalent contributions; no builder use after return; no exception; bounded time | Hidden static state; order-dependent registration |
| C5 | Alone, zero `NPX006` issues; every kind has the `<id>/` prefix and a consistent converter kind and type | Contribution mistakes |
| C6 | Every node kind round-trips: create → save → load → save is byte-identical; unknown extra properties preserved | Serialization drift |
| C7 | Every node kind translates, and the containing class **compiles** with Roslyn against the project's reference assemblies | Emitting invalid C# |
| C8 | Emitters are deterministic (same output across two runs and under `Parallel.For`), and a no-op on classes they don't target (golden with and without the extension, as in EX-T04) | Non-determinism, thread-safety |
| C9 | Settings: section id == manifest id; defaults round-trip; unknown sections preserved | EX-T09 for authors |
| C10 | Host channel factory: unique non-empty id; `SendAsync` after dispose throws (EX-T08 contract) | Lifecycle bugs |
| C11 | **Coexistence:** loads with `KnownNeighbours.BuiltIns + NoisyNeighbour` with zero issues on both sides, and **its golden C# is unchanged by the neighbour's presence** (and the neighbour's by it) | The cheap version of "test with other popular extensions" |
| C12 | Registry `DisposeAsync` disposes contributions without throwing | Dispose leaks |

`NoisyNeighbour` is a kit-shipped fixture extension. It registers a class emitter and a member emitter
that touch *every* class, a type catalog, a profile, a settings section, a host channel, project
properties and a JSON resolver. It uses generic-but-legal ids, so it collides with anything that squats
on names it shouldn't.

Later, add a `dotnet new netprints-extension` template that scaffolds the extension, a test project with
`Conformance : ExtensionConformanceTests<T>`, and a CI workflow. That's the JupyterLab lesson: the kit is
used when the template includes it.

### (b) Host-side multi-extension tests

**Where:** `tests/NetPrints.Core.Tests/Extensibility/MultiExtension/*`, trait `Category=MultiExtension`.
They run in the existing **Test (Core)** CI job and need no UI.

**Fixture extensions.** Use two kinds, matching the existing split:

- *Roslyn-at-runtime* fixtures, the existing `Compile(folder, name, source)` helper in
  `ExtensionLoaderTests`. Use these for pure registration scenarios. They're cheap and flexible.
- *Prebuilt fixture projects* under `tests/Fixtures/Extensions/*`, built as `ProjectReference` with
  `ReferenceOutputAssembly=false` and copied next to the tests, following the TestExtension pattern. Use
  these wherever a **real `.deps.json` and real NuGet or private dependencies** matter:

| Fixture | Purpose |
|---|---|
| `fx.alpha`, `fx.beta` | Baseline pair. Each has one node kind, a class emitter and settings; beta `dependsOn` alpha |
| `fx.squatter` | Registers profile, host channel and settings ids that alpha already owns, plus a kind prefixed `fx.alpha/…`. Expect `NPX006` per contribution and alpha intact |
| `fx.dup-a`, `fx.dup-b` | Same manifest id in two folders: NPX004, first wins, deterministic |
| `fx.libv1`, `fx.libv2` | Each ships a private `Fixture.SharedLib` at AssemblyVersion 1.0 / 2.0 with a divergent API (McMaster `PrivateDepv1/v2` pattern). Both load, each calls its own version, and the two `Assembly` instances differ |
| `fx.hostskew` | Compiled against a *newer* shared assembly (simulated: a `NetPrints.Core` reference assembly with an extra member, or a newer `Microsoft.CodeAnalysis`). The host copy wins, and first use gives `MissingMethodException`. Expect a clean NPX005/NPX007 or translation diagnostic with the editor alive. Later, `netprints-verify` must flag this statically |
| `fx.private-netprints-prefix` | Private dependency named `NetPrintsFixture.Runtime.dll` (**hazard 1**). Pin current behaviour (fails NPX007) with a test, then fix: share only when Default can resolve it, as R8 intends, or reserve the prefix and document it |
| `fx.types-provider`, `fx.types-consumer` | Consumer `dependsOn` provider and **references provider's assembly** (`Private=false`): its node's pin type and its emitter use provider types (**hazard 2**). Pin current behaviour, then design: the consumer ALC delegates resolution of the provider's assemblies to the provider's ALC, as OSGi `Require-Bundle` or IntelliJ parent classloaders do. Then assert `typeof(ProviderType)` identity across both |
| `fx.throws-midway` | Registers two kinds, then throws in `Register`. Assert **none** of its contributions are committed (atomicity), and that neighbours are unaffected |
| `fx.native` | Private native library, for `LoadUnmanagedDll` through the resolver (McMaster `NativeDependency`) |

**Scenarios:**

1. **ID conflicts:** squatter vs alpha across every contribution type (kinds, profiles, host channels,
   settings, document types, CLR node types, JSON resolvers claiming the same type, duplicate project
   properties dedupe). Assert the loser named in `Issues`, and the winner's behaviour unchanged.
2. **Load order and determinism:**
   - Property-style test: permute discovery order (folder list order, search-directory names) and extension
     sets, then assert an identical `Loaded` order, identical registry order, and **byte-identical
     generated C#**.
   - Cover ties by id, diamond `dependsOn`, and a failed middle node cascading NPX003.
3. **Dependency-version isolation:** libv1 and libv2 together, plus hostskew and the NetPrints-prefix
   cases above.
4. **Extension-on-extension:** types-provider and types-consumer (type identity), and beta's emitter
   decorating alpha's node kinds. When alpha fails, beta gets NPX003 and alpha's documents stay preserved.
5. **Documents across extension sets:**
   - A graph with nodes from alpha and beta is saved, then reopened with {alpha}, {beta} and {}. Unknown
     nodes are preserved inactive (`NPT003`), and re-saving is byte-identical. This extends EX-T03 to
     combinations.
   - The same graph through the **CLI/Generator build path** with an explicit `extension=` list, for
     reproducibility.
6. **Failure isolation in combination:** each NPX00x failure fixture loaded with alpha and beta. The
   others load, and the editor registry stays usable (EX-T10 generalised to N).
7. **Scale smoke:** 50 Roslyn-generated extensions load within a time budget and in deterministic order.
8. **Reload and cache (today):** `ExtensionHost` reload reuses cached contexts, and the count of
   `AssemblyLoadContext.All` stays stable across reloads.
   **Unload (only if collectible is ever adopted):** for provider and consumer, unloading must unload
   dependents first. Use the `WeakReference` + `GC.Collect()/WaitForPendingFinalizers()` loop and assert
   both are collected. Include explicit probes for the known pinning roots: STJ resolvers, Avalonia
   styles and templates, static events, and Roslyn `MetadataReference` caches.

**CI:**

- **Per PR:** the `MultiExtension` tests in the Core job. They're fast and Linux-only, which is enough for
  load-context logic. Add a Windows leg only for `fx.native`.
- **Nightly "ecosystem" job,** added once third-party extensions exist:
  - restore the latest published extensions (NetPrintsUnreal and any on a curated list) from NuGet;
  - run `ExtensionConformance.VerifyAsync` on each, and a **co-load of all of them** against host
    `main`.
  - This is the JetBrains Marketplace idea in miniature: the host learns it broke someone before release.
- **Reusable workflow for authors,** the Grafana `e2e-versions` idea. Publish a GitHub reusable workflow
  or composite action (`netprints/extension-ci`) that reads `netprintsApi` from the manifest, resolves the
  matching host package versions (latest patch of each compatible minor, plus prerelease), and runs the
  author's tests plus `netprints-verify` in a matrix.

### (c) API and binary compatibility across NetPrints versions

Work in layers, cheapest first:

1. **Now (no release yet):**
   - Add `Microsoft.CodeAnalysis.PublicApiAnalyzers` to every assembly an extension compiles against:
     `NetPrints.Extensibility`, `NetPrints.Core`, `NetPrints.Reflection`, `NetPrints.Serialization`.
     Every public API change then becomes a visible `PublicAPI.Unshipped.txt` diff in the PR. This fits
     the "analyzers at error" rule in ADR 0003.
   - Mark unstable surfaces with C# `[Experimental("NPXE…")]`, the analogue of IntelliJ
     `@ApiStatus.Experimental`. Extensions using them get a compile-time diagnostic they must opt into.
2. **At the first published extension-API package:**
   - Make `NetPrints.Extensibility` packable (it isn't today).
   - Keep `EnablePackageValidation` on all four packages, and set `PackageValidationBaselineVersion` to
     the last shipped version. The build then fails on breaking changes against the baseline.
   - Allow suppressions only in `CompatibilitySuppressions.xml`, and only when the release also bumps
     `ExtensionApi.Version.Major`.
   - Add a release-workflow check: a non-empty `PublicAPI.Unshipped.txt` at release requires a minor bump
     of `ExtensionApi.Version`, and any removal from `Shipped` requires a major bump. This ties the
     *declared* `netprintsApi` gate to the *actual* API.
3. **Before the first third-party release (NetPrintsUnreal): `netprints-verify`**, a `dotnet tool`
   modelled on the IntelliJ Plugin Verifier. It takes an extension folder or `.nupkg` and one or more host
   versions (NuGet package versions or local build folders). Using `System.Reflection.Metadata`, it:
   - walks every `TypeRef`/`MemberRef` into shared assemblies and resolves them against **that host's**
     copies. Anything unresolved is reported as "would throw `MissingMethodException` /
     `TypeLoadException`". ApiCompat can't do this, because it compares two versions of one library and
     not a consumer against a library;
   - flags `[Obsolete]` and `[Experimental]` usage;
   - runs the static parts of C1/C2 (manifest, packaging, shared-prefix hazards);
   - reports `dependsOn` targets missing from the given set, and their `netprintsApi` compatibility.

   The kit's conformance suite (C2) and the author CI workflow call it, and so does the nightly ecosystem
   job. Estimated size is a few hundred lines.

### (d) Suggested phasing

| When | What |
|---|---|
| **P1 (merging now)** | Nothing that blocks the merge. Optionally, one small follow-up PR adds two *pinning* tests for hazard 1 (NetPrints-prefixed private dependency) and hazard 2 (extension-on-extension types), so the behaviour is documented, not accidental. Adopt the ADR. |
| **P2 (early)** | (b) The multi-extension suite and fixture extensions. Decide hazard 1 and 2 with an ADR (resolution delegation to dependency ALCs; prefix rule) **before NetPrintsUnreal splits into several extensions**. Add `PublicApiAnalyzers` and `[Experimental]` (c.1). |
| **P2/P3** | (a) The kit, v0 as an internal project consumed by `NetPrints.TestExtension` (dogfood), with C1–C12 and `KnownNeighbours`. Publish it as a preview package with the first public Extensibility package (c.2 then applies). |
| **Before the first NetPrintsUnreal release** | `netprints-verify` (c.3), the author reusable CI workflow, the nightly ecosystem job, and the `dotnet new` template. |
| **Only if hot reload or unload is scheduled** | Collectible ALCs plus the unload/leak test suite (scenario 8 bis). |

---

## 7. Draft ADR (for `docs/adr/`, next free number, currently 0010)

```markdown
# 0010: Extension testing — author kit, multi-extension scenarios, API compatibility gates

## Status

Proposed (2026-09-29).

## Context

NetPrints loads third-party extensions (future: NetPrintsUnreal) from NuGet packages into per-extension,
non-collectible `ExtensionLoadContext`s that share host assemblies by name prefix and resolve everything else
with `AssemblyDependencyResolver`. Extensions register contributions through `IExtensionBuilder`; node kinds are
namespaced by manifest id, other contribution ids are first-wins with `NPX006`, duplicate extension ids are
`NPX004`, and `dependsOn` orders loading topologically. Compatibility is declared (`netprintsApi` major equal,
minor ≤ host) but never verified against the binaries.

Tests are internal only: `ExtensionLoaderTests` compiles fixture extensions with Roslyn and covers NPX001–007
and ordering; `tests/NetPrints.TestExtension` exercises each contribution. There is no kit for extension
authors, no test of several real extensions together, and no API baseline (`NetPrints.Extensibility` is not
packable; package validation has no baseline version). Two behaviours are unpinned: a private dependency whose
name starts with a shared prefix (e.g. `NetPrints*`, `Avalonia*`) is deferred to the Default context and fails;
and `dependsOn` does not let an extension resolve its dependency's assemblies, so extension-on-extension types
are unsupported.

A survey of VS Code, IntelliJ, Roslyn, Eclipse/OSGi, Unreal, Unity, Godot, McMaster DotNetCorePlugins,
ReSharper, Grafana, Backstage, JupyterLab and Obsidian shows: hosts ship the real host as the test fixture with
a declarative test object and implicit contract checks (Roslyn `RunAsync`); multi-extension safety comes from
structural id namespacing, declared dependencies loaded into the test runtime, and named neighbours (Backstage
`.add`) rather than an N×N matrix; dependency-version coexistence is tested with purpose-built fixture pairs
(McMaster); cross-version compatibility is two-sided — host API baselining (ApiCompat, bnd, PDE) plus a
consumer-side static verifier (IntelliJ Plugin Verifier) — and hosts hand authors a version-matrix CI
(Grafana `e2e-versions`).

## Decision

1. **Author kit.** Ship `NetPrints.Extensibility.Testing` (framework-agnostic) and an optional xUnit adapter.
   It provides `ExtensionTest<TExtension>` (declarative `TestState`, expected load results/issues/golden C#,
   `RunAsync`), an `ExtensionHarness` that builds a real `ExtensionRegistry` in-process or from a folder through
   a real `ExtensionLoadContext`, and a conformance suite (manifest, packaging and shared-prefix hygiene, type
   identity, pure/repeatable `Register`, no contribution issues, node round-trip, translation compiles,
   deterministic thread-safe emitters, settings, host channel lifecycle, coexistence with built-ins and a
   kit-shipped "noisy neighbour", disposal). `NetPrints.TestExtension` is tested only through the kit.
2. **Host multi-extension suite.** Add fixture extensions (baseline pair with `dependsOn`, id squatter,
   duplicate id, private-dependency v1/v2, host-assembly skew, shared-prefix private dependency, type
   provider/consumer, throws-mid-register, native dependency) and scenarios for id conflicts, order-permutation
   invariance of the registry and generated C#, dependency-version isolation, extension-on-extension types,
   documents across extension subsets, failure isolation, scale, and reload caching. They run in the Core test
   job on every PR; a nightly job co-loads and conformance-checks published third-party extensions against
   `main` once any exist.
3. **API compatibility.** Track public API with `PublicApiAnalyzers` on Extensibility, Core, Reflection and
   Serialization now; mark unstable API `[Experimental]`. When the extension API is first published, make
   `NetPrints.Extensibility` packable and set `PackageValidationBaselineVersion`; a release check ties
   `ExtensionApi.Version` bumps to Shipped/Unshipped changes. Before the first third-party release, build
   `netprints-verify`, a static verifier that resolves an extension's member references against given host
   versions, and a reusable CI workflow that runs author tests and the verifier over the host versions the
   manifest's `netprintsApi` admits.
4. The shared-prefix and extension-on-extension behaviours are pinned by tests first, then decided in a
   separate ADR before NetPrintsUnreal ships more than one extension.

## Consequences

- Extension authors get the same harness the host uses; host changes that break authors show up in the kit's
  own tests and in the nightly ecosystem job instead of in users' editors.
- Conflicts between extensions are tested by construction (namespacing, named neighbours, fixture pairs), not
  by an unbounded matrix; genuinely unforeseen conflicts still need field diagnostics.
- Breaking the extension API requires an explicit, reviewed suppression and a major `netprintsApi` bump.
- Costs: a new packable project and its public API to maintain; about ten fixture projects; a verifier tool
  (a few hundred lines on `System.Reflection.Metadata`); one extra CI job. Unload testing is deferred until
  collectible contexts are adopted, which would need a leak-test suite before shipping.
```

---

## 8. Source list

- VS Code:
  - https://code.visualstudio.com/api/working-with-extensions/testing-extension
  - https://github.com/microsoft/vscode-test-cli
  - https://code.visualstudio.com/api/references/extension-manifest
  - https://code.visualstudio.com/blogs/2021/02/16/extension-bisect
- IntelliJ:
  - https://plugins.jetbrains.com/docs/intellij/testing-plugins.html
  - https://plugins.jetbrains.com/docs/intellij/verifying-plugin-compatibility.html
  - https://github.com/JetBrains/intellij-plugin-verifier
  - https://plugins.jetbrains.com/docs/intellij/dynamic-plugins.html
  - https://plugins.jetbrains.com/docs/intellij/tools-intellij-platform-gradle-plugin-dependencies-extension.html
- Roslyn:
  - https://github.com/dotnet/roslyn-sdk/tree/main/src/Microsoft.CodeAnalysis.Testing
  - https://deepwiki.com/dotnet/roslyn-sdk/2-analyzer-testing-framework
- Eclipse/OSGi:
  - https://tycho.eclipseprojects.io/doc/latest/tycho-surefire-plugin/test-mojo.html
  - https://wiki.eclipse.org/Tycho/Testing_with_Surefire
  - https://github.com/bndtools/bnd/blob/master/maven-plugins/README.md
  - https://bnd.bndtools.org/chapters/180-baselining.html
  - https://tycho.eclipseprojects.io/doc/main/tycho-apitools-plugin/verify-mojo.html
- Unreal:
  - https://dev.epicgames.com/documentation/en-us/unreal-engine/automation-test-framework-in-unreal-engine
  - https://dev.epicgames.com/documentation/unreal-engine/gauntlet-automation-framework-in-unreal-engine
  - https://github.com/MuddyTerrain/unreal-ci-cd-for-fab
- Unity:
  - https://docs.unity3d.com/Manual/cus-tests.html
  - https://docs.unity3d.com/Manual/class-AssemblyDefinitionImporter.html
- Godot:
  - https://github.com/godot-gdunit-labs/gdUnit4
- .NET:
  - https://github.com/natemcmaster/DotNetCorePlugins
  - https://github.com/natemcmaster/DotNetCorePlugins/tree/main/test/TestProjects
  - https://learn.microsoft.com/en-us/dotnet/fundamentals/apicompat/package-validation/overview
  - https://learn.microsoft.com/en-us/dotnet/standard/assembly/unloadability
- ReSharper:
  - https://www.jetbrains.com/help/resharper/sdk/Plugins_Testing.html
  - https://www.jetbrains.com/help/resharper/sdk/Testing_Zones.html
- Grafana:
  - https://grafana.com/developers/plugin-tools/e2e-test-a-plugin/
  - https://grafana.com/developers/plugin-tools/e2e-test-a-plugin/ci
- Backstage:
  - https://backstage.io/docs/frontend-system/building-plugins/testing/
  - https://backstage.io/docs/backend-system/building-plugins-and-modules/testing/
- JupyterLab:
  - https://github.com/jupyterlab/jupyterlab/tree/main/galata
- Obsidian:
  - https://github.com/obsidianmd/obsidian-releases
  - https://docs.obsidian.md/Plugins/Releasing/Submit+your+plugin
