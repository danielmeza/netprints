# 0029: Node capabilities are role interfaces, and pins carry roles that decide purity

## Status

Accepted (2026-10-16, P3 spec `specs/006-editor-extension-host/`, research R9 and R11; lands in sub-phase B). It builds on
[ADR-0025](0025-declarative-node-pins.md), whose generator sets the roles.

## Context

- The editor decided what a node could do by testing its class: the +/- pin buttons for `MakeArrayNode`, `MethodEntryNode`,
  `ReturnNode` and `ClassReturnNode`, the overload list for `CallMethodNode`, `ConstructorNode` and `MakeArrayNode`, editable
  pin names for entry and return nodes, and the inspector target for call and variable nodes. An extension node could not take
  part in any of these.
- Whether a node could be made pure was a per-class override of `CanSetPure`. Each override restated the same idea (the node
  produces a value somebody can need), and a call to a `void` method with an `out` parameter was refused although its
  output is exactly such a value.
- An `out` or `ref` parameter was only an input pin. Its value after the call could not be wired to anything.

## Decision

1. **Role interfaces.** `IPinListEditor` (the +/- buttons and the edit they run), `IOverloadableNode` (the current overload
   and the switch to another), `IPinNameEditor` (which pins the user renames) and the existing `IMemberReferencingNode`
   (which member the node names) are the capabilities. The editor asks for a role, never for a class. The built-in nodes
   implement the interfaces explicitly, so their own surface does not grow. The list of overloads still comes from the
   reflection provider in the editor; the header role keeps falling back to the node's visual kind by convention.
2. **Pin roles.** `PinRole` (`Argument`, `Target`, `Result`, `OutParam`, `Exception`) is a property of every pin. The
   generator sets it from `[Pin(Role = ...)]` on a data pin where the convention cannot tell; `CallMethodNode` sets it for its
   dynamic pins: `Target`, `Result` for each return value, `OutParam` for each `out` or `ref` parameter (input and output),
   `Exception` for the pin that exists while the catch pin is connected.
3. **One purity rule.** `Node.CanSetPure` is true exactly when an output data pin has the `Result` or `OutParam` role. The
   `Exception` pin never counts. The per-class overrides of calls, constructors, ternaries and casts are gone; those kinds
   mark their value pin `Result`. `AwaitNode` keeps its override because its result pin comes and goes with the task type.
4. **`out` and `ref` output pins.** Every `out` and `ref` parameter of a call also gets an output data pin, after the return
   values, keyed `out.data.<parameterName>` and carrying the parameter's value after the call. The translator assigns it
   from the variable passed to the parameter. An `out` or `ref` input with no variable connected stays a translation error
   (`NPT008`), now decided from the pin role. Constructors keep their input-only check.
5. **The file format stays version 1.** The new pins are derived from the method specifier when a graph loads, so no stored
   field is added and no stored pin key changes. A graph that wired nothing to the new pins saves byte-identical. The graphs
   whose generated C# changes are those that call a method with an `out` or `ref` parameter: the call gains one assignment
   after it.

## Consequences

- An extension node gets the +/- buttons, the overload button, editable names and the inspector link by implementing a
  role, with no change to the editor.
- A void method with an `out` parameter can be made pure, and its output can be wired onward.
- A pure call with `out` parameters runs when a consumer needs one of its outputs, like any other pure node.
- `CallMethodNode.ReturnValuePins` now means the `Result` pins only; callers that wanted every output read
  `OutputDataPins`.
- The role of a pin is not stored in a graph; a node kind must set it the same way every time.
