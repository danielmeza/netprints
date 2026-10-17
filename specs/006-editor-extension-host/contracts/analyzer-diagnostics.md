# Contract: analyzer and host diagnostics

FR-001–FR-011, FR-031, FR-034, FR-035, FR-077–FR-080; ADR-0003 (amended), ADR-0024, ADR-0025, ADR-0031; research R2,
R3, R4, R8, R25, R27. Codes are constants in a `*DiagnosticCodes` class per family (ADR-0003 literal discipline);
analyzer rules are tracked in `AnalyzerReleases.Shipped.md`/`Unshipped.md` (RS2008). A batch may refine a rule's
wording or split a rule if the tests and this table change together; a code is never reused for another meaning.

## 1. Families

| Prefix | Owner | Shipped to authors | Where it runs | Status |
|---|---|---|---|---|
| NPS | `NetPrints.RepoAnalyzers` | no | `src/`, `tests/` | new (A) |
| NPA | `NetPrints.Authoring` (+ `.CodeFixes`) | yes, inside the `NetPrints.Core` package | every project that references Core | new (B, C, G) |
| NPH | `NetPrints.RepoAnalyzers` | no | `src/` (VSTHRD switches also in `tests/`) | new (C2) |
| NPI | `NetPrints.RepoAnalyzers` (DI and smart-enum generators) | no | `src/` | new (A) |
| NPX | `NetPrints.Extensibility` (host diagnostics) | n/a (runtime) | loader, registry, editor Errors | extended (C2, D1) |
| NPK | `NetPrints.Extensibility.Testing`, `netprints extension verify` | yes (the kit) | author tests, `verify` | new (D2) |
| NPR | run profile resolver (`NetPrints.Workspace`) | n/a (runtime) | editor, `netprints run` | new (E); table in [run-profiles.md](run-profiles.md) |
| NPW | workspace (project evaluation, package changes) | n/a (runtime) | editor, CLI | extended (A, C1, H) |
| NPC, NPD, NPE, NPT, NPXE | unchanged | | | |

`docs/guide/diagnostics.md` (new, FR-008) lists every prefix and every code with its severity and a one-line cause;
a staleness test compares it with the `*DiagnosticCodes` classes and the analyzer release files.

## 2. NPS — repository style (sub-phase A)

| Code | Severity | Rule | Fix |
|---|---|---|---|
| NPS001 | error | An attribute shares a line with its member or with another attribute | split onto own lines |
| NPS002 | error | No blank line between adjacent members when either is a property, indexer, method, constructor, operator or event; any public member (consts and fields included); or a member with braces, a comment or attributes. Non-public one-line fields and consts with no comment and no attributes may group; enum members and record parameters are out of scope | insert blank line |
| NPS003 | suggestion | Grouped non-public fields (IDE hint only; never fails a build) | insert blank line |

## 3. NPA — authoring (shipped)

