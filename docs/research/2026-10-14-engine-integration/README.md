# Engine integration: what is known about Godot and Unity

Date: 2026-10-14. Scope: what NetPrints already knows about integrating with the Godot (C#) and Unity engines, kept in the repo so
that it is not lost when a Godot or Unity spec is written later. The owner asked for this on 2026-10-14. This is a distillation of
earlier research, experiments and decisions; nothing new was researched. It records what was true on that date and is not updated.

- [Godot](godot.md): script discovery, running, restore and samples, repo hygiene, headless testing, open questions.
- [Unity](unity.md): the NetPrintsUnity roadmap candidate, the C# 9 limits, build and run, open questions.

Evidence tags in both notes: **[V]** verified (a URL, a repo `file:line`, or an experiment that was run) and **[I]** inferred. The
tag and source were carried over from the research the notes come from. A Godot experiment is `E<n>` (E1 to E20) in that research.

## How to use these notes when writing a spec

1. Start from the engine-independent decisions below. A Godot or Unity spec adds data, a template, a sample or an extension; it does
   not add engine names, variables or paths to the core or the workspace (an owner rule from the Godot showcase).
2. Read the engine note's "What is known" for facts, then "How NetPrints would integrate" for the proposed shape. Treat **[I]** items
   as hypotheses to confirm with a small experiment before relying on them.
3. Take the engine note's open questions as the spec's clarification list.
4. Re-check anything dated. Godot facts are for 4.7.2-stable (with a few 4.3 comparisons), and repo line numbers are for
   `005-editor-shell` around `e05277a1` and will have moved. The ADRs are the current decision record; where a note and an ADR
   differ, the ADR wins (see the last section).
5. Pin today's behaviour with tests before changing it.

## Engine-independent decisions

These apply to every engine plugin.

| Decision | Where | What it means for an engine |
|---|---|---|
| Run profile kinds | [ADR-0037](../../adr/0037-run-profile-kinds.md) | NetPrints reads `Properties/launchSettings.json` and supports `Project` and `Executable`; Run is gated by the selected profile, not by `OutputType`. `%NAME%` and `$(Name)` are expanded, and a bare name is looked up on `PATH`. A macOS `.app` resolves to `Contents/MacOS/<binary>`. Godot needs no core code: its profile is an `Executable` one that starts `%GODOT4%`, shipped as template data. A public `AddRunProfileKind` is added only when U3 or NetPrintsUnity needs it. |
| C# target | [ADR-0038](../../adr/0038-csharp-target-and-feature-requirements.md) | Generated code depends on the evaluated language version, and a `CSharpTarget` carries version, TFMs, runtime features and available types. Hosts without MSBuild evaluation (Unity) provide it through an `ICSharpTargetProvider`. Unavailable items are shown disabled with the reason, or lowered to a fallback. |
| User attributes and the plugin extension model | [ADR-0039](../../adr/0039-user-attributes-and-editor-extension-model.md) | The core stores native attributes on every declaration slot. Each plugin (Godot, Unreal, Unity) registers its attribute types with four contributions: attribute descriptors (per-argument editors such as asset or resource pickers, validation, C# requirements, a row or a section), inspector sections over the model's attributes, type editors shared with ADR-0036's value-editor contract, and behaviour through nodes, graph rules and commands. Nothing is lost without the plugin, which falls back to the generic editor. |
| Samples from remote sources | roadmap section "Samples from remote sources (owner decisions 2026-10-14)" in `.specify/memory/roadmap.md` | Samples are not bundled with the editor. Extensions can ship samples under `samples/<id>/`; they are listed while the extension is enabled and need no download, and only extensions installed per user can list samples. The sample format is a `.nupkg` of package type `NetPrintsSample` with `netprints-sample.json`. |

Related decisions the engine notes lean on: [ADR-0022](../../adr/0022-sdk-version-written-by-pre-release-builds.md) (the SDK version a
sample copy gets) and [ADR-0036](../../adr/0036-member-defaults-and-editor-metadata.md) (member defaults and editor metadata, amended
by ADR-0039 decisions 6 and 8).

## Where the sources differ from the ADRs and the roadmap

- **`--upwards` for Godot.** The run-profile audit listed it as a working portable flag (verified against the 4.7 docs); a later
  experiment found it removed in 4.7.2 (E17). The Godot note follows the experiment: `--path .` with no `workingDirectory`. ADR-0037
  does not mention `--upwards`.
- **Absent `workingDirectory`.** ADR-0037 decision 6 follows Visual Studio for relative paths and makes an absent one the project
  folder. The run-profile audit says Visual Studio's default for an absent value is the output folder and `dotnet run`'s is the
  project folder. The ADR's absent-value default is the `dotnet run` one.
- **MSBuild variables reaching Godot.** The Godot research proposed that child processes must not inherit the variables
  MSBuildLocator sets (`MSBUILD_EXE_PATH`, `MSBuildSDKsPath`, `MSBuildExtensionsPath`). ADR-0037 decision 8 scopes the profile
  environment but does not state this scrub. The leak is inferred, not verified.
- **Debug versus Release.** The editor binary loads `.godot/mono/temp/bin/Debug`; running after a Release build would start a stale
  assembly [I]. ADR-0037 does not address a selected configuration.
- **Run-profile slice timing.** The Godot research recommended pulling the run-profile slice forward for the showcase. ADR-0037 says it
  lands in the P3 run-profiles batch, and the owner deferred NetPrints-side changes during the showcase.
- **Attributes.** The Godot research recommended an extension route on ADR-0036's per-member bag (or a neutral `attributes` list as an
  owner call). ADR-0039 decided native core attributes and amended ADR-0036 decisions 6 and 8.
- **`sample.json`.** The Godot research's optional `sample.json` is superseded in shape by the roadmap's `netprints-sample.json`.
- **`ICSharpTargetProvider`.** The attributes research advised deferring it; ADR-0038 lists it as decided and leaves its shape open.
- No contradiction was found between the sources and the roadmap's NetPrintsUnity entry, which ADR-0038 already amended.
