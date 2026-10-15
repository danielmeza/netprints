# 0039: User attributes and their editor extension model

## Status

Accepted (owner, 2026-10-14, after a research on user attributes and its adversarial audit, and the owner's call on
plugin-owned attributes; the contracts land in P3b with A1-A3 over R6, the implementations in each plugin's phase). It
takes the first free number after 0038. Where the audit and the research differ, the audit wins. It amends
[ADR-0036](0036-member-defaults-and-editor-metadata.md) decisions 6 and 8.

## Context

- Emitters write attributes as strings (`IList<string> Attributes` on the member and class emit contexts, NPXE0003), and the
  model has no typed attribute use. Users cannot add `[Obsolete]`, `[JsonPropertyName]` or an engine attribute to a variable,
  method or class.
- Engine plugins need attributes as their main surface. UnrealSharp (`[UProperty]`, `[UFunction]`, meta tags), Godot
  (`[Export]`, `[Signal]`) and Unity (`[SerializeField]`) are all attribute-driven, and each has pickers and sections of its
  own that no generic editor can know.
- ADR-0036 decision 6 stores Unreal-only flags in a per-member extension data bag, which conflicts with attributes living in
  the model: two stores for the same fact.
- Roslyn pairs attribute mistakes with specific codes, and the audit corrected the research: CS0404 is retired in Roslyn,
  CS0657 and CS0658 are warnings (the whole attribute block is silently dropped and the build passes), and CS1614 is
  avoided only by writing `global::` names.
- A raw C# argument such as `x)] public static void Evil() {} [Obsolete(` closes the attribute list and injects members that
  the graph, the inspector and `netprints show` never show, breaking "the generated code is the visible model".
- Attribute rows have no node id, so navigation through the `SourceMap` needs a new entry kind. One event graph holds several
  entries, each emitted as its own method, so event attributes belong on the entry.
- The owner's rule: do not maintain upstream surfaces (no curated attribute lists).

## Decision

1. **The core supports native attributes on every declaration slot** (class, method, event method on its entry node,
   constructor, field, property with `field:`, parameter in the per-parameter record, and `return:`), as schema v1 `init`
   members. Delegates, enum members, record and primary-constructor parameters, events and type parameters gain slots with
   their kinds; type parameters are keyed by index or id, not by name.
2. **A generic editor** covers constants (through `PrimitiveKinds`, with no `decimal`), enums, `typeof`, arrays, `null`,
   named arguments and `params`, and `nameof` and constant references stored by member id so they survive renames.
3. **`Raw` C# arguments are deferred to v1.1**, and then allowed only behind a parse gate: the text must parse as one
   expression with no newline, consuming everything, with no errors, at the emitted language version, and is shown verbatim
   and flagged in `show` and in the inspector.
4. **The picker is driven by `AttributeUsage` and the project's references.** There is no curated list, and `ValidOn`
   mismatches are hidden (ADR-0038 decision 6).
5. **Pre-compile checks are paired with Roslyn codes** (a parity test asserts each):
   - CS0592 for a `ValidOn` mismatch; CS0404 is retired and is not used;
   - CS0579 (duplicate), CS7046, CS8968 and CS0416 (`typeof(T)` over a type parameter), CS8423 (event accessors) and CS1614;
   - CS0657 and CS0658 are only warnings, so NetPrints reports a stale or invalid target specifier as its own error;
   - attributes are emitted as `[global::Ns.NameAttribute(...)]`, one per line, with target specifiers inside each bracket.
6. **Semantic rules** cover `Obsolete`, `Conditional`, caller-info and `Flags`, after v1 (about 3 units, v1.1 if needed).
7. **Declaration-only partial members are their own P3b item** (signature-only `partial` methods from C# 9, properties from
   C# 13, events and constructors from C# 14), because they are a member shape that source generators and U2 build on.
   `field:` is not allowed on a declaration-only partial property.
8. **Feature-owned emitted attributes** are owner-tagged, with the structured API landing with A1 under NPXE0003 (owners are
   recorded by diffing the list around each emitter call until then). The feature wins a single-use conflict: the user row
   shows "overridden" with a "Promote" action. Multi-use attributes are emitted in feature-then-user order.
9. **ADR-0036's raw metadata list is not user attributes.** It is key/value editor metadata, and plain .NET has no
   key/value attribute to emit it into.
10. **Assembly and module attributes** are edited later through project settings (`<AssemblyAttribute>`).
11. **Plugin-owned attributes** (owner, 2026-10-14). Each plugin (Godot, Unreal, Unity) registers its attribute types and
    contributes four things, modelled on Unreal's `IDetailCustomization` and `IPropertyTypeCustomization`:
    1. **Attribute descriptors:** per-argument editors such as asset, resource or collision-channel pickers that store the C#
       constant; validation; C# requirements (ADR-0038); and a presentation as a row or as its own section.
    2. **Inspector sections** on variables, methods and classes, as views over the model's attributes. Nothing is lost
       without the plugin, which then falls back to the generic editor.
    3. **Type editors**, shared with ADR-0036's value-editor contract (R6).
    4. **Behaviour** through the existing contribution points: nodes, graph rules and commands.
12. **The contracts land in P3b** with A1-A3 over R6; the implementations ship in each plugin's phase.
13. **Amendment to ADR-0036 decisions 6 and 8.** Only the Unreal flags (Instance Editable, Expose on Spawn, Replicated, SaveGame)
    become views over `[UProperty]` attributes stored in the model. Raw `UMetaData` stays in the Unreal extension bag.
14. **Prerequisite:** unknown document members are preserved on save (ADR-0038 decision 10) before `attributes` ships; the
    extension bag cannot do it, because it keeps the data of a missing extension, not an unknown core member.

## Alternatives rejected

- **Raw C# arguments in v1**, checked only by the compile. They break the visible-model guarantee, and the typed model already
  covers every argument form the specification allows.
- **A curated attribute list.** It would mean maintaining the upstream surface.
- **Merging ADR-0036's raw list into user attributes.** It has no plain .NET target.
- **Keeping the Unreal flags in the extension bag.** A second store for facts that the attribute already records, which
  diverge when a user edits `[UProperty]` directly.
- **Attributes on `PinStateDocument`.** It mixes declaration metadata into pin UI state shared by every node kind.
- **Plugin-specific inspector code in the core.** Descriptors and sections keep engine knowledge in the plugins.

## Consequences

- Attributes v1 (A1-A4 and the v1 slots): about 16-18 units. Declaration-only partial members: about 4-5. Semantic rules:
  about 3. Source-map navigation for attribute rows needs a new entry kind (about 1 unit).
- U1 maps the P3b metadata to Unreal property flags and meta tags, and builds the flags section as views over `[UProperty]`
  (about 3 units in the Unreal plugin).
- Risks to cover with tests: the parity test per Roslyn code above, round trip of every slot, `field:` on records and
  partial properties, rename of a `nameof` target, and an older editor saving a file that carries `attributes`.
- Open: the exact shape of the descriptor and section contracts, settled with A1-A3 and R6.