| Code | Default severity | Rule | Lands in |
|---|---|---|---|
| NPA001 | error | A pin property or its class is not `partial`, the property is not get-only, or its pin type does not match the attribute | B (S2b); fix: make partial |
| NPA002 | error | Duplicate pin key in one collection, including base-class pins | B |
| NPA003 | error | The constructor does not call `DeclarePins()` exactly once, or adds pins to a declared collection before it | B |
| NPA004 | error | Invalid pin key: empty, or containing `.`, `/` or `~` | B |
| NPA005 | error | A base class adds pins imperatively to a declared collection (offset unknown) | B |
| NPA006 | error | `[DefaultValue]` does not convert to the pin's `T` | B |
| NPA007 | error | The `[PinType]` member is missing or has the wrong type | B |
| NPA008 | error | A pin property name does not end in `Pin` and has no `[Pin("…")]` (the convention cannot derive a key) | B; fix: add `[Pin]` |
| NPA009 | error | Pin properties in a class without `[NodeKind]` or `[NodeBase]` | B; fix: add the marker |
| NPA010 | warning | A declared pin key is not in `NodePins.Shipped.txt`/`Unshipped.txt` | B (S4); fix: add to Unshipped |
| NPA011 | error | A shipped pin key is no longer declared (the old and new key are named) | B (S4); fixes: keep key (`[Pin("old")]`), remove on purpose |
| NPA012 | info | A node kind declares pins, but the project has neither `NodePins.Shipped.txt` nor `NodePins.Unshipped.txt` (AB-12) | B (S4) |
| NPA020 | error | An icon id literal does not match the icon id pattern (ADR-0021) or, for built-ins, is not registered | C2 (S6) |
| NPA021 | error | A gesture literal does not parse, or breaks ADR-0020's scope rule for its descriptor's scope | C2 (S6) |
| NPA022 | error | A constant contribution id does not match the id pattern or the owner prefix | C2 (S6) |
| NPA023 | error | Duplicate constant command id in a compilation | C2 (S6) |
| NPA024 | warning | Duplicate (menu id, group, order) | C2 (S6) |
| NPA030–NPA033 | error | Node kinds by convention (S7): document record not in a JSON context; kind id format; duplicate kind ids; `CanSetPure` true without a `Pure` document member | G (tail) |
| NPA040 | warning (repo: error) | A convention resource key is missing from the neutral resx | C1 (L1) |
| NPA041 | error | A satellite resx has a key the neutral one lacks, or a `{n}` placeholder count differs | C1 (L1) |
| NPA042 | info | A resx key that no declaration uses | C1 (L1) |

## 4. NPH — repository hygiene (sub-phase C2, after the publish gate)

| Code | Replaces (`NetPrints.Core.Tests`) | Rule |
|---|---|---|
| NPH001 | `SourceHygieneTests.NoNullForgivingOperator` | `!`, `null!`, `default!` in `src/` |
| NPH002 | `SourceHygieneTests.NoTypeNameEndsInVM` | a type name ending in `VM` |
| NPH003 | `SourceHygieneTests.NoRawDiagnosticCodeLiteralsOutsideTheirConstants` | a diagnostic or kind code literal outside its constants class (regex extended with NPS, NPA, NPH, NPI, NPK, NPR) |
| NPH004 | `SourceHygieneTests.NoUnlistedSuppressions` | `#pragma warning disable`, or a `[SuppressMessage]` not in the allowlist AdditionalFile (the ADR-0003 ledger) |
| NPH005 | `UiBoundCodeNeverUsesConfigureAwaitFalse` | `ConfigureAwait(false)` in UI-bound code (scope from `.editorconfig`) |
| NPH006 | (new, L1) | a string literal passed to a known UI sink (`LocalizedText` default excepted): `CommandDescriptor` labels, dialog titles and messages, status-bar text |

All at `error` in `src/`. Each replaced test is removed only after the analyzer is red on the test's planted violation
and green on the repository (red/green). VSTHRD002 and VSTHRD103 go to `error` in `tests/`. XAML rule E7 stays a test
(ADR-0007 amendment).

## 5. NPI — internal generators (sub-phase A)

| Code | Rule |
|---|---|
| NPI001 | A `[Singleton<T>]` service depends (constructor parameter, directly or through a factory) on a `[SessionScoped<T>]` service |
| NPI002 | A service attribute on an abstract or open generic type, or on a type that does not implement the service type |
| NPI003 | A registered type has more than one accessible (public or internal) constructor and none is marked `[ActivatorUtilitiesConstructor]` |
| NPI004 | Two registrations of the same service in one scope where the consumer takes a single instance |
| NPI005 | A type registered for several services is disposable (the container would dispose the one instance once per registration) or its registrations have different lifetimes; a non-disposable type with one lifetime is registered once and forwarded for the other services |
| NPI010 | A `[SmartEnum]` type is not an `abstract partial class` |
| NPI011 | A smart-enum member is not a `static readonly` field of the type, or a static lookup reads `All` before `All` is declared |

## 6. NPK — conformance checks and `verify` (sub-phase D2)

