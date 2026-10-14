# 0036: Member defaults and editor metadata

## Status

Accepted (owner, 2026-10-13, after a research on object properties and the details panel and its adversarial audit;
lands in P3 and P3b, with Unreal parts in U1 and U2). It takes the first free number after 0035. Where the owner
delegated a choice ("lo que genere más valor") the choice is recorded as a coordinator default.

## Context

- The variable inspector edits a variable's name, type, visibility and modifiers. A `Variable` has no default value and no
  editor metadata, and `VARIABLE_TEMPLATE` emits no initializer.
- **Const is broken today.** A Const variable emits `const` with no value, which does not compile: CS0145 with no value,
  CS0504 with Static, CS0106 with ReadOnly, CS0133 for a non-constant default, and syntax errors with accessors or on the
  Unreal partial path.
- Unreal splits the work in two: Class Defaults edit design-time data in the Blueprint, and the Details panel edits
  instances. NetPrints edits a class, a model of code that may not compile yet, so it edits design-time data only.
- `VariableDocument` is a positional record shipped in the Serialization package at the P3 publish gate, and unknown
  properties are dropped on save today (there is no `JsonExtensionData` in `src`).
- Unreal evidence (UnrealSharp fork, checked by the audit): the CDO is created deferred and the managed parameterless
  constructor runs on it (`CSManagedClassCompiler.cpp:119-120, 258-272`), so constructor assignments become CDO values;
  instances run their constructor first and Unreal copies the archetype's values over them, so per-instance edits win
  (`CSClass.cpp:7-45`).
- The research report said a "schema cut" was needed. The audit corrected it: the schema stays v1 and may change without
  migrations until the version cut (roadmap, P2 notes), as P3b's new `kind` field already does.

## Decision

1. **Const is disabled in P3a** (done in G-F3b, diagnostic NPT009) until defaults exist. The checkbox is disabled with
   its reason shown, a `.netpc` with Const set still loads, and the build reports it instead of failing with CS0145.
   The defects above are the cases the P3b re-enable must cover.
2. **Two layers.** NetPrints edits (a) **variable default values** and (b) **editor metadata**: category, tooltip (the
   P3b `Summary`), display name, range and edit scope (defaults, instances, both, visible, none), plus an advanced flag
   and a raw pass-through list. It never edits live objects.
3. **Placement.**
   - **P3:** only R6's `PinEditorDescriptor` is widened into a **value-editor contract** (a descriptor that accepts a
     `TypeSpecifier`, with priority, view-model factory and template key, shared by pins, variable defaults, parameter
     defaults and grid rows), at no extra cost while R6 is experimental. At most an ADR (this one) lands before the
     publish gate.
   - **P3b:** (a) and (b), with the emission styles, Summary-as-tooltip and parameter defaults it already owns. Const is
     re-enabled with defaults.
   - **(c)** a grid driven by the model with editors contributed by plugins, at the P3 tail or in P3b (open: it lands
     in P3 if it fits).
   - **(d)** a read-only live inspection grid over the P6 watches, in P6.
4. **The grid is driven by the model.** No reflection over user types. An `ItemsControl` with DataTemplates and bound
   commands, registered through our own attributes and the registration generator (ADR-0031), no runtime scanning. The
   third-party PropertyGrid (bodong) is rejected on trimming, theming and conformance grounds, not on "no live
   instance": it works through `TypeDescriptor`, says nothing on trimming, and its colours are not our tokens.
5. **Unreal values.**
   - **P3b:** simple types (numbers, bool, enums, strings), assigned in the constructor.
   - **U2, through the Unreal extension:** FVector, FRotator and FLinearColor, with a composite `TypedValue` and an
     extension-contributed editor.
   - **Unreal itself:** assets, components and other UObject defaults stay in Unreal's Details panel or a Blueprint
     child, where the asset picker, thumbnails and component tree already live (the owner's rule: do not maintain
     upstream surfaces).
   - A U1 smoke test replaces the "spike": hot reload, propagation to Blueprint children and inherited properties.
6. **Unreal-only flags** (Instance Editable, Expose on Spawn, Replicated, SaveGame) live in an Unreal extension section,
   stored per member in an extension data bag. The core model holds only neutral metadata.
