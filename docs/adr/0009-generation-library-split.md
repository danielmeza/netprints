# 0009: Split `NetPrints.Generation` out of `NetPrints.Generator`

## Status

Accepted (2026-09-28). Supersedes ADR-0005's "Editor → Generator project reference" subsection.

## Context

ADR-0005 gave `NetPrints.Editor` a `ProjectReference` to `NetPrints.Generator.csproj` (an
`OutputType=Exe` project) with `ReferenceOutputAssembly="false"`, paired with a plain
`<Reference Include="NetPrints.Generator">` whose `HintPath` pointed at Generator's own,
non-RID build output. That combination was meant to keep Generator out of the .NET SDK's
"referenced executable" publish graph (NETSDK1067/1150/1151) while still giving the Editor
`GraphCodeGenerator` at compile time.

It does not survive a clean clone. `dotnet build src/NetPrints.Editor -c Release -r linux-x64` (and
`dotnet build NetPrints.slnx -r <rid>`) fails with `CS0234: 'Generator' does not exist in namespace
'NetPrints'`, because the RID flows to Generator and its build output moves to
`bin/Release/net10.0/linux-x64/`, a path the `HintPath` never accounted for. Worse, in a tree that
already has a stale non-RID build sitting in `bin/Release/net10.0/`, the same command compiles
silently against that stale `NetPrints.Generator.dll` instead of failing. The documented CI and
publish commands (`dotnet publish src/NetPrints.Desktop -r …`) happened to avoid this because the
Editor is RID-agnostic behind Desktop, which hid the defect from every exercised path.

The actual NETSDK1150 fix Microsoft documents for "a library-like `Exe` project referenced by an
app that itself gets RID-published" is to stop referencing the `Exe` project at all: move the shared
code into a library both sides reference normally.

## Decision

Extract `GraphCodeGenerator` and `GenerateRequestFile` (with their `GraphJob`/`GenerateRequest`/
`GeneratedFileResult` records) out of `NetPrints.Generator` into a new class library,
`NetPrints.Generation` (namespace `NetPrints.Generation`), referencing `NetPrints.Core`,
`NetPrints.Extensibility` and `NetPrints.Serialization` exactly as Generator did.

- `NetPrints.Generator` keeps only `Program.cs` (the `generate <request.rsp>` CLI entry point,
  `UseAppHost=false`, always run via `dotnet exec` from `NetPrints.Sdk.targets`) and becomes a thin
  host: a plain `ProjectReference` to `NetPrints.Generation`.
- `NetPrints.Editor` (`MainEditorVM`/`ClassEditorVM`) takes a plain `ProjectReference` to
  `NetPrints.Generation` — a normal library reference, not to an `Exe` project — so
  `ReferenceOutputAssembly="false"` and the `HintPath` workaround are gone entirely. There is no
  `Exe` project on the Editor's reference graph anymore, so a self-contained, RID-published build
  has nothing NETSDK1150/1151/1067 to trip over, on any RID.
- `NetPrints.Sdk.csproj`'s own build-order-only reference to `NetPrints.Generator.csproj`
  (`ReferenceOutputAssembly="false"`, so the generator builds before the SDK package packs) is
  unchanged: publishing `NetPrints.Generator` (framework-dependent, into `tools/net10.0/`) already
  pulls in its own project references transitively, so the packed `NetPrints.Sdk` package gains one
  extra file, `NetPrints.Generation.dll`, alongside `NetPrints.Generator.dll` — the package shape
  (`build/`, `tools/net10.0/`, no `lib/`) is unchanged.

## Consequences

- A clean-clone `dotnet build`/`dotnet publish -r <rid>` of the Editor or Desktop no longer depends
  on a same-configuration Generator build having happened first, and cannot silently compile against
  a stale Generator DLL — the failure mode ADR-0005 accepted as a documented limitation is gone.
- `docs/adr/0005-release-and-docs-stack.md`'s "Editor → Generator project reference" subsection and
  its "do not simplify this back to a plain `ProjectReference`" warning in Consequences describe the
  now-superseded design; this ADR is the one to follow instead. ADR-0005's other decisions (versioning,
  packaging, publishing, docs site, wiki) are unaffected.
- One more project to keep in step with `AssemblyReferenceGateTests` (never references `Avalonia*` or,
  alongside Generator, `Microsoft.Build*`).
