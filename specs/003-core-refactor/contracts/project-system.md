# Contract: Project system — `NetPrints.Sdk`, generator, MSBuild project model, conversion

Owner decision (2026-09-25): a NetPrints project is an SDK-style `.csproj`. One generation mechanism:
MSBuild → `Exec` → `NetPrints.Generator` (research R11–R15). Test obligations (`PS-Txx`) at the end.

| Artifact | Project / path |
|---|---|
| NuGet package `NetPrints.Sdk` | `src/NetPrints.Sdk/NetPrints.Sdk.csproj` (pack-only), `src/NetPrints.Sdk/build/NetPrints.Sdk.props`, `…/build/NetPrints.Sdk.targets` |
| Generator tool | `src/NetPrints.Generator/` (Exe, net10.0; refs Core, Serialization, Extensibility); packed to `tools/net10.0/` of `NetPrints.Sdk` |
| `IProjectSystem` and model records | `src/NetPrints.Core/Projects/*.cs` (UI-free, no MSBuild dependency) |
| MSBuild implementation | `src/NetPrints.Workspace/` (new; Microsoft.Build.Locator, Microsoft.CodeAnalysis.Workspaces.MSBuild, Microsoft.Build(.Framework) compile-only) |

## 1. Project file

New projects (profile template of `DefaultProjectProfile`, extension-points.md §5):

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>MyNamespace</RootNamespace>
    <NetPrintsProfile>netprints.default</NetPrintsProfile>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="NetPrints.Sdk" Version="$(editor version)" PrivateAssets="all" />
  </ItemGroup>

