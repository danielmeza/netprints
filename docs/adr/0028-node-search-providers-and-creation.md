# 0028: Node search providers, node creation and ranking

## Status

Accepted (2026-10-16, P3 batch B). The reserved number 0028 ("node search") is used here. The public move of the
provider contract (SUG4) lands in C1 and gets its own amendment.

## Context

- The node search built its rows in one large switch, created nodes with `Activator.CreateInstance` and
  `GetConstructor` behind an `AddNodeRequest`, and ordered its categories by the order in which the switch happened to
  yield them. Extensions can offer nodes (P3), so the host needs one place for each of those jobs.
- Each of the reflection calls was an IL2xxx trim warning in the ratchet (`eng/trim-warnings.txt`).
- P3a's characterization goldens pin the rows and their order; FR-017 keeps them, with one recorded exception.

## Decision

- **Providers grouped by node kind.** An `INodeSearchProvider` yields `NodeSearchEntry` records (method, variable, type,
  node kind, make delegate, custom event, override) and names the node kind that owns it. The 13 internal providers
  run in a fixed order. Opening the search creates no node.
- **Providers are built once per extension registry**, never per open (`NodeSearchProviderRegistry`).
- **One creation service.** `INodeCreationService` turns an entry into a node: it asks the prompts adapter
  (`INodeSearchPrompts`, over the editor dialogs) for a type or method where the entry needs one, and every node is
  built by a `Func<NodeGraph, Node>` factory through `NodeGraphViewModel.AddNode(position, pin, factory)`. That call
  clamps a negative position to the canvas, connects the node to the pin the search was opened for, and records one undo
  step. `AddNodeRequest` and the generic `AddNode<T>` are removed; no node is built by reflection any more.
- **One ranker.** `ISuggestionRanker` orders the entries before they are grouped into rows. The default
  ranker uses a fixed category order (NetPrints, Pin Variables, Pin Methods, This Methods, This Variables, Static
  Methods, Static Variables, Method Variables, Types, Pin Static Methods, Generic Types, Generic Static Methods), then
  the categories of extensions in order of first appearance; entries keep their order inside a category.
- **Recorded order change.** This Methods now come before This Variables. The P3a goldens do not contain a This
  Variables category (the fixture class has no instance variables), so no golden changes; a ranker test pins the order.
- **Trimming.** The ratchet falls from 14 to 12: the `Activator` and `GetConstructor` warnings are gone.
- **SUG4** (C1): the provider and ranker contracts move to the Abstractions assembly under NPXE0006, with
  `AddSuggestionProvider`, an NPX code for id-prefix and duplicate problems, and `Requires` on entries.

## Consequences

- Adding a node kind adds a provider and a factory; nothing edits a switch.
- A test creates nodes through a fake prompts adapter; a test helper keeps the old `AddNode<T>` call shape with
  `Activator`, outside the product.
- The ranker is host-only until SUG4: extensions cannot reorder the built-in categories.
