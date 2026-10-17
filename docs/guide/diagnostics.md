# Diagnostics

Every NetPrints diagnostic has a stable code: a prefix that names the part of NetPrints that reports it and a number that is never reused for another meaning. Severities are the defaults; a project can change an analyzer rule's severity in `.editorconfig`.

This page lists every code. The tests keep it in step with the sources: a code that is added or removed without a row here fails `DiagnosticsGuideTests`.

| Prefix | Reported by |
|---|---|
| NPA | The authoring analyzers of node classes (shipped in the `NetPrints.Core` package) |
| NPC | The catalog generator and reader |
| NPD | The graph document reader |
| NPE | The editor |
| NPI | The repository's internal source generators (NetPrints' own source, not shipped) |
| NPS | The repository style analyzers (NetPrints' own source, not shipped) |
| NPT | The translator and the code generator |
| NPW | The project system (project evaluation and restore) |
| NPX | The extension loader |
| NPXE | The experimental extension APIs |

## NPA: authoring

These rules check the node classes whose pins the convention generator declares (`[NodeKind]` and `[NodeBase]` classes with `partial` pin properties). They ship with the `NetPrints.Core` package and report as errors, except the rules marked otherwise below.

<span id="npa001"></span><span id="npa002"></span><span id="npa003"></span><span id="npa004"></span><span id="npa005"></span><span id="npa006"></span><span id="npa007"></span><span id="npa008"></span><span id="npa009"></span><span id="npa010"></span><span id="npa011"></span><span id="npa012"></span><span id="npa020"></span><span id="npa021"></span><span id="npa022"></span><span id="npa023"></span><span id="npa024"></span><span id="npa040"></span><span id="npa041"></span><span id="npa042"></span>

| Code | Severity | Cause |
|---|---|---|
| NPA001 | error | A pin property or its class is not `partial`, the property is not get-only, or `[Pin]` or `[PinType]` sits on a property that cannot take it. The fix makes the class and the property partial. |
| NPA002 | error | Two pins with the same key in one collection, counting the pins of base classes. |
| NPA003 | error | A constructor does not call `DeclarePins()` exactly once, or adds pins to a declared collection before it. |
| NPA004 | error | A pin key is empty or contains `.`, `/` or `~`. |
| NPA005 | error | A `[NodeBase]` class adds pins imperatively to a collection in which the convention declares pins (the offsets would be wrong). |
| NPA006 | error | A `[DefaultValue]` on a typed input data pin does not convert to the pin's type. |
| NPA007 | error | An untyped data pin or an output type pin has no `[PinType]`, or its member is missing or is not an `ObservableValue<BaseType>`. |
| NPA008 | error | A pin property name does not end in `Pin` and has no `[Pin]` attribute. The fix adds `[Pin("Name")]` with the current name as the key. |
| NPA009 | error | A partial pin property in a class with neither `[NodeKind]` nor `[NodeBase]`. The fix adds `[NodeBase]` to an abstract class or `[NodeKind("TODO.kindId")]` to a concrete one; replace the placeholder with the real kind id. |
| NPA010 | warning | A declared pin key is in neither `NodePins.Shipped.txt` nor `NodePins.Unshipped.txt`. The fix records it in the unshipped file. |
| NPA011 | error | A shipped pin key is no longer declared; the message names the old key and, when a new key took its place, the new one. The fixes keep the shipped key with `[Pin("old")]`, or remove it on purpose with a `*REMOVED*` line in the unshipped file. |
| NPA012 | info | A node kind declares pins, but the project has neither `NodePins.Shipped.txt` nor `NodePins.Unshipped.txt`, so no pin key is tracked. Add both files (empty is fine) to turn tracking on. |
| NPA020 | error | An icon id given to a command, panel or template descriptor is a constant that does not match `^[a-z0-9]+(\.[a-zA-Z0-9]+)+$`, or is a `netprints.icon.` id the icon registry (`IconIds`) does not know. |
| NPA021 | error | A gesture passed to `CommandGesture.Parse` or `TryParse` does not parse, or a single-key or Shift-only gesture (function keys excepted) is a default of a command in the Global scope. |
| NPA022 | error | A constant contribution `Id` does not match the id pattern `^[a-z0-9]+(\.[a-zA-Z0-9]+)+$`, or, when the project sets the MSBuild property `NetPrintsExtensionId` (the NetPrints.Core package makes it compiler-visible), does not start with that id and a dot. |
| NPA023 | error | Two commands in one project are built with the same constant id. |
| NPA024 | warning | Two commands in one project are built with the same menu id, group and order. |
| NPA040 | warning | A resource key a `[NodeKind]` class implies (`Node.<kind>.Title` when the class overrides `TitleIsFixed`, `Node.<kind>.Pin.<direction>.<collection>.<key>`, as in `NodePins.Shipped.txt`) is missing from the neutral resx. The repository sets it to an error. A project without a resx among its `AdditionalFiles` is not checked. |
| NPA041 | error | A satellite resx has a key the neutral file lacks, or a `{n}` placeholder count that differs from the neutral text. |
| NPA042 | info | A `Node.<kind>.*` resx key of a `[NodeKind]` class in the project that no declaration uses. |

The pin key files are `AdditionalFiles` of the project (`NodePins.Shipped.txt` and `NodePins.Unshipped.txt`), one `<kind> <direction>.<exec|data|type>.<key>` line per pin; a project without them is not tracked.

## NPC: catalogs

Catalog codes are also described in [Catalogs](catalogs.md#diagnostics).

| Code | Severity | Cause |
|---|---|---|
| NPC001 | error | A catalog request names an assembly that is not referenced. |
| NPC002 | error | Unknown profile id. |
| NPC003 | error | Profile file unreadable or invalid, or a catalog id or accessor name that is not valid. |
| NPC004 | warning | An annotation sits on a member or type that is not public; it is ignored. |
| NPC005 | warning | A member was skipped because a type it uses comes from an assembly that is not available. |
| NPC006 | error | Two catalogs with the same id, or the same accessor name, in one build. |
| NPC007 | warning | An annotated library is compiled without documentation comments, so its own catalog has no summaries. |
| NPC101 | error | The catalog `schemaVersion` is not supported by this reader. |
| NPC102 | error | The catalog file is malformed. |
| NPC103 | warning | Two loaded catalogs share an id; the later one is ignored. |

## NPD: graph documents

Document issues are reported when a graph file is read; all but NPD008 leave the document loaded.

| Code | Severity | Cause |
|---|---|---|
| NPD001 | warning | A node of unknown kind (a missing or untrusted extension) is preserved unchanged. |
| NPD002 | warning | A connection was dropped: missing node, unknown pin key or incompatible pins. |
| NPD003 | warning | A pin state was dropped because its pin key is unknown. |
| NPD004 | warning | A layout entry was ignored: unknown graph key or node id. |
| NPD005 | warning | Unknown `NetPrintsProfile`; the default profile was used. |
| NPD006 | warning | A project extension is not trusted; its nodes are preserved but inactive. |
| NPD007 | warning | A node or member id was duplicated in its scope; the later one got a fresh id. |
| NPD008 | error | A graph document could not be read at all: malformed JSON or XML, or an unsupported extension. |
| NPD009 | warning | A node or member id did not match the id format; it was replaced by a fresh id. |
| NPD010 | warning | A call-method node was stored as pure but its method returns no value; it loaded as impure. |
| NPD011 | warning | A custom event argument name was not a valid C# identifier or was used twice; it was repaired. |

## NPE: editor

| Code | Severity | Cause |
|---|---|---|
| NPE001 | error | The project folder has graph files, but the project system loaded none. |

## NPI: internal generators

These rules belong to the generators that compose the editor (`[Singleton<T>]`, `[SessionScoped<T>]`, `[ScopeFactory]`) and to the smart enum generator (`[SmartEnum]`). They check NetPrints' own source and are not shipped in any package; they report as errors.

<span id="npi001"></span><span id="npi002"></span><span id="npi003"></span><span id="npi004"></span><span id="npi005"></span><span id="npi010"></span><span id="npi011"></span>

| Code | Severity | Cause |
|---|---|---|
| NPI001 | error | A singleton takes a session-scoped service, directly or through its factory method. |
| NPI002 | error | A service attribute on a type that cannot be registered: abstract, an open generic, not implementing the service, or a `Factory` that is missing, not unique or returns another type. Also a `[ScopeFactory]` method whose return type is neither registered nor constructible. |
| NPI003 | error | A registered type has no accessible constructor, or several and none marked `[ActivatorUtilitiesConstructor]`. |
| NPI004 | error | Two registrations of one service in the same lifetime where another registered type takes a single instance of it. |
| NPI005 | error | A type registered for several services is disposable (the container would dispose the one instance once for each registration) or the registrations have different lifetimes. |
| NPI010 | error | A `[SmartEnum]` type is not a top-level `abstract partial class`. |
| NPI011 | error | A smart enum member is a property or a field that is not `static readonly`, or a static field initializer reads `All` before every member field is declared (`All` would be read half-built). |

## NPS: repository style

These rules check NetPrints' own source. They are not shipped in any package and are off unless a repository turns them on in `.editorconfig`; the severities are the ones this repository sets.

<span id="nps001"></span><span id="nps002"></span><span id="nps003"></span>

| Code | Severity | Cause |
|---|---|---|
| NPS001 | error | An attribute list shares a line with its member or with another attribute list. The fix puts each on its own line. |
| NPS002 | error | No blank line between adjacent members when either is a property, indexer, method, constructor, operator or event, any public member (consts and fields included), or a member with braces, a comment or attributes. Enum members and record parameters are out of scope. The fix inserts a blank line. |
| NPS003 | suggestion | Non-public fields and consts on one line each, with no comment or attribute, are grouped without a blank line. A hint only; it never fails a build. |

## NPH: repository hygiene

These rules check NetPrints' own source in `src/` and `tests/`. They are not shipped in any package and are off unless a repository turns them on in `.editorconfig`; the severities are the ones this repository sets. Reviewed exceptions live in `HygieneLedger.txt` at the repository root, one row per site (`CODE | path | detail`); the ledger only shrinks.

<span id="nph001"></span><span id="nph002"></span><span id="nph003"></span><span id="nph004"></span><span id="nph005"></span><span id="nph006"></span>

| Code | Severity | Cause |
|---|---|---|
| NPH001 | error | The null-forgiving operator (`x!`, `null!`, `default!`) at a site the ledger does not list. Model the null instead. |
| NPH002 | error | A type or delegate name that ends in `VM` or has `VM` before an upper-case letter (`MVVM` excepted). Use `ViewModel`. |
| NPH003 | error | A diagnostic or kind code literal (`NPT`, `NPD`, `NPX`, `NPW`, `NPC`, `NPS`, `NPA`, `NPH`, `NPI`, `NPK`, `NPR`, `NPXE`) outside its constants class, or a code constant declared outside a designated file or twice in one project. |
| NPH004 | error | A `#pragma warning` directive, a `#nullable disable` or `restore`, or a `[SuppressMessage]` the ledger does not list for its file, member and rule (ADR-0003); a listed one must carry an `ADR-0003` justification. |
| NPH005 | error | `ConfigureAwait(false)` in a file where `.editorconfig` sets `VSTHRD111` to `none` (UI-bound code resumes on the UI thread). |
| NPH006 | error | A string literal, or an interpolated string with literal text, passed to a user-visible text sink (a dialog title or message, status-bar text). Put the text in the resx and fill its placeholders with `TextFormat.Of`. A command label, panel, dialog, tile or template text is a `LocalizedText` with a convention key and is not reported. |

## NPT: translation and code generation

| Code | Severity | Cause |
|---|---|---|
| NPT000 | error | A translation failure that is not yet mapped to a more specific code. |
| NPT001 | error | An impure node is used outside its own event entry's exec flow. |
| NPT002 | error | An event and a method on the same class share a name. |
| NPT003 | error | A graph holds a node of an extension that is not loaded; its generated file is left as it was. |
| NPT004 | error | A method-local variable's name conflicts with a parameter, another local or a C# keyword. |
| NPT005 | error | A member emitter threw while emitting a member. |
| NPT006 | error | No translator is registered for a node's runtime type. |
| NPT007 | error | A member emitter produced an invalid result. |
| NPT008 | error | A required input data pin is not set and has no connection. |
| NPT009 | error | A variable has the `Const` modifier, which cannot be emitted yet. |

## NPW: project system

| Code | Severity | Cause |
|---|---|---|
| NPW001 | error | No .NET SDK could be registered; the editor and the command line cannot evaluate projects. |
| NPW002 | warning | Restoring the project out of process failed; loading continues with what was restored. |
| NPW003 | error | MSBuild evaluation of the project failed. |
| NPW004 | warning | The project multi-targets; the first target framework is used. |
| NPW005 | warning | MSBuild reported a diagnostic while opening the project. |
| NPW006 | info | `LangVersion` is `latest`, `latestMajor`, `preview` or `default`; it resolved to the smaller of the framework's maximum and the newest version NetPrints knows. Pin `<LangVersion>` so the generated code does not change with the SDK. |
| NPW007 | info | `LangVersion` is newer than the target framework's default; it is kept. |
| NPW008 | info | `LangVersion` is unknown or newer than the newest version NetPrints knows; that version is used. |
| NPW009 | warning | A reference of the project could not be read (not a managed assembly, or damaged or locked); the editor skips it and loads the other references. |

## NPX: extension loader

Extension codes are also described in [Extensions](extensions.md).

| Code | Severity | Cause |
|---|---|---|
| NPX001 | error | Invalid or missing manifest, or an assembly without exactly one usable extension type. |
| NPX002 | error | The manifest asks for an incompatible `netprintsApi` version. |
| NPX003 | error | A dependency is missing, failed to load, or is part of a dependency cycle. |
| NPX004 | error | Another extension with the same id was found first. |
| NPX005 | error | Creating the extension or its `Register` method threw. |
| NPX006 | warning | A contribution was rejected because of a conflict or inconsistency; the extension stays loaded. |
| NPX007 | error | The extension assembly is missing or could not be loaded. |
| NPX008 | error | The host or a dependency provides an older version of an assembly than the extension, or a private library it ships, was built against. |
| NPX009 | warning | Two commands asked for the same gesture in overlapping scopes; the first registration keeps it. |
| NPX010 | warning | A contribution names an id nothing registered; for an icon id, the editor draws the fallback glyph. |
| NPX011 | warning | A contributed handler, provider or view model factory threw while the editor ran it; its result is skipped and the failure is logged once per provider and session. |
| NPX012 | info | A project's extension has the same id as one in the user extension directories; the project's copy serves the session and the message names both versions. |
| NPX013 | warning | A contributed id (for now a node search provider's) does not start with the manifest id of the extension that contributed it; the contribution is dropped. |

## NPXE: experimental APIs

These are compiler errors on a use of an unstable extension API until the project opts in; see [API stability](extensions.md#api-stability).

| Code | Severity | Cause |
|---|---|---|
| NPXE0001 | error | The host channel API is experimental. |
| NPXE0002 | error | The extension settings API is experimental. |
| NPXE0003 | error | The class and member emitter API is experimental. |
| NPXE0004 | error | The catalog engine and profile API is experimental. |