</Project>
```

| Setting (P0 name) | MSBuild | Notes |
|---|---|---|
| `Project.Name` | project file name without `.csproj` (`$(MSBuildProjectName)`) | rename = rename file (not in P1 UI) |
| `DefaultNamespace` | `RootNamespace` | default `$(MSBuildProjectName)` |
| `OutputBinaryType` | `OutputType` (`Exe` ↔ `Executable`, `Library` ↔ `SharedLibrary`) | editable (PAR-07) |
| `CompilationOutput` | removed | spec clarification; sources always generated, binaries in `bin/` |
| `TargetFramework` | `TargetFramework` | read-only in P1 UI; multi-targeting (`TargetFrameworks`) → first TFM is used by the editor, NPW004 info |
| `ProfileId` | `NetPrintsProfile` | default `netprints.default` |
| `AssemblyReference` | `<Reference Include="<simple name>"><HintPath>relative\path.dll</HintPath></Reference>` | References dialog "Add assembly" |
| `SourceDirectoryReference` (included) | `<Compile Include="<dir>/**/*.cs" NetPrintsSourceDirectory="true" />` | "Add source directory" |
| `SourceDirectoryReference` (excluded) | `<None Include="<dir>/**/*.cs" NetPrintsSourceDirectory="true" />` | the toggle moves the item between `Compile` and `None` |
| NuGet / project references | `PackageReference`, `ProjectReference` | shown read-only in the dialog; edited by hand/IDE in P1 |
| Extensions used by the project | `<NetPrintsExtension Include="<folder containing netprints-extension.json>" />` | usually added by the extension package's `build/*.props` |
| Project-scope extension settings | MSBuild properties named by the extension (convention `<ExtensionPascalName>*`) | read via `ProjectSnapshot.GetProperty` |

## 2. `NetPrints.Sdk` package

Package: `DevelopmentDependency=true`, `IncludeBuildOutput=false`, `SuppressDependenciesWhenPacking=true`,
contents `build/NetPrints.Sdk.props`, `build/NetPrints.Sdk.targets`, `tools/net10.0/**` (framework-dependent
publish of `NetPrints.Generator`). Versioned with the repo (`Version` from `Directory.Build.props`).

`build/NetPrints.Sdk.props`:

```xml
<Project>
  <PropertyGroup>
    <EnableDefaultNetPrintsGraphItems Condition="'$(EnableDefaultNetPrintsGraphItems)' == ''">true</EnableDefaultNetPrintsGraphItems>
    <NetPrintsGeneratorPath Condition="'$(NetPrintsGeneratorPath)' == ''">$(MSBuildThisFileDirectory)../tools/net10.0/NetPrints.Generator.dll</NetPrintsGeneratorPath>
    <NetPrintsSdkVersion>$(version)</NetPrintsSdkVersion>
  </PropertyGroup>
</Project>
```

`build/NetPrints.Sdk.targets` (normative; verified shape in research R12/R15):

```xml
<Project>
  <ItemGroup Condition="'$(EnableDefaultNetPrintsGraphItems)' == 'true'">
    <NetPrintsGraph Include="**/*.netpc.json" Exclude="$(DefaultItemExcludes);$(DefaultExcludesInProjectFolder)" />
  </ItemGroup>
  <ItemGroup>
    <NetPrintsGraph Update="@(NetPrintsGraph)" GeneratedFile="%(RootDir)%(Directory)%(Filename).g.cs" DependentUpon="%(Filename)%(Extension)" />
    <Compile Remove="**/*.netpc.g.cs" />
    <Compile Include="@(NetPrintsGraph->'%(GeneratedFile)')" />
    <UpToDateCheckInput Include="@(NetPrintsGraph)" />
  </ItemGroup>

  <Target Name="NetPrintsGenerate"
          BeforeTargets="CoreCompile"
          Condition="'@(NetPrintsGraph)' != ''"
          Inputs="@(NetPrintsGraph);$(NetPrintsGeneratorPath);$(MSBuildProjectFullPath);@(NetPrintsExtension->'%(FullPath)/netprints-extension.json')"
          Outputs="@(NetPrintsGraph->'%(GeneratedFile)')">
    <PropertyGroup>
      <_NetPrintsHost>$(DOTNET_HOST_PATH)</_NetPrintsHost>
      <_NetPrintsHost Condition="'$(_NetPrintsHost)' == '' and '$(NetCoreRoot)' != ''">$(NetCoreRoot)dotnet$(_NetPrintsExeExtension)</_NetPrintsHost>
      <_NetPrintsHost Condition="'$(_NetPrintsHost)' == ''">dotnet</_NetPrintsHost>
      <_NetPrintsRequest>$(IntermediateOutputPath)netprints.generate.rsp</_NetPrintsRequest>
    </PropertyGroup>
    <ItemGroup>
      <_NetPrintsRequestLine Include="project=$(MSBuildProjectFullPath)" />
      <_NetPrintsRequestLine Include="rootNamespace=$(RootNamespace)" />
      <_NetPrintsRequestLine Include="profile=$(NetPrintsProfile)" />
      <_NetPrintsRequestLine Include="@(NetPrintsExtension->'extension=%(FullPath)')" />
      <_NetPrintsRequestLine Include="@(NetPrintsGraph->'graph=%(FullPath)|%(GeneratedFile)')" />
    </ItemGroup>
    <WriteLinesToFile File="$(_NetPrintsRequest)" Lines="@(_NetPrintsRequestLine)" Overwrite="true" WriteOnlyWhenDifferent="true" />
    <Exec Command="&quot;$(_NetPrintsHost)&quot; exec &quot;$(NetPrintsGeneratorPath)&quot; generate &quot;$(_NetPrintsRequest)&quot;"
          WorkingDirectory="$(MSBuildProjectDirectory)" />
    <Touch Files="@(NetPrintsGraph->'%(GeneratedFile)')" />
  </Target>
