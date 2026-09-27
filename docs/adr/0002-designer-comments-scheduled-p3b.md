# 0002: Designer-authored comments become a P3b feature, not a P1 addition

## Status

Accepted (2026-09-27).

## Context

The owner asked whether NetPrints plans to let a designer write comments (per node, per
method/constructor/event graph, per class) that reach the developer reading the generated C#, for
example as `//` lines and `/// <summary>` XML docs. Today the model has no comment or description
field anywhere (`Node`, `MethodGraph`, `ClassGraph`, `Variable`); the only text that reaches a
tooltip is the XML doc of the wrapped .NET API on a built-in node (T063, SC-003), which is not
designer-authored. The roadmap already has an adjacent, purely visual item under P6 ("comment
boxes/regions") and a phase, P3b, that owns how NetPrints decides what C# a graph emits
(declaration kinds, emission styles, `.editorconfig`-driven style).

P1 (`003-core-refactor`) is at checkpoint F of sub-phase L, past the point where new model fields
should be introduced: it would touch the schema, the canonical writer, golden fixtures, the
translator's emitters and the editor, none of which P1's remaining sub-phases (G–L) need.

## Decision

Schedule designer comments as a new roadmap item under P3b, not as a P1 addition:
- a `Comment` field on a node (canvas note, `//` line above its generated statement);
- a `Summary` field on a method/constructor/event graph and on the class (`/// <summary>`);
- plain text in the model, no new node kind;
- whether XML docs are required/optional and their exact shape follows P3b's
  `.editorconfig`-driven style, not a separate switch.

P6's "comment boxes/regions" bullet now points at this item for the field and its emission; P6
keeps only the visual grouping/region behavior.

## Consequences

- P1 ships without this feature; nothing in P1 needs to special-case it.
- When P3b is implemented, the declaration-kind and emission-style registries it introduces are the
  natural place to add `Comment`/`Summary` emission, instead of hard-coding it earlier and
  reworking it later.
- The schema gains two optional string fields at that point; since the schema stays v1 until the
  version cut (per the P3b plan), this needs no migration.