7. **Plain .NET** emits the metadata attributes only when the profile opts in, written as
   `global::System.ComponentModel.*` (and `DataAnnotations.Range`), one per line, as attributes are emitted today.
8. **Unreal attribute names** follow the audit: `UProperty` is in `UnrealSharp.Attributes`; the MetaTags and `UMetaData`
   are in `UnrealSharp.Core.Attributes`; `[UiMin]` and `[UiMax]` exist; `ClampMin`, `ClampMax` and `ExposeOnSpawn` take
   strings. There is no curated list of Unreal metadata keys: a small neutral core set, plus raw `UMetaData` entries
   passed through and validated at runtime by the extension.
9. **Schema v1 stays additive.** New members (`default`, `editor`, the per-member `extensions` bag) are non-positional
   `init` members of the existing records, so the positional-record break is avoided and 0.x already allows hard breaks.
   The extension bag is a new preservation mechanism (data from a missing extension survives a save).
10. **Prerequisite: a "set member property" undo command.** Visibility and Modifiers edits are not undoable today (the
    view model sets the model directly), and typing must coalesce into one entry. Class name and namespace edits already
    use `SetMemberProperty` (G-F4c).
11. **Theming of editors is by tokens only.** Editors return view models; their views are DataTemplates in a host that
    sets style classes. `Inspector.LabelColumnWidth` (a `StaticResource` today) and the `inspectorLabel` and
    `propertyGrid` styles move to `DynamicResource` tokens in the [ADR-0035](0035-editor-themes-are-data.md) catalogue,
    with new tokens for row spacing, category headers, the modified marker and value editors. Contributed XAML may use
    `DynamicResource` tokens only, and the conformance kit scans it (today `XamlHygieneTests` scans repo sources only).

## Alternatives rejected

- **Editing live objects in NetPrints.** Unreal's Details panel does it best; (d) only inspects.
- **A schema cut or v2 before the publish gate**, and "shape first" as a required step. Optional at most: the `init`
  route costs about 1 unit, the ADR.
- **bodong PropertyGrid** (see decision 4).
- **Rebuilding asset, component and UObject pickers.** Scope creep over Unreal's own UI.

## Consequences

- Estimates in half-days (audit): (a) 10-11 with the constructor-assignment style and the undo work; (b) 8-9, plus 3 in
  U1; (c) 13-15, plus 3 for the Unreal struct editors in U2; (d) about 2 on top of P6; the shape slice 1 (this ADR).
- P3b needs the same editors for parameter defaults and the inspector sections of declaration kinds.
- Dependencies: R7 and N1 for the contracts, R2 for inspector lookup by role, the `PrimitiveKinds` table for the numeric
  editor, L1 for label keys, the ADR-0035 token catalogue and P3b emission styles.
- Risks to cover with tests:
  - **Pin-key clashes.** S2b derives a pin key from the property name minus `Pin`, so a node setting `Value` and a pin
    `ValuePin` share a key; Expose on Spawn pins named after members can collide with a spawn node's pins and get
    order-dependent `~2` keys.
  - **Collections, structs and enums.** `TypedValue` has no form for arrays, maps, structs or asset references;
    `ParseEnum` uses `Type.GetType`, so an enum known only to the catalog (Unreal) fails to load; changing a variable's
    type leaves a stale default.
  - **Localisation.** The descriptor `Title` is not a label key; numeric input under a decimal-comma culture is stored
    invariant.
  - **Accessibility.** The variable inspector has no `LabeledBy` (the class inspector has); a generated grid fixes it
    centrally.
  - **Trimming.** `Type.GetType` is already an IL2057; more enum defaults lean on it until `PrimitiveKinds` lands. The
    trim ratchet baseline is 14.
  - **Snapshots.** Keep one grid tile per preset and test editors headless.
  - **Controls.** ColorPicker needs `Avalonia.Controls.ColorPicker` and `Semi.Avalonia.ColorPicker`, neither referenced
    today.
- Open: whether (c) lands in P3 or P3b; the exact shape of the per-member extension bag.