</Project>
```

`_NetPrintsExeExtension` = `.exe` when `$([MSBuild]::IsOSPlatform('Windows'))`, else empty (set in the props).
The request is a UTF-8 line file (no JSON, so no path escaping in MSBuild): `key=value` per line, keys
`project`, `rootNamespace`, `profile` (once each), `extension` (0..n), `graph` (1..n, `<input>|<output>`).
In partial builds MSBuild passes only the out-of-date graphs. Example:

```text
project=/work/App/App.csproj
rootNamespace=App
profile=netprints.default
graph=/work/App/Program.netpc.json|/work/App/Program.netpc.g.cs
```

Exec's default canonical-error parsing turns generator output lines into MSBuild errors/warnings.

### 2.1 In-repo development mode (samples and tests)

The package is not published during P1 development, so repository projects import the targets directly:

- `samples/Directory.Build.props`: `<NetPrintsUseLocalSdk>true</NetPrintsUseLocalSdk>`,
  `<NetPrintsGeneratorPath>$(MSBuildThisFileDirectory)../src/NetPrints.Generator/bin/$(Configuration)/net10.0/NetPrints.Generator.dll</NetPrintsGeneratorPath>`
  (`Configuration` defaults to `Debug`), then `<Import Project="../src/NetPrints.Sdk/build/NetPrints.Sdk.props" />`.
  It deliberately does not import the repository root props (samples are user-style projects).
- `samples/Directory.Build.targets`: `<Import Project="../src/NetPrints.Sdk/build/NetPrints.Sdk.targets" />`.
- `samples/Directory.Packages.props`: `<ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>`.
- Project files keep the normal `PackageReference Include="NetPrints.Sdk"` with
  `Condition="'$(NetPrintsUseLocalSdk)' != 'true'"`, so a copied sample works with the published package.
- Tests that copy a sample into a temp directory write the same three files with absolute paths through
  `tests/NetPrints.Core.Tests/Projects/LocalSdkLayout.cs`; the test projects reference
  `src/NetPrints.Generator` with `ReferenceOutputAssembly=false` so it is built first.
- Package mode is covered by PS-T05 only.

## 3. Generator — `src/NetPrints.Generator`

```csharp
namespace NetPrints.Generator;

public sealed record GenerateRequest(string ProjectPath, string? RootNamespace, string Profile,
    IReadOnlyList<GraphJob> Graphs, IReadOnlyList<string> Extensions);
public sealed record GraphJob(string Input, string Output);

public sealed record GeneratedFileResult(string Input, string Output, bool Written, IReadOnlyList<CodeDiagnostic> Diagnostics);

public sealed class GraphCodeGenerator
{
    public GraphCodeGenerator(ExtensionRegistry extensions, DocumentFormatRegistry formats, IDocumentMapper mapper);
    public Task<IReadOnlyList<GeneratedFileResult>> GenerateAsync(GenerateRequest request, CancellationToken cancellationToken);
    public static string RenderFile(TranslatedClass translated, string graphFileName); // header + code, "\n" line endings
}

public static class GenerateRequestFile
{
    public static GenerateRequest Parse(string path);   // FormatException (exit code 2) on unknown key, missing project/profile, or a graph line without "|"
}

internal static class Program   // "generate <request.rsp>" | "convert <legacy.netpp>"
{
    public static Task<int> Main(string[] args);
}
```

| Rule | Contract |
|---|---|
| File content | `// <auto-generated>\n//     Generated by NetPrints <NetPrintsSdkVersion> from <graph file name>. Do not edit.\n// </auto-generated>\n` + `TranslatedClass.Code` with `\r\n` → `\n`, ending with one `\n`. Deterministic: no timestamps, no absolute paths. |
| Write | Only when content differs (`Written`); the target `Touch`es all outputs afterwards. Atomic (temp + move). |
| Errors | One line per diagnostic on stdout in MSBuild canonical format: `<graph path>: error NPT001: <message> (graph <key>, node <id>)`; document errors as `<graph path>(<line>,<col>): error NPD…: …`. A graph with errors keeps its previous `.g.cs` (not deleted). |
| Exit codes | `0` success (warnings allowed); `1` at least one error; `2` bad arguments / request; `3` internal exception (stack trace on stderr). |
| Extensions | `ExtensionLoader` with `SearchDirectories = []` and the request's `Extensions` as explicit extension folders (build = trusted); user extension dirs are **not** used, for reproducible builds. |
| `convert` | Runs `ProjectConverter.ConvertAsync` (§5) and prints the created files; exit 1 on `NPM` errors. Used by tests and the CLI; the editor calls the library directly. |
| Dependencies | Never references `Microsoft.Build*` or Avalonia. |

## 4. `IProjectSystem` — `src/NetPrints.Core/Projects/*.cs`

