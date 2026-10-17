# Contract: the extension API (published packages)

FR-019–FR-038, FR-076–FR-081; ADR-0010, ADR-0017, ADR-0020 (amended), ADR-0021 (amended), ADR-0030, ADR-0031,
ADR-0040; research R12–R18, R25. Shapes are sketches: member names may change in implementation if the tests and
this contract change together. The invariants may not: UI-free contracts, typed contexts, first registration wins,
built-ins first, groups removed as a whole, editor contributions ignored by hosts without an editor, and every
settling surface behind an NPXE id.

## 1. Packages (version 0.4.0)

| Package | Assembly contents | Depends on |
|---|---|---|
| `NetPrints.Core` | model, translator, `CSharpLanguageVersion`, `CSharpTarget`, typed pins and pin attributes, `PinRole`, `GraphPoint`, `LocalizedText`, `ExperimentalApiIds`; `analyzers/dotnet/cs`: `NetPrints.Authoring`, `NetPrints.Authoring.CodeFixes` | — |
| `NetPrints.Reflection`, `NetPrints.Catalog` | unchanged roles | Core |
| `NetPrints.Serialization` | documents, mapping, migrations, the JSON format | Core |
| `NetPrints.Extensibility.Abstractions` | contracts only: `INetPrintsExtension`, `IExtensionBuilder`, the manifest model, `ExtensionApi`, node library and search contracts, settings descriptors, host channel contracts, project templates, migrations, `NetPrints.Extensibility.Editor` (facet, descriptors, contexts) | Core (analyzers flow, research R12), Serialization, Reflection, Catalog, Logging.Abstractions, DependencyInjection.Abstractions |
| `NetPrints.Extensibility` | host only: loader, load contexts, `ExtensionHost`, `ExtensionRegistry`, `RegistryBuilder`, `BuiltInExtension`, `JsonFileSettingsStore` | Abstractions |
| `NetPrints.Extensibility.Testing` | `ExtensionTest<TExtension>`, `ExtensionHarness`, the conformance suite, `InMemoryHostChannel` | Extensibility |
| `NetPrints.Extensibility.Testing.Xunit` | `ConformanceTests<TExtension>` | Testing, xunit.v3 |
| `NetPrints.Templates` | `dotnet new` templates `netprints-console`, `netprints-library`, `netprints-extension` | — |

Every library package: `PublicAPI.Shipped.txt`/`Unshipped.txt`, package validation (research R12), a README, XML docs
on every public member. `ExtensionApi.Version` changes with every change to the Abstractions or Extensibility API
files.

## 2. The extension builder

```csharp
public interface IExtensionBuilder
{
    ExtensionManifest Manifest { get; }

    ILoggerFactory LoggerFactory { get; }

    // Existing, flat, model-side: AddNodeLibrary, AddTypeCatalog, AddProjectProfile, AddProjectProperty,
    // AddJsonTypeInfoResolver, AddSettings (NPXE0002), AddHostChannel (NPXE0001), AddClassEmitter/AddMemberEmitter
    // (NPXE0003), AddCatalogProfile (NPXE0004).

    [Experimental(ExperimentalApiIds.NodeSearchProviders)]
    IExtensionBuilder AddSuggestionProvider(INodeSuggestionProvider provider);

    [Experimental(ExperimentalApiIds.ProjectTemplates)]
    IExtensionBuilder AddProjectTemplate(ProjectTemplateDescriptor template);

    [Experimental(ExperimentalApiIds.NodeDataMigrations)]
    IExtensionBuilder AddDocumentMigration(INodeDataMigration migration);

    [Experimental(ExperimentalApiIds.StringResources)]
    IExtensionBuilder AddStringResources(ResourceManager resources);

    [Experimental(ExperimentalApiIds.ExtensionServices)]
    IServiceCollection Services { get; }

    /// <summary>Editor contributions; a Null Object that ignores every call in hosts without an editor.</summary>
    [Experimental(ExperimentalApiIds.EditorContributions)]
    IEditorContributions Editor { get; }
}
```

```csharp
[Experimental(ExperimentalApiIds.EditorContributions)]
public interface IEditorContributions
{
    IEditorContributions AddCommand(CommandDescriptor command);

    IEditorContributions AddPanel(PanelDescriptor panel);

    IEditorContributions AddDashboardTile(DashboardTileDescriptor tile);

    IEditorContributions AddContextMenuItem(ContextMenuItemDescriptor item);

    IEditorContributions AddTooltipProvider(ITooltipProvider provider);

    IEditorContributions AddGoToProvider(IGoToProvider provider);

    IEditorContributions AddStatusBarItem(StatusBarItemDescriptor item);

    IEditorContributions AddDialog(DialogDescriptor dialog);

    // NPXE0005 with the rest of the facet (ADR-0040 amendment 1); NPXE0009 marks only the settings metadata attributes.
    IEditorContributions AddSettingsPage(SettingsPageDescriptor page);

    /// <summary>An avares URI of a resource dictionary holding this extension's DataTemplates and Icon.&lt;id&gt; geometries.</summary>
    IEditorContributions AddViewResources(string resourceUri);
}
```

