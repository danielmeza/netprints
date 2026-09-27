# 0002: Designer-authored comments become a P3b feature, not a P1 addition

## Status

Accepted (2026-09-27). Scope widened the same day (below) after the owner asked where else a
comment is useful, before any implementation started.

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

Schedule designer comments as a new roadmap item under P3b, not as a P1 addition. The fields, where
each shows and what it emits:

| Field | On | Shown | Emitted as |
|---|---|---|---|
| `Comment` | Node | canvas note (builds on P6 comment boxes/regions), node hover tooltip (reuses the T063 built-in-doc tooltip slot) | `//` line above the node's generated statement |
| `Comment` | Connection (wire) | connection tooltip (builds on P6's planned live-thumbnail tooltip) | optional: `//` appended to the consuming statement, once sub-phase I's `SourceMap`/`NodeOffsets` make that mapping available; not required for the first cut |
| `Description` | Parameter/return pin | pin tooltip, inspector | `<param name="...">`/`<returns>` on the owning method's XML doc |
| `Summary` + `Remarks` | Method/constructor/event graph | node search results (extends the existing built-in-doc-preview item), method tooltip | `/// <summary>` / `/// <remarks>` on the generated method |
| `Summary` + `Remarks` | Class | class inspector | `/// <summary>` / `/// <remarks>` on the generated class |
| `Summary` | Variable/field | variable list, tooltip | `/// <summary>` on the generated field |
| `Comment` | Local variable (sub-phase H, P5) | inspector | `//` inline above the generated local declaration (locals get no XML doc in C#) |

All of these are plain text in the model; none introduces a new node kind. Whether XML docs are
required/optional and their exact shape follows P3b's `.editorconfig`-driven style, not a separate
switch. Search-preview and tooltip reuse existing P6/T063 UI slots rather than adding new ones.

P6's "comment boxes/regions" and "connection tooltip" bullets now point at this item for the
underlying field and its emission; P6 keeps only the visual grouping/region and thumbnail/live-value
behavior.

## Consequences

- P1 ships without this feature; nothing in P1 needs to special-case it.
- When P3b is implemented, the declaration-kind and emission-style registries it introduces are the
  natural place to add this emission, instead of hard-coding it earlier and reworking it later.
- The schema gains several optional string fields at that point (on nodes, connections, pins,
  graphs, classes and variables); since the schema stays v1 until the version cut (per the P3b
  plan), this needs no migration.
- The wire-comment-to-code-comment mapping depends on sub-phase I's source map; it may ship one
  release after the rest of this item if that turns out to be non-trivial.