```csharp
namespace NetPrints.Projects;

public sealed record ResolvedAssembly(string Path, string? DocumentationPath);   // moved from References

public enum ProjectMessageSeverity { Info, Warning, Error }
public sealed record ProjectMessage(ProjectMessageSeverity Severity, string Code, string Message, string? File, int? Line, int? Column);

public sealed record ProjectSnapshot(
    string ProjectFilePath,
    string Name,
    string RootNamespace,
    string AssemblyName,
    BinaryType OutputType,
    string TargetFramework,
    string ProfileId,
    bool ReferencesNetPrintsSdk,
    IReadOnlyList<string> GraphFiles,                 // full paths, ordinal-sorted
    IReadOnlyList<string> ExtensionFolders,           // NetPrintsExtension items, full paths, project order
    IReadOnlyList<ResolvedAssembly> References,       // ordinal by path
    IReadOnlyList<ProjectReferenceInfo> DeclaredReferences, // for the References dialog
    IReadOnlyList<SourceFile> OtherSources,           // Compile documents except *.netpc.g.cs (incl. obj/ generated usings)
    string CompilationOptionsJson,                    // serialized language version, nullable, usings; consumed by CodeAnalysisSession
    IReadOnlyDictionary<string, string> Properties,   // evaluated properties requested via ProjectSystemOptions.ExtraProperties
    IReadOnlyList<ProjectMessage> Messages)
{
    public string? GetProperty(string name);
}

public enum DeclaredReferenceKind { Package, Project, Assembly, SourceDirectory }
public sealed record ProjectReferenceInfo(DeclaredReferenceKind Kind, string Include, string? Version, bool Included, bool Editable);

public abstract record ProjectEdit
{
    public sealed record SetOutputType(BinaryType Value) : ProjectEdit;
    public sealed record SetProfile(string ProfileId) : ProjectEdit;
    public sealed record AddAssemblyReference(string AssemblyPath) : ProjectEdit;        // HintPath relative to the project dir when under it
    public sealed record AddSourceDirectory(string DirectoryPath) : ProjectEdit;
    public sealed record SetSourceDirectoryIncluded(string DirectoryPath, bool Included) : ProjectEdit;
    public sealed record RemoveReference(DeclaredReferenceKind Kind, string Include) : ProjectEdit;
    public sealed record AddNetPrintsSdk(string Version) : ProjectEdit;
}

public sealed record BuildResult(bool Success, IReadOnlyList<ProjectMessage> Messages, string? OutputAssemblyPath, string Log);

public interface IProjectSystem
{
    Task<ProjectSnapshot> LoadAsync(string projectFilePath, CancellationToken cancellationToken);
    Task<ProjectSnapshot> ApplyAsync(string projectFilePath, IReadOnlyList<ProjectEdit> edits, CancellationToken cancellationToken);
    Task<string> CreateAsync(string directory, string projectName, IProjectProfile profile, string rootNamespace, CancellationToken cancellationToken); // returns .csproj path
    Task<BuildResult> BuildAsync(string projectFilePath, CancellationToken cancellationToken);
    ProcessStartRequest GetRunCommand(string projectFilePath);   // dotnet run --project <p> --no-build
}

public sealed record ProcessStartRequest(string FileName, IReadOnlyList<string> Arguments, string WorkingDirectory);
```