- The built-in editor registers every one of its contributions through this facet as the in-process extension
  `netprints.editor`, before any other extension (FR-021).
- The CLI, the SDK generator and the build hand out the Null Object facet: an extension that registers editor
  contributions loads there without error (US3 scenario 3).
- In the tail (sub-phase G): `AddInspectorSection`, `AddValueEditor` (NPXE0014) on the facet; `AddGraphRule`
  (NPXE0015) on the builder. They are experimental, so they may land after the publish gate.

## 3. Typed contexts (R5a, R5b)

```csharp
[Experimental(ExperimentalApiIds.EditorContributions)]
public interface ICommandContext
{
    IEditorShell Shell { get; }

    IProjectSession? Session { get; }

    IGraphEditor? ActiveGraph { get; }

    CommandSelection Selection { get; }

    object? Parameter { get; }

    CommandScope Scope { get; }

    IDialogService Dialogs { get; }

    IEditorOutput Output { get; }

    /// <summary>The calling extension's own service provider (ADR-0031).</summary>
    IServiceProvider Services { get; }
}

[Experimental(ExperimentalApiIds.EditorContributions)]
public interface IProjectSession
{
    Project Project { get; }

    CSharpTarget Target { get; }

    IReflectionProvider Reflection { get; }

    IGraphEditor? ActiveGraph { get; }

    /// <summary>The calling extension's session-scoped services.</summary>
    IServiceProvider Services { get; }
}

[Experimental(ExperimentalApiIds.EditorContributions)]
public interface IGraphEditor
{
    NodeGraph Graph { get; }

    IReadOnlyList<Node> SelectedNodes { get; }

    IEditorUndo Undo { get; }

    Node Place(Func<NodeGraph, Node> create, GraphPoint position, NodePin? connectTo);
}
```

- `IPanelContext`, `IStartPageContext`, `IStatusBarContext`, `ISettingsPageContext` and `IDialogContext` carry the
  subsets their kinds need; every member is non-null in the editor host (a test).
- `ICommandHandler` receives `ICommandContext`: `CanExecute`, `ExecuteAsync(context, cancellationToken)`,
  `DynamicLabel`. Handlers are stateless.
- `CommandDescriptor.DefaultGestures` is `IReadOnlyList<CommandGesture>?` (ADR-0034).

## 4. Descriptors

```csharp
public readonly record struct LocalizedText(string Key, string DefaultText);

public sealed record CommandDescriptor(string Id, LocalizedText Label, ICommandHandler Handler)
{
    public string? IconId { get; init; }

    public IReadOnlyList<CommandGesture>? DefaultGestures { get; init; }

    public CommandScope Scope { get; init; } = CommandScope.Global;

    public MenuPlacement? Menu { get; init; }   // menu id, group id, order; ids split from labels (L1)

    public int? CommandBarOrder { get; init; }
}

public sealed record ContextMenuItemDescriptor(string Id, ContextMenuTarget Target, string CommandId, string Group, int Order)
{
    public Func<ICommandContext, bool>? AppliesTo { get; init; }
}

public sealed record StatusBarItemDescriptor(string Id, StatusBarAlignment Alignment, int Order, Func<IStatusBarContext, object> CreateViewModel)
{
    public Func<IStatusBarContext, bool>? AppliesTo { get; init; }
}

public sealed record SettingsPageDescriptor(string Id, LocalizedText Title, string? SectionKey, int Order)
{
    /// <summary>A custom page view model; null means the page is generated from the section's schema.</summary>
    // Under the descriptor's NPXE0005; NPXE0010 is retired (ADR-0040 amendment 1).
    public Func<ISettingsPageContext, object>? CreateViewModel { get; init; }

    public string? ParentPageId { get; init; }   // extension pages sit under "netprints.settings.extensions"
}

public sealed record ProjectTemplateDescriptor(string Id, LocalizedText DisplayName, LocalizedText Description, string ProfileId, ProjectOutputType OutputType)
{
    public string? IconId { get; init; }

    public IReadOnlyList<TemplateFile> Files { get; init; } = [];
}

public interface INodeDataMigration
{
    string NodeKind { get; }        // must start with "<manifest id>/"

    int FromVersion { get; }

    void Migrate(JsonObject node);  // touches only this node's JSON
}
```

