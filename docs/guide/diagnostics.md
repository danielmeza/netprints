# Diagnostics

Every NetPrints diagnostic has a stable code: a prefix that names the part of NetPrints that reports it and a number that is never reused for another meaning. Severities are the defaults; a project can change an analyzer rule's severity in `.editorconfig`.

This page lists every code. The tests keep it in step with the sources: a code that is added or removed without a row here fails `DiagnosticsGuideTests`.

| Prefix | Reported by |
|---|---|
| NPC | The catalog generator and reader |
| NPD | The graph document reader |
| NPE | The editor |
| NPI | The repository's internal source generators (NetPrints' own source, not shipped) |
| NPS | The repository style analyzers (NetPrints' own source, not shipped) |
| NPT | The translator and the code generator |
| NPW | The project system (project evaluation and restore) |
| NPX | The extension loader |
| NPXE | The experimental extension APIs |

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

| Code | Severity | Cause |
|---|---|---|
| NPS001 | error | An attribute list shares a line with its member or with another attribute list. The fix puts each on its own line. |
| NPS002 | error | No blank line between adjacent members when either is a property, indexer, method, constructor, operator or event, any public member (consts and fields included), or a member with braces, a comment or attributes. Enum members and record parameters are out of scope. The fix inserts a blank line. |
| NPS003 | suggestion | Non-public fields and consts on one line each, with no comment or attribute, are grouped without a blank line. A hint only; it never fails a build. |

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
| NPX008 | error | A dependency provides an older version of an assembly than the extension was built against. |

## NPXE: experimental APIs

These are compiler errors on a use of an unstable extension API until the project opts in; see [API stability](extensions.md#api-stability).

| Code | Severity | Cause |
|---|---|---|
| NPXE0001 | error | The host channel API is experimental. |
| NPXE0002 | error | The extension settings API is experimental. |
| NPXE0003 | error | The class and member emitter API is experimental. |
| NPXE0004 | error | The catalog engine and profile API is experimental. |
