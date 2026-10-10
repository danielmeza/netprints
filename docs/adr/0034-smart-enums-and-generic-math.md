# 0034: Smart enums keep behaviour in each member; generic math builds the primitive table

## Status

Accepted (2026-10-08, P3 planning; lands with P3 S1). It supersedes the earlier idea of generating the whole smart
enum, and the rule "generator when a second smart enum appears" for `CommandKey`. No number was reserved for this
topic, so it takes the first free one after the reserved 0024-0032 and 0033.

## Context

- `CommandGesture` holds its key as a string, with 75 key names and aliases in a table, 24 `DefaultGestures` lists and
  about 10 consumers. Typed keys would remove string parsing from the core of the registry.
- The owner's condition for any smart enum: keep the object-oriented flexibility, so behaviour lives in each member
  (virtual members overridden per concrete class) and not in `switch` expressions. An `enum` plus a C# 14 extension
  block fails that condition, because the behaviour would be a switch or a table; it is rejected.
- A class cannot be an attribute argument, and the attribute-driven plan writes `[DefaultGesture(...)]`, so the keys
  also need an enum for attributes.
- Primitive types are listed in five places (`TypedValueConverter`, `SignatureChange`, `TranslatorUtil` suffixes,
  `NodePinViewModel`, `TypeSpecifier.IsPrimitive`). The one reflection-shaped spot, `TypedValueConverter`, produces an
  IL2057 trim warning.

## Decision

### CommandKey

1. **A hybrid.** `CommandKey` is an abstract class whose members are `static readonly` fields built from private
   nested sealed subclasses, so each member overrides what differs (for example `Glyph` and `IsFunctionKey`) and
   nothing switches on the key:

   ```csharp
   public abstract partial class CommandKey : ISpanParsable<CommandKey>, ISpanFormattable
   {
       public static readonly CommandKey Escape = new EscapeKey();

       public static readonly CommandKey F1 = new FunctionKey(CommandKeyId.F1);

       public static readonly IReadOnlyList<CommandKey> All = [Escape, F1];

       public abstract CommandKeyId Id { get; }

       public virtual string Glyph => Id.ToString();

       public virtual bool IsFunctionKey => false;

       private sealed class EscapeKey : CommandKey
       {
           public override CommandKeyId Id => CommandKeyId.Escape;

           public override string Glyph => "Esc";
       }

       private sealed class FunctionKey(CommandKeyId id) : CommandKey
       {
           public override CommandKeyId Id => id;

           public override bool IsFunctionKey => true;
       }
   }
   ```

   Verified in a scratch build on SDK 10.0.400. `All` is declared before the lookup: the audit reproduced a
   `TypeInitializationException`, with no compiler warning, when a lookup built in a static field initializer read
   `All` declared after it.
2. **A companion `CommandKeyId` enum for attributes only**, with `implicit operator CommandKey(CommandKeyId)`, so
   `[DefaultGesture(CommandKeyId.Z, CommandModifiers.Control)]` works. The enum is never used for behaviour.
3. **Our `[SmartEnum]` source generator lands in S1**, in the internal analyzer and generator project (not shipped).
   It emits only the `CommandKeyId` enum and `All`, read from the class's fields. That removes the triple listing of
   the 75 keys (field, enum value, `All` entry). The lookup, the aliases and the per-member behaviour are written by
   hand. A test checks the 1:1 mapping (every id maps to exactly one member, names match) and a second checks the
   Avalonia key mapping is complete. The generator has a snapshot test and a test that touches the type's static
   constructor.
4. **`ISpanParsable<CommandKey>` and `ISpanFormattable` are implemented directly**, with public `Parse` and `TryParse`
   on the type, backed by a `FrozenDictionary` with an alternate span lookup (.NET 9). `CommandGesture` implements the
   same two interfaces. Strings are parsed only at the boundaries: keybinding JSON, manifests and display.
   `DefaultGestures` becomes `IReadOnlyList<CommandGesture>?` under the 0.3.0 hard-rename policy. How tolerant a
   future keybindings file must be is a separate decision; the state-files rule only requires the whole-file fallback.
5. **The generic `ISmartEnum<TSelf>` waits for the second smart enum.** An ADR then chooses between a base class with
   `static abstract All` and `ISmartEnum` as prototyped.
6. **Smart enums are never bound through `IConfiguration`.** Settings bind strings and parse them; the configuration
   binding generator rejects such types anyway (SYSLIB1100 and SYSLIB1101).
7. **CS8926.** A static abstract interface member is reached only through a type parameter (CS8926 otherwise). The
   P3b spec carries the note, and a probe test covers static abstract members in node search and in the override
   list, because `ReflectionProvider` does not exclude `IsStatic` there.
8. **Ardalis.SmartEnum is rejected.** Its base class would sit in our public API, upstream has stalled (last release
   2024-11-19, open issues and pull requests unreviewed), it has no aliases, and it finds members by reflection over
   the whole assembly. Under AOT it finds none (IL2026, IL2070), though rooting the assembly works around that; AOT
   is a secondary point because the editor is JIT. Intellenum and Thinktecture were also tried; the first has low
   activity and the second keeps three AOT warnings with no verified alias feature.

### Generic math and the primitive table

9. **One `PrimitiveKinds` table in Core**, landing before or with R6, replaces the five scattered lists, with the
   suffixes in `TranslatorUtil.cs` and the editor's default-value rows.
   - Numeric rows are registered through `Number<T>() where T : INumberBase<T>`, using `T.Zero` and `T.Parse` /
     `ToString` with the invariant culture.
   - `bool`, `char` and `string` quoting, NaN and Infinity literals and enums keep special rows.
   - The table removes one trim-ratchet warning (IL2057 at `TypedValueConverter`).
   - It gets a golden test built from the 18 wire-form samples and the 240-input parse matrix.
   - Switch node (B1) case literals reuse the table.
   - Cost, accepted by the owner: about 36 generic methods JIT-compiled once at first use, roughly 1.4 ms.
10. **`sbyte` and `decimal` become full primitives**: unconnected default values and the numeric editor, like the other
    numeric types. This changes behaviour, so a test pins it.
11. **Typed pins carry no generic-math constraint** (S2a): defaults come from `default(T)`.
12. **Rejected:** `IUtf8Span*` interfaces, `INodeKind<TSelf>`, a `static virtual` suggestion hook (S7's generator stays
    the single source of node-kind metadata), typed ids, `GraphPoint` math, and CLI or DI uses of generic math.

## Consequences

- Behaviour stays object-oriented; adding a key is a field, a nested class and nothing else (the enum and `All` are
  generated).
- Two types per smart enum (class and companion enum) and a generator to maintain, justified for the 75-key type and
  reused by later smart enums.
- Generic code that calls static abstract members on classes is JIT-compiled once at first use, under a millisecond
  per method; there is no hot-loop restriction.
- The vocabulary is closed and has no punctuation or numpad keys; adding members later is not breaking, but it is
  decided while the type is designed (P3 S1).
- Side finding: `GraphPoint.ToString()` formats with the current culture; it is fixed in the next batch that touches
  the file (roadmap).
