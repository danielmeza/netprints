# 0031: The editor composes with MS.DI and our own registration generator

## Status

Accepted (2026-10-08, P3 planning; implementation lands in P3 along the migration path below). It replaces the
earlier plan of a hand-written composition root with no container, which was the roadmap's position until
2026-10-08. The reserved number 0031 ("editor composition and DI") is used here.

## Context

- The editor builds its services by hand (`EditorServices`, `EditorContext`, `NoServices`, `StartPageServices` and
  `?? new Default()` fallbacks). Extensions will need to register their own services (P3), and the owner does not
  want a container choice to cost adaptability or extensibility.
- The editor cannot be NativeAOT, so AOT friendliness of a container is not a reason to choose or reject one:
  extensions are loaded from assemblies at run time (`ExtensionLoader`), the Desktop project references the
  MSBuild locator and Roslyn workspaces with runtime assets excluded, project loading goes through MSBuild, the
  translators compile user code with Roslyn, and Roslyn's own MEF host and five `System.Composition` assemblies ship
  with the editor, with `System.Composition.Hosting` not trim-safe. The trimming ratchet (`eng/trim-warnings.txt`)
  stays a hygiene count, not a deployment target.
- A research pass (container survey, spike, measurements) and an adversarial audit of it preceded this decision.
  Measured, compose time against a hand-wired baseline on the same machine, 15 cold processes per case:

  | Flavor | MS.DI with factory lambdas | Pure.DI | Autofac |
  |---|---|---|---|
  | JIT, logger created first | +4.0 ms | +1.4 ms | +36.5 ms |
  | ReadyToRun, logger created first | +2.1 ms | +0.2 ms | +18.1 ms |

  At 180 services under ReadyToRun the container is at most about 6% of the start page. Factory lambdas start no
  faster than type registrations; the generator is worth having for trim safety and compile-time checks, not for
  speed. The orders of magnitude hold, but JIT deltas carry a 30-50% uncertainty band.
