# Unity and NetPrints: what is known

Date: 2026-10-14. Unity is not built yet; "NetPrintsUnity" is an owner-approved roadmap candidate (2026-09-25). This note collects
what the repo and earlier research already say, and no new research was done for it. Unity is the thinnest of the engine notes: the
facts are few, and most of what NetPrints would do is inferred.

Tags: **[V]** verified (a URL or a repo file), **[I]** inferred. The roadmap is `.specify/memory/roadmap.md` in the repository (not part
of the website); lines cited are as of `d3f65b8e`.

---

## 1. What is known

### 1.1 The roadmap candidate

The roadmap entry "NetPrintsUnity" (`.specify/memory/roadmap.md`, near line 930) [V] describes:

- A UPM package whose `ScriptedImporter` or `AssetPostprocessor` generates the `.g.cs` next to each `.netpc.json`. Unity does not
  compile through a csproj, so `NetPrints.Sdk` does not apply.
- A `unity` catalog profile over `UnityEngine.dll` and `Library/ScriptAssemblies`.
- Emitters for `MonoBehaviour`, `ScriptableObject`, `[SerializeField]` and `[RequireComponent]`.
- Entry points `Awake`, `Start`, `Update` and `OnCollision*`.
- Latent nodes as coroutines or, in Unity 6, `Awaitable`.
- An `ICSharpTargetProvider` for the C# target (ADR-0038), because Unity has no MSBuild evaluation: C# 9, no `IsExternalInit`
  unless the project defines one, no covariant returns.
- "P1 extension points and project profiles should keep this case in mind."

The roadmap's run-profile note (near line 257) also says a public `AddRunProfileKind` is added when U3 or NetPrintsUnity needs it [V].

### 1.2 The C# compiler Unity uses

Unity manual, "C# compiler" (`https://docs.unity3d.com/6000.2/Documentation/Manual/csharp-compiler.html`, fetched 2026-10-14) [V]:

- Supported C# version: C# 9.0, with Roslyn as the compiler.
- Unsupported C# 9.0 features, as listed on that page: suppress emitting `localsinit` flag; covariant return types; module
  initializers; extensible calling conventions for unmanaged function pointers; init-only setters.
- `System.Runtime.CompilerServices.IsExternalInit` is needed for complete record functionality, but exists only in .NET 5+, which
  Unity does not support; developers can define the type themselves. The page also advises against records in serialized types,
  because Unity's serialization system does not support them.

Consequence: a language version number alone cannot describe what NetPrints may emit for Unity; "C# 9" still lacks init-only
setters and covariant returns, and records need a user-defined `IsExternalInit` [V, consequence per the attributes research P7, F17].
This is why ADR-0038 models a `CSharpTarget` of language version plus types and runtime features, not just a version.

### 1.3 Build and run

- Unity compiles scripts itself; there is no NetPrints build for a Unity project (the NetPrintsUnity entry: Unity does not compile through a csproj) [V roadmap; the run-profile
  research and its audit M1]. `NetPrints.Sdk` does not apply, so the `NetPrintsGenerate` target path is not available [I].
- Play happens inside a running Unity editor, so running from NetPrints does not start a process the way a `Project` or `Executable`
  profile does: it goes through the host channel (a message to an open editor) [V from the run-profile audit M1; I for the design].
- Unity's own docs recommend attaching an IDE to the Editor or Player for debugging [V per the run-profile report; not re-fetched].
- The Unity Hub installs editors under a versioned folder and the project's version is in `ProjectSettings/ProjectVersion.txt`, so
  locating the editor to open is engine-specific logic that belongs in an extension [I].

### 1.4 Other Unity facts the earlier research recorded

- Generic attributes on Unity's Mono may have runtime issues; not verified [I]. The Unity target provider can deny the feature
  (ADR-0038 decision 6 and 7 give the mechanism).
- Unity Visual Scripting uses attributes only to author custom nodes; no source was found for a visual-scripting tool that lets users
  add arbitrary attributes to graph members (attributes research P11) [I].