`PanelDescriptor`, `DashboardTileDescriptor`, `DialogDescriptor`, `ITooltipProvider` and `IGoToProvider` keep their P3a
shapes (`specs/005-editor-shell/contracts/contributions.md`) with `LocalizedText` labels and typed-context factories
instead of `IServiceProvider`.

## 5. Icons and view resources (N8, ADR-0021 amendment)

- An extension icon id is `<extension id>.<name>`; it resolves through the resource key `Icon.<extension id>.<name>`
  in a dictionary added with `AddViewResources`.
- An unknown icon id draws the fallback glyph and records NPX010.
- Contributed views use theme tokens only; the conformance kit scans them (NPK016).

## 6. Experimental ids

The table is in research R16 and in the extension guide's "API stability" section (FR-030). An extension opts in per
id with `<NetPrintsExperimentalOptIn Include="NPXE00nn" />`; without it, the compiler reports the id (US3 scenario 5).

## 7. Lifecycle and failures

- **Groups**: the app group (built-ins, then per-user extensions in load order) and one session group per open project
  (data-model §2). A session group is removed as a whole when its project unloads; no item of it remains on any
  surface (SC-004).
- **First wins**: within a kind, the first registration of an id wins; a later duplicate is NPX004. Two commands with
  overlapping gestures in an overlapping scope: the first keeps the gesture, the second is NPX009. A contribution that
  names an unknown id (command, menu, icon) is NPX010.
- **Failures**: a throwing `Register` adds nothing of that extension and shows NPX005 in Errors; a throwing handler
  shows the error dialog naming the extension and the command; a throwing provider loses only its own results for
  that call (NPX011, logged once per provider and session); a throwing view factory shows a placeholder naming the
  extension (research R27).

## 8. Host exports (ADR-0031)

An extension's provider is built from `IExtensionBuilder.Services` plus exactly: `ILoggerFactory`, `ILogger<T>` for the
extension's own types, `TimeProvider`, and `IOptionsMonitor<T>` for the extension's own settings sections. Session
services reach it through `IProjectSession.Services`. Registrations that try to replace or configure host services
(closed generics over host types, `IConfigureOptions`/`IPostConfigureOptions`/`IValidateOptions` for host options) are
rejected. Core services are not replaceable in P3.

## 9. Manifest and API version

- `themes` (string array) is accepted from `netprintsApi` 0.4; an older `netprintsApi` with `themes` is NPX001.
- `ExtensionApi.Version` becomes 0.4 (P3's release, 0.4.0) with the first P3 API change and changes again with every
  later shipped change (a test ties it to the API files).
- Project-declared extensions load in the CLI and the build without a trust check, like analyzers and build tasks;
  the extension guide and ADR-0030 say so (FR-038).

## 10. Conformance kit

```csharp
public abstract class ExtensionTest<TExtension>
    where TExtension : INetPrintsExtension, new()
{
    protected TestState State { get; } = new();

    protected Task<ExtensionTestResult> RunAsync(CancellationToken cancellationToken);
}

public sealed class ConformanceSuite
{
    public static Task<ConformanceReport> RunAsync(ExtensionHarness harness, ConformanceOptions options, CancellationToken cancellationToken);
}

public abstract class ConformanceTests<TExtension>   // NetPrints.Extensibility.Testing.Xunit: one [Fact] per check
    where TExtension : INetPrintsExtension, new();
```

Check ids NPK001–NPK017 are listed in [analyzer-diagnostics.md](analyzer-diagnostics.md) §6.

## 11. Tests that pin this contract

- An architecture test: Abstractions references only its allowed list and contains no `System.Runtime.Loader`;
  `NetPrints.TestExtension` and the `Fx.*` fixtures compile against Abstractions only.
- The public API analyzers and package validation for every package; the `ExtensionApi.Version` tie test.
- `NetPrints.TestExtension` contributes one item of every kind; a headless test checks each surface shows it and that
  none remains after its project unloads (SC-004).
- Failure fixtures: throwing `Register`, handler, provider, view factory; each leaves the editor usable and names the
  extension.
- The CLI loads an extension that registers editor contributions and ignores them.
- An `Fx` extension with its own services resolves its own registrations and only the allowed host exports; a
  forbidden registration is rejected.
- An `Fx` extension without the NPXE opt-in fails to compile with that id (a compile test in the Analyzers project).
- A label with no translation shows its default text; a migration fixture migrates a `kindVersion` 0 node.
