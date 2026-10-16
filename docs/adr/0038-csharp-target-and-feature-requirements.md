# 0038: The C# target and feature requirements

## Status

Accepted (owner, 2026-10-14, after a research on user attributes and the language version and its adversarial audit; the
pre-gate parts land at the start of P3, the rest in P3b). It takes the first free number after 0037. Where the audit and the
research differ, the audit wins. Where the owner delegated a choice the choice is recorded as a coordinator default.

## Context

- NetPrints writes C# that must compile for the user's project, but nothing models which language version, framework or
  runtime features that project has. `Project.TargetFramework` exists; `LangVersion` is not read.
- A version alone is not enough. Some features also need a type (`init` needs `IsExternalInit`, which Unity lacks;
  `required` needs three attribute types) or a runtime feature (default interface members, static abstract members,
  covariant returns). The compiler synthesizes some types when they are missing (`RequiresLocationAttribute`,
  `ParamCollectionAttribute`, `ExtensionMarkerAttribute`), so those features need none.
- **Emission is not deterministic if it depends on the machine.** Three writers produce `.g.cs`: the editor, CLI
  `generate` and `regen`, and the SDK `NetPrintsGenerate` target. The last two go through `GraphCodeGenerator` with a
  throwaway snapshot. Capping `latest` at MSBuild's `_MaxAvailableLangVersion` (the installed compiler's maximum) would make
  the committed file change with the SDK.
- A multi-targeted project compiles one `.g.cs` for every framework, so the first target can break the others.
- Node translators (`INodeTranslator`, built-in and contributed) are the largest emitter surface, and literal formatting
  (ADR-0034's `PrimitiveKinds`) depends on the version too. Both sit in `NetPrints.Core`, already on NuGet.
- ADR-0017's AP-T02 requires every public member whose signature names an `[Experimental]` type to carry the same id, so an
  experimental `CSharpTarget` would spread through `NodeSearchContext`, `ICommandContext` and others.
- Documents drop unknown members on save today. An older editor that saves a file would delete any new member.
- Probing the project's compilation to find provided types would run its analyzers and generators in the editor, and
  generators built against a newer Roslyn would not load. The owner deferred "generators in editor" (Godot D1.6).

## Decision

1. **Generated code depends on the language version only.** It is computed from the evaluated `LangVersion`,
   `MaxSupportedLangVersion` and `TargetFramework(s)`, and passed in the generate request (the SDK target writes them as
   request lines), so the editor, `regen` and `NetPrintsGenerate` write identical bytes. Parsing is ours (`major.minor`,
   `latest`, `latestMajor`, `default`, `preview`), not Roslyn's `LanguageVersionFacts`, which loses versions newer than the
   editor's Roslyn.
2. **`latest`, `preview` and `default`** resolve to the smaller of the TFM's maximum and the editor Roslyn's maximum, with
   an info hint to pin `<LangVersion>`. Never cap at `_MaxAvailableLangVersion`.
3. **Multi-targeting** uses the minimum version and the intersection of capabilities across the TFMs.
4. **Types and runtime features only gate availability and diagnostics.** A missing type is a warning ("may be provided by
   a source generator"; the real build confirms it). The editor runs no probe that executes generators; revisit with Godot
   D1.6.
5. **A version newer than the TFM's default** gives an info message and is allowed.
6. **Filtering.** `Availability()` returns Available or Unavailable(reason, fixes), and each surface decides:
   - short lists show unavailable items disabled, with the reason and quick fixes;
   - node search hides them, says "N results hidden by the project's C# version" and offers a "Show unavailable" toggle;
   - the attribute picker hides `ValidOn` mismatches.
7. **Lowering after use.** An item whose descriptor declares a fallback lowers to it with an info diagnostic; one without a
   fallback is an error. The choice depends only on the language version, and stored data never changes.
8. **Quick fixes.** Edit `<LangVersion>` up to `MaxSupportedLangVersion`, even when it comes from an imported props file
   (the project body wins, and the fix says so); otherwise hint to change the TFM. A PolySharp hint covers missing types.
9. **Contracts.**
   - `CSharpLanguageVersion` and a minimal `CSharpTarget` are stable from the start (language version, the specified value,
     TFMs, `Check`), so they pass AP-T02. `CSharpTarget.Unconstrained` serves graphs with no project.
   - `CSharpFeature` and `Requires` (on node-search entries, inside SUG4) are experimental under a new NPXE id. A companion
     id enum serves attributes, as `CommandKey` and `CommandKeyId` do (ADR-0034 decision 2).
   - The target hangs on `ProjectSnapshot` as a non-positional `init` member and is reached through `Project` and
     `IProjectSession`, derived again on every snapshot replacement.
   - `IExecutionTranslationContext` gets a default-implemented `Target`, so implementers do not break; literal formatting
     follows the version.
   - Hosts without MSBuild evaluation (Unity) get an `ICSharpTargetProvider`.
10. **Unknown document members are preserved on save** (`[JsonExtensionData]` on every document record, in canonical order,
    opaque to `format`, `show` and the merge driver), before any new document field ships. The editor's open and save keep
    them on every record that has a model object; the reference records (type, method, variable, value) keep them in
    `format`, `show` and `merge` only, since the editor writes references from the model's own types.
11. **Diagnostics.** NPT010: feature unavailable (error). NPT011: fallback used (info).
12. **Styles declare their requirements.** A style names the features it needs and an optional fallback style; the order is
    `.editorconfig`, then profile, then per-class override, then target gating, and an unavailable preferred style falls back
    with NPT011.

## Alternatives rejected

- **A capability probe through `GetCompilationAsync` as the default.** It executes analyzers and generators on every load,
  is unavailable to the SDK path, and breaks on generators built for a newer Roslyn.
- **Capping at `_MaxAvailableLangVersion`, or using the first TFM.** The first makes output follow the SDK; the second can
  break the other frameworks.
- **An experimental `CSharpTarget`.** AP-T02 would mark every member that names it.
- **A `FeatureKind` (stylistic or semantic) next to the descriptor's fallback.** The two can disagree; the fallback alone
  decides.
- **A mirror of Roslyn's feature table or a deny list of reserved attributes.** The table keeps only what NetPrints emits or
  gates, each row pinned by a compile snippet; live analysis reports the rest.

## Consequences

- Pre-gate: about 3-5 units (language version, minimal target, `Requires`, unknown-member preservation, fixtures). The P3b
  slice (probes, the CLI target line, NPT010 and NPT011, the style matrix): about 8-10 units.
- Tests: workspace fixtures (no `LangVersion`, `latest`, `preview`, `10.0`, an unknown `16.0`, multi-target) and a byte
  identity test across the three writers. `generate --check` prints the target and reports a target mismatch as its own
  reason.
- Catalogs stay byte-identical (ADR-0012, ADR-0013): gating happens when the editor queries, never inside stored catalogs.
- The live analysis compile should parse at the emitted version. It lacks generator output, so a project that gets
  `IsExternalInit` from PolySharp can show a false CS0518 today.
- Surfaces to cover: paste and duplicate, class and project templates, the "Add C# partial file" command, type pickers (a
  `ref struct` generic argument, `nint`, pointers) and the override list.
- Source-generated tables (S7) declare node requirements; `netprints check` for the target is low priority.
- Open: running generators in the editor (D1.6); the exact shape of `ICSharpTargetProvider` (the audit advised deferring it
  until NetPrintsUnity is scheduled).