| Member | Contract |
|---|---|
| `LoadAsync` | 1) `ProjectSystemException(NPW001)` if no .NET SDK was registered. 2) Restore out of process when `obj/project.assets.json` is missing or older than the project file or any imported `Directory.*.props/targets` (restore failure → `Error` message `NPW002`, continue). 3) In-process evaluation (properties, items; evaluation failure → `ProjectSystemException(NPW003)` with MSBuild's message). 4) `MSBuildWorkspace.OpenProjectAsync` for references, documents and options; `WorkspaceFailed` diagnostics → `Warning` messages `NPW005`. `DocumentationPath` = sibling `.xml` of each reference if it exists. Idempotent; no files written except by restore. |
| `ApplyAsync` | Edits with `ProjectRootElement.Open(path, ProjectCollection, preserveFormatting: true)`, saves once (atomic temp + move), then returns `LoadAsync`. Unknown/duplicate items: `AddAssemblyReference` of an existing HintPath (case-insensitive) is a no-op (PAR-17). `RemoveReference` of a non-editable (package/project) reference → `ArgumentException`. |
| `CreateAsync` | Writes `<directory>/<projectName>.csproj` from `profile.ProjectTemplate` (placeholders `{ProjectName}`, `{RootNamespace}`, `{TargetFramework}`, `{NetPrintsSdkVersion}`, `{ProfileId}`); `IOException` if it exists. |
| `BuildAsync` | `dotnet build "<p>" -nologo -tl:off -v:quiet -clp:NoSummary` out of process (environment `DOTNET_CLI_UI_LANGUAGE=en`, `MSBUILDTERMINALLOGGER=off`); stdout/stderr collected into `Log`; `Messages` parsed by `MsBuildMessageParser` (§4.1), de-duplicated, ordered by file, line, code. Cancellation kills the process tree. `OutputAssemblyPath` from `-getProperty:TargetPath` in the same invocation. |
| Threading | All members are thread-safe; evaluation calls are serialized internally (one `ProjectCollection` per call, disposed). |
| Registration | `MsBuildRegistration.EnsureRegistered()` (`src/NetPrints.Workspace/MsBuildRegistration.cs`): exactly the UnrealSharp logic (`QueryVisualStudioInstances().OrderByDescending(Version)` → `RegisterInstance`, else `RegisterDefaults`); idempotent; must run before any `Microsoft.Build` type is loaded (Desktop `Main`, CLI `Main`, `[ModuleInitializer]` in test projects that touch MSBuild). Returns `false` when no SDK is found. |

`MsBuildProjectSystem(ProjectSystemOptions options, IProcessRunner processes, ILogger<MsBuildProjectSystem> logger)`
in `src/NetPrints.Workspace/MsBuildProjectSystem.cs`; `ProjectSystemOptions(IReadOnlyList<string> ExtraProperties)`
(profile/extension properties to capture). `IProcessRunner` (`src/NetPrints.Core/Projects/IProcessRunner.cs`):
`Task<ProcessResult> RunAsync(ProcessStartRequest request, CancellationToken ct)` with
`ProcessResult(int ExitCode, string StandardOutput, string StandardError)`; the Desktop/CLI implementation uses
`System.Diagnostics.Process`; tests use the real one (SDK present in CI).

### 4.1 `MsBuildMessageParser` — `src/NetPrints.Workspace/MsBuildMessageParser.cs`

`public static IReadOnlyList<ProjectMessage> Parse(string output)`. Canonical format
`^(?<file>.+?)(\((?<line>\d+)(,(?<col>\d+))?\))?\s*:\s*(?<sev>error|warning|info)\s+(?<code>[A-Z]+\d+)\s*:\s*(?<msg>.*?)(\s+\[[^\]]+\])?$`
(culture-invariant, `RegexOptions.Multiline`). NetPrints generator messages keep the `(graph <key>, node <id>)`
suffix inside `msg`; `DiagnosticMapper` (editor) extracts it. Lines that don't match are ignored.

## 5. Conversion of legacy projects — `src/NetPrints.Serialization/Legacy/ProjectConverter.cs`

```csharp
namespace NetPrints.Serialization.Legacy;

public sealed record ConversionResult(string ProjectFilePath, IReadOnlyList<string> GraphFiles, IReadOnlyList<DocumentIssue> Issues);

public sealed class ProjectConverter
{
    public ProjectConverter(LegacyXmlDocumentFormat legacy, JsonDocumentFormat json, IProjectProfile defaultProfile, string netPrintsSdkVersion, ILogger<ProjectConverter> logger);
    public Task<ConversionResult> ConvertAsync(string legacyProjectPath, CancellationToken cancellationToken);
}
```

| Legacy (`.netpp` / `.netpc`, DataContract) | Converted |
|---|---|
| file `<Dir>/<Name>.netpp` | `<Dir>/<Name>.csproj` from `DefaultProjectProfile.ProjectTemplate`; exists → `ProjectConversionException(NPM002)`, nothing written |
| `DefaultNamespace` | `RootNamespace` |
| `OutputBinaryType` | `OutputType` |
| `CompilationOutput`, `SaveVersion`, `LastCompiledAssemblyPath` | dropped |
| `FrameworkAssemblyReference(".NETFramework/…")` | dropped; one issue `NPM001` ("legacy .NET Framework references replaced by net10.0") |
| `AssemblyReference(path)` | `<Reference Include="<file name>"><HintPath>…</HintPath></Reference>` (relative if under `<Dir>`); missing file → still written + issue `NPM003` |
| `SourceDirectoryReference(dir, include)` | `Compile`/`None` item with `NetPrintsSourceDirectory="true"` (§1) |
| `Compiled_<Name>/` folder present | `<DefaultItemExcludes>$(DefaultItemExcludes);Compiled_*/**</DefaultItemExcludes>` in the new project |
| each `ClassPaths` entry `X.netpc` | `X.netpc.json` in the same folder (legacy import → mapper → JSON, document-format.md §3); an existing `X.netpc.json` → `NPM002` |
| `.netpc` files not listed in `ClassPaths` | ignored, issue `NPM004` |
| order of writes | all graph files first, then the `.csproj`; any exception deletes files written by this conversion |

Legacy files are opened read-only and never written, moved or deleted (FR-007). After conversion the
caller opens the new `.csproj`. Generated `.g.cs` files appear on the first build (or immediately when the
editor saves, §6).

## 6. Editor integration (summary; details in editor-services.md)

- Open accepts `*.csproj` and `*.netpp`; a `.netpp` is converted first (confirmation dialog listing the
  files to be written), then the `.csproj` opens.
- Graphs are loaded from `ProjectSnapshot.GraphFiles` through `ProjectPersistence` (document-format.md §2.8).
- Save writes changed graph files **and** their `.netpc.g.cs` via `GraphCodeGenerator.RenderFile` (same
  output as the build), so the working tree is consistent without a build.
- Compile = save all → `IProjectSystem.BuildAsync`; Run = Compile → `GetRunCommand` via `IProcessLauncher`
  with captured output (P0 D3 Output pane).
- New Project = folder picker + name → `CreateAsync` → open.
- New Class = new graph file `<RootNamespace>.<Name>.netpc.json` in the project folder (default glob picks it up).
- If `ReferencesNetPrintsSdk` is false: banner with "Add NetPrints.Sdk" (`AddNetPrintsSdk` edit).

## 7. Test obligations

| ID | Case |
|---|---|
| PS-T01 | Targets (in-repo dev import, temp project): first `dotnet build` generates all graphs and compiles; second build logs "Skipping target NetPrintsGenerate"; touching one graph regenerates only it (build log assertions) |
| PS-T02 | Never twice: with `.g.cs` present before evaluation, `dotnet msbuild -getItem:Compile` lists each generated file once with `DependentUpon=<graph file name>`; build has no CS2002/CS0101 |
| PS-T03 | `EnableDefaultNetPrintsGraphItems=false` + explicit `NetPrintsGraph` item works; graph outside project folder via item works |
| PS-T04 | Generator error: invalid graph → build fails with `NPD`/`NPT` error pointing to the graph file; previous `.g.cs` kept |
| PS-T05 | Package mode: `dotnet pack src/NetPrints.Sdk` into a temp feed; a temp project with `PackageReference` + `nuget.config` builds and generates (one CI test) |
| PS-T06 | Generated file header, `\n` endings, determinism (two builds → identical bytes); content equals `RenderFile` of the editor translation |
| PS-T07 | `LoadAsync` on HelloWorld: properties, graph files, `System.Console.dll` from a reference pack with `DocumentationPath`; restore runs when `obj/` is missing and not when current |
| PS-T08 | `LoadAsync` with a `PackageReference` (local test package) and a `ProjectReference`: both resolved |
| PS-T09 | `ApplyAsync`: each `ProjectEdit`; untouched parts of the file byte-identical (comments, formatting); duplicate assembly no-op |
| PS-T10 | `MsBuildMessageParser`: csc error with path/line/col/code, MSB/NU warnings, generator line with graph/node suffix, non-matching lines ignored |
| PS-T11 | `BuildAsync`/run: converted HelloWorld builds and prints `Hello, World!` via `GetRunCommand` |
| PS-T12 | Conversion: HelloWorld and AllNodes fixtures → `.csproj` + `.netpc.json`; legacy files byte-identical and timestamps unchanged; existing `.csproj` → `NPM002` and nothing written; `Compiled_*` excluded; `NPM001` issued |
| PS-T13 | No SDK (Locator registration returns false, simulated through `ProjectSystemOptions`/fake registration) → `NPW001` message path in the editor (VM test) |
| PS-T14 | Extensions in the build: a project with a `NetPrintsExtension` item to the test extension generates code for the extension node; without the item → `NPT003` error |
