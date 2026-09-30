# Reading the Xaml.Behaviors catalog

The catalog lists every behavior, trigger and action in the `Xaml.Behaviors.*` packages, split by job into the other
files in this folder. `SKILL.md` routes each job to its file; this page explains what an entry means, which packages
NetPrints uses, and where the catalog came from.

## Reading a catalog entry

Each group heading names the group (the source folder it lives in) and its package. The `Use when` line that follows
ends with the NetPrints stance in brackets:

| Stance | Meaning |
|---|---|
| `[avoid …]` | Don't use it: the job belongs in a VM command or service. |
| `[prefer X …]` | Use X instead (for example `CanvasPopup`, ADR-0004, or `ThemeVariantScope`). |
| `[not used]`, `[not needed]`, `[not applicable …]`, `[no]`, `[never]`, `[never commit]` | NetPrints has no case for it. |
| anything else | Where it fits in NetPrints. |

Each table row names the type, its kind (Behavior, Trigger or Action) and what it does. The catalog doesn't list
properties: run `scripts/behavior-props.cs` (see `SKILL.md`), which prints every public property with the base class
that declares it. Build after a change: compiled XAML rejects an unknown property.

## Packages

| Package | In NetPrints |
|---|---|
| `Xaml.Behaviors.Interactions` | referenced |
| `Xaml.Behaviors.Interactions.Custom` | referenced |
| `Xaml.Behaviors.Interactions.DragAndDrop` | referenced |
| `Xaml.Behaviors.Interactivity` | transitive (the base types of the three above) |
| `Xaml.Behaviors.Animations`, `.Interactions.Draggable`, `.Interactions.Events`, `.Interactions.ReactiveUI`, `.Interactions.Responsive`, `.Interactions.Scripting` | not referenced |

Versions are pinned in `Directory.Packages.props`. Don't add the `Xaml.Behaviors.Avalonia` meta package: it also pulls
in Animations, Draggable, Events and Responsive, which NetPrints doesn't use (ADR-0007). To use a type from an
unreferenced package, add a `PackageVersion` there and a `PackageReference` in
`src/NetPrints.Editor/NetPrints.Editor.csproj`, and say why in the PR.

`Xaml.Behaviors.Interactions.Events` duplicates the `InputElement/Triggers` family with typed, trim-safe types
(`XEventTrigger` versus `XTrigger` style). If it is ever referenced, pick one family per file and stay consistent
within it.

## Source

Generated from the `wieslawsoltes/Xaml.Behaviors` source at `45387e3` (release 12.0.7), grouped by source folder,
then split by job. When the packages are upgraded, regenerate the tables for the new version and keep the recipes.
