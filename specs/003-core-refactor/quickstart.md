# Quickstart: Validate P1 on Linux

Prerequisites: .NET 10 SDK (brings `packs/Microsoft.NETCore.App.Ref`), `git` on `PATH` (DF-T23), and for E2E the P0 tools (Xvfb, openbox, xdotool; see
`specs/001-modernize-build/quickstart.md` §5).
For §7 also `unzip`, `zip` and Node 24 (`website/.nvmrc`); `actionlint` 1.7.x for the workflow check.

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

## 7. US8 — release, packages and docs (sub-phase L; nothing is published)

Versions (a full clone is needed; a shallow one falls back to the default version):

```bash
dotnet restore src/NetPrints.Core
dotnet msbuild src/NetPrints.Core/NetPrints.Core.csproj -t:MinVer -getProperty:MinVerVersion -p:Configuration=Release
#   → 0.1.0-alpha.0.<commits since the start> (no tag yet)
```

Packages, the tool and the SDK from the local feed (RL-T01…T05):

```bash
ver=$(scripts/pack-local.sh --print-version)     # → 0.1.0-local.<yyyyMMddHHmmss>, packages in local-packages/
ls local-packages                                 # 4 .nupkg + 3 .snupkg for $ver
scripts/verify-packages.sh local-packages "$ver"  # metadata, tool install, fresh HelloWorld from NetPrints.Sdk
unzip -p local-packages/NetPrints.Core.$ver.nupkg README.md | head   # plain Markdown, absolute links
```

The self-contained editor (RL-T06, research R19):

```bash
dotnet publish src/NetPrints.Desktop -c Release -r linux-x64 --self-contained \
  -p:PublishSingleFile=false -p:PublishTrimmed=false -o /tmp/np-desktop
scripts/smoke-desktop.sh /tmp/np-desktop
# or by hand, no display needed:
env -u DISPLAY /tmp/np-desktop/NetPrints.Desktop --check-project samples/HelloWorld/HelloWorld.csproj --run
#   references: 167 (System.Console: …/packs/Microsoft.NETCore.App.Ref/10.0.x/ref/net10.0/System.Console.dll)
#   analysis: 0 errors, 0 warnings / build: succeeded / Hello, World! / run: exit 0
scripts/archive-desktop.sh /tmp/np-desktop 0.0.0-test linux-x64 && tar -tzf artifacts/desktop/NetPrints-0.0.0-test-linux-x64.tar.gz | head -3
```

Docs site (RL-T08, RL-T09):

```bash
scripts/build-docs.sh                         # DocFX → docs/api/_site, Docusaurus → website/build, + api/ and schemas/
(cd website && npx docusaurus serve --dir build)   # http://localhost:3000/netprints/ ; API at /netprints/api/
```

Workflows (RL-T10, RL-T11): `actionlint .github/workflows/*.yml` reports nothing. On the PR, the **Release**
workflow runs as a dry run (its `pull_request` trigger): `pack`, `desktop` ×3 and `assets` green,
`publish-nuget` and `github-release` skipped; download the `release-assets` artifact and run
`sha256sum -c SHA256SUMS.txt`. **Docs** builds; its `deploy` job is skipped. **Wiki** does not run.

Do **not** push a `v*` tag, run `dotnet nuget push`, or set `PUBLISH_DOCS`/`PUBLISH_WIKI` during P1.

## 8. Owner one-time steps (after merge; the P1 PR needs none)

Exact steps in [contracts/release-and-docs.md](./contracts/release-and-docs.md) §11, copied to
`docs/contributing/releasing.md`:

1. nuget.org Trusted Publishing policy (`danielmeza`/`netprints`, `release.yml`, environment `release`) and the
   `NUGET_USER` secret (profile name, not the email).
2. Environment `release` (optional reviewers, tag rule `v*`).
3. Pages source "GitHub Actions", then variable `PUBLISH_DOCS=true`.
4. Wiki on, first page created in the web UI, then variable `PUBLISH_WIKI=true`.
5. Labels `breaking-change` and `ignore-for-release`.
6. First release: `git tag v0.1.0 && git push origin v0.1.0`.

Until each step is done, the job that needs it is skipped (`PUBLISH_*`) or fails with a message naming the
step (`NUGET_USER` on a tag); pull requests and dry runs never depend on them.

