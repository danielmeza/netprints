# 0030: The Extensibility package is split into Abstractions and host, with the editor as a facet

## Status

Accepted (2026-10-16, P3 spec `specs/006-editor-extension-host/`, research R12 and R14; the split lands in sub-phase C1,
batch C1a). The Facet section is filled by T072 in sub-phase C2.

## Context

- One `NetPrints.Extensibility` assembly held both the contracts an extension compiles against (`INetPrintsExtension`,
  `IExtensionBuilder`, the manifest model, the node library contracts, the settings descriptors, the host channel
  contracts) and the host (the loader, the load contexts, the registry, the built-in extension). An extension that
  referenced it also referenced `System.Runtime.Loader` and every host type, and any host change was an API change for
  authors (FR-019).
- Nothing of the package has been published, so the split costs no compatibility (FR-116): namespaces do not change and no
  type forwards are added.
- The Authoring analyzers ship inside `NetPrints.Core`. NuGet excludes the analyzers of a transitive dependency by default,
  so an author who references only the contracts would lose them.

## Decision

1. **Two assemblies.** `NetPrints.Extensibility.Abstractions` holds the contracts; `NetPrints.Extensibility` holds the host
   and references Abstractions. Types keep their namespaces (`NetPrints.Extensibility`, `.Hosting`, `.Loading`, `.Nodes`,
   `.Settings`), so a host consumer's `using` directives do not change.
2. **What moves to Abstractions.** `INetPrintsExtension`, `IExtensionBuilder`, `ExtensionApi`, `ExtensionManifest`,
   `ExtensionManifestException` and `ExtensionDiagnosticCodes`; `INodeLibrary`, `NodeKindDescriptor`, `NodeSuggestion`,
   `GraphKinds` and `NodeGraphKinds`; `ExtensionSettingsDescriptor` and `ISettingsStore`; `IHostChannel`, `HostMessage`,
   `HostLaunchContext`, `HostChannelState` and `InMemoryHostChannel` (extensions build channel pairs in tests). The host
   keeps `ExtensionHost`, `ExtensionLoader`, the load contexts, `ExtensionRegistry`, `RegistryBuilder`, `BuiltInExtension`,
   `BuiltInNodeLibrary`, `NetPrintsSettings`, `JsonFileSettingsStore` and `NullHostChannel`.
3. **Allowed references of Abstractions.** `NetPrints.Core`, `NetPrints.Serialization`, `NetPrints.Reflection`,
   `NetPrints.Catalog`, `Microsoft.Extensions.Logging.Abstractions` and `Microsoft.Extensions.DependencyInjection.Abstractions`
   (10.0.12). It holds no reference to `System.Runtime.Loader` or to the host. `AbstractionsReferenceTests` pins the project
   and package references and the referenced assemblies, and requires `NetPrints.TestExtension` and the `Fx` fixtures to
   reference Abstractions and not the host.
4. **No `Microsoft.Extensions.Options` in the contracts.** `ExtensionSettingsDescriptor<T>.Validator` is a
   `Func<T, IReadOnlyList<string>>` that returns failure messages (an empty list accepts), instead of a
   `ValidateOptionsResult`. The host and the editor adapt their generated options validators to it.
5. **Analyzer delivery.** Abstractions declares its Core dependency with no excluded assets (`PrivateAssets="none"` on that
   reference when the package is packed, S2a), so an author who references only Abstractions or the host still gets the
   Authoring analyzers once. The pack spike and the sample Godot extension, built from packed packages only, verify it; if
   they show a duplicate or a missing analyzer, the `netprints-extension` template references `NetPrints.Core` directly and
   the guide says why.
6. **`GraphPoint` moves to Core** (`NetPrints.Graph`), public and stable from 0.4.0, because Abstractions depends on Core and
   a public contract positions nodes with it.
7. **Public API files are split** with the types: each project has its own `PublicAPI.Shipped.txt` and
   `PublicAPI.Unshipped.txt`.

## Facet

Filled by T072 (sub-phase C2).

## Consequences

- An author's project file has one reference, to Abstractions, and the host assembly never enters the author's compile.
- A change to the loader, the registry or the built-ins is not an API change for authors.
- Abstractions still references Serialization, which brings `System.Reactive` transitively; `InMemoryHostChannel` uses it.
- `ExtensionSettingsDescriptor<T>.Validator` changed shape (experimental, unreleased).
- The package validation baseline for the new package is set in the release that first publishes it (FR-020).
