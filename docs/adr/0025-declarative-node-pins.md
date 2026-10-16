# 0025: Node pins are declared as typed partial properties, and their keys are tracked

## Status

Accepted (2026-10-16, P3 spec `specs/006-editor-extension-host/`, research R8; lands in sub-phase B). It builds on
[ADR-0024](0024-analyzer-and-generator-infrastructure.md), which provides the generator, the analyzers and the code fixes.

## Context

- A node class created its pins in its constructor with `AddInputExecPin("Exec")` and friends, and exposed each one through
  a property that indexed a collection (`InputExecPins[0]`). The order of the calls was the contract with every saved graph,
  and nothing checked it. A reordered line silently rewired old graphs.
- Data pins were untyped: a `bool` input was a `NodeInputDataPin` whose type lived in a field. Callers cast, and a new
  node author had no help from the compiler.
- Authors outside the repository need the same help the built-in nodes get, so the mechanism must ship with `NetPrints.Core`.

## Decision

1. **Typed pins.** `NodeInputDataPin<T>` and `NodeOutputDataPin<T>` carry the pin's type. The initial unconnected value is
   `default(T)`; there is no generic-math constraint on pins. Pins whose type is data (a generic result, a default value)
   take `[PinType(nameof(member))]`, which points at an observable value on the node.
2. **Convention over constructor calls.** A class marked `[NodeKind("id")]` (a concrete kind) or `[NodeBase]` (an abstract
   base) declares each pin as a `partial`, get-only property whose type says the pin kind and direction. The generator
   implements the properties and a `DeclarePins()` method that the constructor calls once. The pin key is the property
   name without the `Pin` suffix; `[Pin("key")]` overrides it. A derived class continues after the pins its bases declare,
   using `[NodePinLayout]` metadata the generator emits, so a base in another assembly keeps working.
3. **Rules as errors.** NPA001-NPA009 (ADR-0024's authoring family) reject a pin property that is not partial, a duplicate
   or invalid key, a base class that adds pins imperatively, a `[PinType]` member that is missing or wrongly typed, and pin
   properties outside a marked class. Code fixes make the class partial, add the `[Pin]` that keeps the current key, and
   add the missing marker.
4. **Pin keys are tracked like public API.** Each project that declares pins has `NodePins.Shipped.txt` and
   `NodePins.Unshipped.txt`, seeded from the pin-layout golden. NPA010 (a warning) says a declared key is not recorded, with
   a fix that adds it to the unshipped file. NPA011 (an error) says a shipped key is no longer declared and names both the
   old and the new key, with two fixes: keep the key (`[Pin("old")]` on the replacing property) or remove it on purpose
   (`*REMOVED*<line>` in the unshipped file). The release step moves the unshipped lines to the shipped file. A key that
   changed would break stored graphs, so the build fails before it can. A project that declares pins but has neither file is
   not tracked; NPA012 (an info) says so.
5. **Not every kind converts.** Kinds whose pins depend on constructor arguments or are fully dynamic (`Await`,
   `ExplicitCast`, `Ternary`, `CallMethod`, `Constructor`, `Literal`, `MakeArray`, `MakeDelegate`, `Type`, `VariableGetter`,
   `VariableSetter`, `Reroute`) stay imperative. A test lists them, so a kind moves only by an explicit change.

### The 0.4.0 rename policy

The convention names a property after its key, so eight built-in properties were renamed in 0.4.0:
`ExecutionPin`, `InitialExecutionPin` and `ReturnPin` became `ExecPin`, `DefaultValuePin` became `DefaultPin` and
`SuperTypePin` became `BaseTypePin`. `TypeOfNode.InputTypePin` keeps `[Pin("Type")]` because `TypePin` is already its output
pin. The break is hard: `*REMOVED*` lines in `PublicAPI.Unshipped.txt`, a breaking entry in the release notes and no
`[Obsolete]` shims, because 0.4.0 is the first release of the extension API and the old names were not part of a published
contract. Stored keys, every fixture and every sample graph are unchanged; the pin-layout golden changes only in its
property-name column.

## Consequences

- A node author writes the pin list once, as properties, and the compiler checks types, order and keys.
- Moving or renaming a pin property can no longer change the file contract without a visible edit to a tracked file.
- Core builds with the analyzers it ships, so the built-in nodes are the first test of every rule.
- Two small files per project to keep in step with releases, and a generator that must stay in the build of every project
  that declares pins.
- Dynamic kinds are not covered by the rules until their pins can be declared (a later batch).