| Code | Check | Kit | `verify` |
|---|---|---|---|
| NPK001 | Manifest: valid, id format, `netprintsApi` within the host's | yes | yes |
| NPK002 | Packaging: the loader's folder layout; `NetPrintsExtension` package type; no host assembly copies | yes | yes |
| NPK003 | Type identity: contracts resolve to the host's assemblies, not private copies | yes | yes |
| NPK004 | Registration is pure and repeatable (twice gives the same registry) | yes | — |
| NPK005 | No registry issues (NPX004, NPX006, NPX009, NPX010, NPX013) | yes | — |
| NPK006 | Node round trip: every node kind saves and loads byte-identically | yes | — |
| NPK007 | Translation compiles: generated C# for each node kind compiles | yes | — |
| NPK008 | Deterministic emitters: two runs write identical bytes | yes | — |
| NPK009 | Settings: sections serialize, validate and export a schema | yes | — |
| NPK010 | Host channel lifecycle: open, message, close, dispose | yes | — |
| NPK011 | Coexistence with the built-ins and a noisy neighbour | yes | — |
| NPK012 | Disposal: no exception, idempotent, no leak of the load context | yes | — |
| NPK013 | Service providers: builds, validates, only allowed host exports, capture and leak test | yes | — |
| NPK014 | Graph rules: deterministic, graph unchanged, cancellation honoured, isolated when throwing (G, with graph rules) | yes | — |
| NPK015 | Editor contributions: ids prefixed, labels resolvable, gestures parse and follow scope rules, icon ids resolve | yes | — |
| NPK016 | Contributed views: no colour or size literal a theme cannot reach (E2/E8 rules over the extension's `.axaml`) | yes | — |
| NPK017 | API compatibility: every NetPrints member the extension references exists in the host | — | yes |

Each check fails with a message naming the code, the rule and the offending item; each has a fixture extension in
`tests/Fixtures/Extensions/Conformance/` that fails it (FR-078, SC-011).

## 7. NPX — host diagnostics added by P3

| Code | Severity | Case |
|---|---|---|
| NPX008 (extended) | error | also host-provided and transitive references (FU-1, sub-phase A) |
| NPX009 | warning | gesture conflict in an overlapping scope; the first registration keeps it |
| NPX010 | warning | a contribution names an unknown id (command, menu, icon, parent page) |
| NPX011 | warning | a contributed handler, provider or view failed at runtime (logged once per provider and session) |
| NPX012 | info | a project's extension shadows a per-user copy of the same id (both versions named) |
| NPX013 | warning | a contributed id, node kind, migration or template does not start with the manifest id |
| NPX014 | warning | an extension settings section failed validation; its defaults apply |

## 8. NPW — workspace messages added by P3

| Code | Severity | Case |
|---|---|---|
| NPW006 | info | `LangVersion` `latest`, `preview` or `default` resolved; pin `<LangVersion>` |
| NPW007 | info | `LangVersion` newer than the target framework's default |
| NPW008 | info | `LangVersion` unknown or newer than NetPrints knows; `MaxKnown` used |
| NPW009 | warning | a reference could not be read; skipped (FU-2) |
| NPW010 | error | a package change's restore failed or was cancelled; the project and props files were put back (H, [nuget-references.md](nuget-references.md)) |
| NPW011 | warning | a package source could not be reached; offline results are shown (H) |
| NPW012 | warning | a package source needs credentials that no non-interactive credential provider supplied (H) |
| NPW013 | info | a package reference cannot be edited safely (a version from an MSBuild property, several conditioned items); it is shown read-only (H) |

## 9. Tests that pin this contract

- Every NPS, NPA, NPH and NPI rule: one passing and one failing case at exact spans; every code fix: before and after
  (SC-001).
- Every generator: a snapshot of its output, a compile test, an incrementality test (FR-008).
- The diagnostics guide staleness test.
- Every NPK check: its failing fixture; every NPX and NPR code: a test that produces it.