- Microsoft states no plan for a source-generated DI in the current MS.DI model (runtime#44432 closed 2024-08-30;
  aspnetcore#62104 closed as a duplicate 2025-05-25), so the generator is ours.

## Decision

1. **Container: Microsoft.Extensions.DependencyInjection 10, with our own attributes and registration generator.**
   `[Singleton<T>]` and `[SessionScoped<T>]` on the implementation, in the style of the other NetPrints attributes
   (ADR-0024 family); a source generator emits one `AddEditorServices()` extension whose registrations are factory
   lambdas that call the constructors, so there is no reflection-based activation. Analyzers and snapshot tests ship
   with it. The reasons are not speed: one registration style shared with extensions, native Options and logging,
   Microsoft servicing, and zero added size.
2. **Where the container lives.** The container exists only at the root and per open project (one
   `AsyncServiceScope` per session). Below that, class and graph contexts use generated typed factories
   (`ClassContextFactory`, `GraphScopeFactory`); nothing is resolved per row, node or pin. Services are lazy by
   default; eager work runs after the first frame and is timed in the start-up measurement (M1).
3. **Extensions.**
   - `IExtensionBuilder.Services` is an `IServiceCollection`, experimental at first (ADR-0017). This ties the public
     API to `Microsoft.Extensions.DependencyInjection.Abstractions` 10, which the host provides; the NPX version
     check covers it.
   - **One provider per extension**, built from the extension's collection plus an explicit host export allow-list
     (`HostExports`). Host singletons are bridged as instances, never as lambdas. Session services reach an
     extension through a scoped accessor per extension.
   - Reasons: no conflicts between extensions (two extensions registering the same shared contract would otherwise
     get each other's implementation), disposal failures stay inside one extension, and one extension can be unloaded
     on its own, which keeps hot enable and disable open. Orchard Core's per-tenant provider is the production
     precedent.
4. **Replacing or decorating core services** is possible only through seams the host declares, added on demand.
   There are none at the start.
5. **Design requirements from the audit** (each gets a test):
   - The seam filter is an allow-list. It accepts only service types defined in the extension's own load context,
     declared seams and contribution contracts. It rejects closed generics over host types (for example
     `IOptions<HostOptions>`, `ILogger<HostSessionService>`) and any `IConfigureOptions`, `IPostConfigureOptions` or
     `IValidateOptions` for host-owned options; a deny-list on service type is bypassed by all of these.
   - Disposal order: sessions first, then the provider, then the load context's `Unload()`. Each extension's
     disposal is wrapped so one throwing `Dispose` does not stop the rest, including host services (MS.DI aborts the
     remaining disposals otherwise).
   - Startup validation runs on every provider build, the extensions' providers included. At .NET 11 this moves to
     `IAsyncStartupValidator` (`IStartupValidator` becomes obsolete, SYSLIB0066).
   - The conformance kit (ADR-0010) runs a capture and leak test per extension: an extension that subscribes to a
     host singleton's event pins its load context. Leak tests must not release in Tier-0 code such as top-level
     `Main`, which reports false leaks.
6. **Rejected.**
   - MEF and VS-MEF: not trim-safe, no unload, and it conflicts with decision 3.
   - A hybrid (generated or Pure.DI root plus MS.DI at the extension boundary): two DI models in one editor.
   - Autofac: the cost is not the reason (see the table); it adds a second registration style and its adapter is not
     AOT-annotated. Its one real gain, container-managed nested scopes with per-scope registrations, is not needed
     because our generator emits the typed factories.
   - Pure.DI is reconsidered only if the registration generator proves weaker than expected in the R5-0 spike or the
     generated files pass about 600 lines; its `DI.Setup()` stays mandatory and cross-assembly use is unverified.
   - A hand-written composition root with no container: it was the previous plan; it gives extensions no way to
     register services.
7. **Options and configuration now.** The configuration binding generator (`EnableConfigurationBindingGenerator`),
   and `[OptionsValidator]` source-generated validators instead of `ValidateDataAnnotations` (which is
   `RequiresUnreferencedCode`). There is no generic host, so the composition calls the startup validator itself.
   How `IConfiguration` relates to `JsonFileSettingsStore` (settings stay JSON files in the config folder) is settled
   in the P3 spec; smart enums are never bound through `IConfiguration` (ADR-0034).
8. **ReadyToRun.** Desktop releases are published ReadyToRun per assembly with `DisableDynamicEngine=true`.
   Measured on the real editor: the start page goes from 1034 to 519 ms (-50%) and opening a project from 2688 to
   1869 ms (-30%), for +38% install size. `DisableDynamicEngine` stops MS.DI from emitting IL after the second call
   of a service, which ReadyToRun cannot precompile: over the first 100 sessions at 180 services it costs 18.5 ms
   without the switch and 2.2 ms with it, and it keeps emitted code from pinning collectible types. Composite
   ReadyToRun is not adopted. The publishing work stays in P8 (FR-104); the property is set in the same change.

## Migration path

Each step lands in the P3 order of the roadmap and has its gate.

1. **R5-0.** Baselines for M1 (shell start), M2 (graph scope) and M4 (trimming) before any change, plus the M5 spike:
   a throwaway copy of `EditorServices` composed three ways. This is the gate between MS.DI with our generator and
   Pure.DI.
2. **With S1.** `SingletonAttribute<T>`, `SessionScopedAttribute<T>`, the registration generator, its analyzers and
   snapshot tests, and the hygiene test in its new form.
3. **App and shell.** `EditorServices` builds a `ServiceCollection` with `AddEditorServices()` plus the
   host-provided instances; `EditorContext` stays as a facade resolved from the root so callers do not change;
   `TestComposition` swaps doubles with `services.Replace(...)`; `NoServices`, `StartPageServices` and the
   `?? new Default()` fallbacks go as the typed contexts (R5a) land. Gate: M1 (composition budget 150 ms, including
   the provider build and the first resolves).
4. **Project session.** `ProjectSessionScope` extracted from `ProjectLoader`; `BackupScope`, `RecoveryService` and
   the template services become session-scoped. Gate: a resolve-everything test.
5. **Class and graph.** Generated `ClassContextFactory` and `GraphScopeFactory`; `GraphDocumentFactory` keeps only
   document ownership. Gate: M2 with counting fakes.
6. **R7 and N1.** `IExtensionBuilder.Services`; `ExtensionRegistry` builds and owns the extension providers;
   `HostExports` becomes the allow-list; contribution contexts carry `Services`; the conformance kit validates the
   providers. ADR-0020 is amended (contribution factories get typed contexts plus the extension's provider).
7. **Later, with hot enable and disable.** Collectible `ExtensionLoadContext`, cache eviction and the unload
   `WeakReference` test; the extension-seams item for replace and decorate; the Avalonia "restart required" rule.
8. **Shrink `EditorContext`** as each consumer moves to constructor-injected interfaces when it is touched.

## Consequences

- Extension authors register services the way ASP.NET Core developers expect, and the host's logging and Options
  work in extensions unchanged.
- The public extension API depends on MS.DI.Abstractions 10; a major MS.DI change is a NetPrints API decision.
- We own a generator and its analyzers (about 2.5 units in S1), and the allow-list filter and its bypass tests.
- Nothing resolves from the container per row, node or pin; a hygiene test and the M2 counting fakes enforce it.
- The CLI keeps MS.DI as today. Project-declared extensions load in the CLI without a trust check, the same as
  analyzers and build tasks; the guide says so.
- The roadmap lines that said "no container in the editor" are amended; ADR-0020 gets an amendment at step 6.