- The earlier Godot showcase discarded Unity as the demo engine: a coordinator conclusion that Unity is "not standard .NET"
  (Mono/IL2CPP) and that a domain reload hurts Roslyn-hot-compiled blueprints [I; an opinion from that session, not verified].

---

## 2. How NetPrints would integrate

Everything below is inferred from the roadmap and the ADRs unless tagged otherwise.

1. **Generation.** A UPM package (an importer or postprocessor inside Unity) calls the generator for each `.netpc.json` and writes the
   `.g.cs` beside it, because there is no csproj [roadmap, V]. Whether the importer shells out to the NetPrints CLI or hosts the
   generator is not decided [I].
2. **C# target.** An `ICSharpTargetProvider` contribution supplies the target (ADR-0038 decision 9: "hosts without MSBuild evaluation
   (Unity) get an `ICSharpTargetProvider`"): C# 9, `IsExternalInit` absent unless the project defines one, no covariant returns, and
   a denial for generic attributes if Mono cannot run them. The research advised deferring the interface; ADR-0038 lists it as
   decided and leaves its shape open, to be defined with the Unity phase [V ADR; I for the Unity values beyond the manual].
3. **Catalog.** A `unity` catalog profile over `UnityEngine.dll` and `Library/ScriptAssemblies` supplies the types, so graphs
   see Unity's API without MSBuild [roadmap, V].
4. **Attributes and inspector.** Unity-specific attributes (`[SerializeField]`, `[RequireComponent]`, and any others) are registered by
   the Unity plugin as attribute descriptors with inspector sections and type editors, per ADR-0039 decision 11. Which Unity
   attributes to include is an open question; the roadmap names `[SerializeField]` and `[RequireComponent]` only.
5. **Emitters and entry points.** `MonoBehaviour` and `ScriptableObject` class emitters and the `Awake`, `Start`, `Update` and
   `OnCollision*` entry-point nodes [roadmap, V].
6. **Latent nodes.** Coroutines (an `IEnumerator` state machine) or `Awaitable` on Unity 6. Whether the target provider gates this by
   Unity version is open [roadmap names both; I for gating].
7. **Run.** Play goes through the host channel to an open Unity editor, not through a profile that starts a process. ADR-0037 decision 4
   leaves a public experimental `AddRunProfileKind` to be added "when U3 or NetPrintsUnity needs it", shaped then: build or no build,
   an outcome handled without a process, and a process lifetime hint [V ADR]. A Unity extension kind that opens the editor (version
   file plus Hub path) is the case the audit named [V audit M1; I for the design].
8. **Samples.** The Unity plugin can ship samples under `samples/<id>/` (roadmap "Samples from remote sources", README) [V].
9. **Where code lives.** The core and workspace stay engine-agnostic (an owner rule recorded during the Godot showcase): no Unity
   names, variables or paths in `src/NetPrints.Workspace` or the core [V, owner rule].

---

## 3. Open questions

1. How the UPM package invokes the generator: the CLI, a hosted generator, or a Unity-side port. Domain reload and import timing
   against hot compilation are unexamined.
2. The shape of `ICSharpTargetProvider` (the ADR leaves it open), and its exact Unity values: the manual's list is the only source,
   so runtime features beyond it (for example default interface members, static abstract members) are unverified for Unity.
3. Generic attributes on Mono: not verified.
4. Whether Unity 6 `Awaitable` and coroutines need different emitters, and how latent nodes pick one.
5. The host channel for Play, and the shape of the run-kind extension point (build or no build, no process, lifetime).
6. How a `unity` catalog profile reads `Library/ScriptAssemblies` while Unity recompiles, and which assemblies it covers.
7. Which Unity attributes ship (beyond `[SerializeField]` and `[RequireComponent]`), and how Unity's serialization limits (no records in
   serialized types) show up as graph rules.
8. How NetPrints finds the project's Unity version and the editor (`ProjectSettings/ProjectVersion.txt`, Hub paths per OS).
9. Whether `.meta` files and Unity's GUID model affect where generated files and graphs live (nothing in the sources covers it).
10. Which Unity versions are supported.
