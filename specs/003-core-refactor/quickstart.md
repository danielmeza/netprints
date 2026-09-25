# Quickstart: Validate P1 on Linux

Prerequisites: .NET 10 SDK (brings `packs/Microsoft.NETCore.App.Ref`), `git` on `PATH` (DF-T23), and for E2E the P0 tools (Xvfb, openbox, xdotool; see
`specs/001-modernize-build/quickstart.md` §5).

## 1. Build and test (two commands, unchanged)

```bash
dotnet build NetPrints.slnx -c Release          # 0 warnings, warnings are errors everywhere (SC-007)
dotnet test --solution NetPrints.slnx -c Release --no-build
```

Focused suites:

```bash
dotnet test --project tests/NetPrints.Core.Tests -c Release -- --filter-namespace "*Serialization*"   # DF-T01…T26
dotnet test --project tests/NetPrints.Core.Tests -c Release -- --filter-namespace "*Extensibility*"   # EX-T01…T13
dotnet test --project tests/NetPrints.Core.Tests -c Release -- --filter-namespace "*Projects*"        # PS-T01…T15
dotnet test --project tests/NetPrints.Editor.Tests -c Release                                          # ED-T02…T15 (VM level)
dotnet test --project tests/NetPrints.Editor.UITests -c Release                                        # ED-T01, T03, T05–T07, T09 (headless)
```

Golden files (C#, documents, notification map, pin keys, `schemas/netpc.v1.schema.json`) and UI baselines
are regenerated only on purpose: `NETPRINTS_UPDATE_SNAPSHOTS=1 dotnet test …`, then review the diff
before committing. The normal suite fails when the committed schema, a sample graph (canonical form) or a
sample `.netpc.g.cs` is out of date (DF-T24, DF-T26).

## 2. US1 — legacy project → `.csproj`, identical C#, builds everywhere

```bash
dotnet build src/NetPrints.Generator -c Release
cp -r tests/NetPrints.Core.Tests/Fixtures/Legacy/HelloWorld /tmp/hw
dotnet exec src/NetPrints.Generator/bin/Release/net10.0/NetPrints.Generator.dll convert /tmp/hw/HelloWorld.netpp
#   → /tmp/hw/HelloWorld.csproj, /tmp/hw/HelloWorld.Program.netpc.json, /tmp/hw/.gitattributes (legacy files untouched)
# outside the repo the package is needed: pack it into a local feed
dotnet pack src/NetPrints.Sdk -c Release -o /tmp/np-feed
printf '<configuration><packageSources><add key="np" value="/tmp/np-feed"/></packageSources></configuration>' > /tmp/hw/nuget.config
dotnet build /tmp/hw/HelloWorld.csproj      # generates HelloWorld.Program.netpc.g.cs, compiles
dotnet run --project /tmp/hw --no-build      # Hello, World!
dotnet build /tmp/hw/HelloWorld.csproj -v n | grep NetPrintsGenerate   # "Skipping target … up-to-date"
```

Expected: the graph JSON matches `contracts/document-format.md` §1.7 byte for byte except the member id
value, and equals the committed `samples/HelloWorld/HelloWorld.Program.netpc.json` (conversion is
deterministic); the
`.netpc.g.cs` equals the golden C# plus the auto-generated header; in VS Code with
`"explorer.fileNesting.patterns": { "*.netpc.json": "${capture}.netpc.g.cs" }` the generated file nests
under the graph. Manual once per release: open `/tmp/hw/HelloWorld.csproj` in Visual Studio 2022/2026 and
Rider, build, and check nesting (research K9).

Editor: `dotnet run --project src/NetPrints.Desktop -c Release -- /tmp/hw/HelloWorld.netpp` asks to
convert, then opens the `.csproj`.

Version control (US1, graph format): in a git repository with the converted sample, open it in the
editor, move one node and save: `git diff` shows one changed line in `layout` and no change to the
`.netpc.g.cs`; save again without changes: nothing is written.

## 3. US2 — MSBuild references and documentation

- Editor: open `samples/HelloWorld/HelloWorld.csproj`, open `Main`, hover the `WriteLine` node:
  the tooltip shows "Writes the specified string value, followed by the current line terminator…".
- Add `<PackageReference Include="Humanizer.Core" Version="2.14.1" />` to a copy of the sample, reopen:
  node search finds `Humanizer.StringHumanizeExtensions`; Compile succeeds.
- References dialog → Add assembly: the `.csproj` gains a `Reference` with `HintPath`; `git diff` shows
  only that item.

## 4. US3 — test extension

```bash
dotnet build tests/NetPrints.TestExtension -c Release
export NETPRINTS_EXTENSION_PATH=$PWD/tests/NetPrints.TestExtension/bin/Release/extensions
dotnet run --project src/NetPrints.Desktop -c Release
```

Expected (user-directory extension): node search in a method graph shows the "Test Extension" category; the generated C# of a
class shows the test emitter's attribute; no error dialog. Rename the manifest's `netprintsApi` to
`2.0`: the startup dialog lists `NPX002` and the editor works. Project-referenced variant: add
`<NetPrintsExtension Include="<path>/netprints.test" />` to a project; opening it asks to trust it, and
`dotnet build` generates code for the extension node without asking.

## 5. US4–US6

- Event graph: class `Program` → Event graphs → New; add "Custom Event" `OnStart` and `OnTick`; the code
  view shows two methods.
- Local variable: open `Main`, Variables panel → "Method: Main" → New; set type `int`; the code view
  shows `int Variable0 = default(int);` at the top of `Main`.
- Code view: highlighted, numbered, foldable; break a connection so an argument is missing → squiggle and
  error row within ~2 s; double-click the row → the node is selected and in view.

## 6. E2E once at the end

`NETPRINTS_E2E=1 dotnet test --project tests/NetPrints.Desktop.E2ETests -c Release` on a private Xvfb
display (≥ :140, `NETPRINTS_E2E_DISPLAY_START`), as in P0.1.
