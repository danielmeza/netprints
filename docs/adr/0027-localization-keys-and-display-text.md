# 0027: Localization keys and display text

## Status

Accepted (2026-10-16, P3 spec, `specs/006-editor-extension-host/`, FR-027 and FR-044, research R13). Decided autonomously on the
owner's standing instruction for P3 (decide and record). Three product choices below are defaults that are still pending the
owner; they are marked and listed in the P3 notes under "Owner questions taken by default".

## Context

- Display text in the editor is a literal in C# or XAML. P3 adds contribution descriptors that extensions fill, so the
  strings need a stable identity before the extension packages publish (owner decision 8: L1 in P3, L2 in P6).
- Moving the existing strings is L2 and is not part of this ADR. This ADR fixes how new and contributed text is keyed,
  where the neutral text lives, how it falls back, which culture the editor runs in, and what the analyzers check.

## Decision

1. **`LocalizedText`.** A public `record struct LocalizedText(string Key, string? DefaultText)` in `NetPrints.Core`. It
   resolves through resource managers in order: the UI culture, its parent, the neutral resources (the framework's
   `ResourceManager` fallback), then the default text, then the raw key. A plain string converts to a text that is its own
   key and default (`FromLiteral`).
2. **Convention keys.** `Node.<kind>.Title`, `.Description`, `.Keywords`; `Node.<kind>.Pin.<in|out>.<exec|data|type>.<key>`
   (the line format of `NodePins.Shipped.txt`); `Category.<id>`; `Command.<id>.Label` and `.Tooltip`; `Menu.<menuId>`;
   `Panel.<panelId>.Title`; `Settings.<section>.<key>.Label` and `.Description`. A descriptor built with a literal takes the
   key its id implies; a descriptor built with an explicit `LocalizedText` keeps its key. The menu id (`MenuPlacement.Path`)
   is separate from the header text, which is `Menu.<id>`.
3. **Neutral resources.** `src/NetPrints.Core/Graph/NodeStrings.resx` (node titles, pins, the built-in category) and
   `src/NetPrints.Editor/Strings.resx` (category, commands, menus, panels), compiled with the MSBuild strongly typed
   generator. The editor class is public; Core's is internal behind one public member, `CoreStrings.Resources`, because the
   public API tracker lists every generated property of a public class, one line per key.
4. **Extension resources.** `IExtensionBuilder.AddStringResources(ResourceManager)` (experimental, NPXE0011) collects an
   extension's resources into `ExtensionRegistry.StringResources`, in registry order.
5. **Language.** `netprints.language` in the `netprints` section is a culture name or empty (the operating system's
   culture). The validator refuses a string that is not a predefined culture, and the section then falls back to its
   defaults like any invalid section. The editor sets `CultureInfo.CurrentUICulture` and `DefaultThreadCurrentUICulture` once
   at start. Semi and Ursa follow the app culture: the editor sets their `Locale`, and when the theme does not take it (it
   reads back a different culture) the editor sets `en-US`.
6. **Analyzers.** NPA040 (a key a `[NodeKind]` class implies is missing from the neutral resx; a warning for authors, an
   error in this repository), NPA041 (a satellite has an extra key or a different `{n}` placeholder count) and NPA042
   (a `Node.<kind>.*` key no declaration uses; info). They read `.resx` files from `AdditionalFiles`; a project without a
   resx is not checked. NPH006 (a literal passed to a UI sink) lands with the repository analyzers in C2.

## Defaults pending the owner

- **Pin labels with no resource show the raw key** (AOP follow-up question 4). Today a pin shows its name; nothing
  changes until a pin label is resolved through `LocalizedText`.
- **The built-in node category stays the single "NetPrints" category** (question 9), key `Category.netprints`.
- **A language change applies after a restart** (question 5). There is no live switch: compiled `x:Static` text and the
  Semi and Ursa locale are read at start, which matches the "Restart to apply" bar of the settings pages (ADR-0040).

## Consequences

- Command labels, panel titles and menu headers resolve through `Strings` today; node titles, pin labels, category names
  and the resources of extensions are keyed and checked, but the views that show them still show the model's names. The
  views switch to the resources in the tasks that touch them, and L2 moves the rest.
- Satellite resx files are not shipped yet; the fallback chain is tested against in-memory resources, and the first
  translation adds a satellite and NPA041 starts to apply to it.
- Semi has locales for 16 cultures and Ursa for 7 (versions pinned in `Directory.Packages.props`); any other culture shows
  the English strings in those controls while the editor's own text follows the app culture.
