# NetPrints graph file format: research and recommendation

Date: 2026-09-25. Scope: the on-disk format for `*.netpc.json` class graphs (spec `003-core-refactor`,
`data-model.md` and `contracts/document-format.md`, read from the `003-core-refactor` worktree). This is research only.
Nothing in the spec or code was changed.

Status: the owner approved the recommendation on 2026-09-25 (keep canonical JSON; fold the §6 changes into
the P1 spec before implementation).

## TL;DR

- **Keep JSON through System.Text.Json as the single source of truth. Do not adopt YAML, TOML, KDL or a
  custom DSL now.** The P1 direction is right: versioned DTOs, `IDocumentFormat`, `$kind`
  discriminator, sorted maps, positions kept apart from logic, and no volatile fields.
- The weak points are not the choice of JSON. They are **identity and line layout** in schema v1.
  The tools that failed at VCS (Godot 3, Node-RED, Shader Graph v1, ComfyUI, Grasshopper, Pure Data)
  did so because of sequential or positional ids, one-line blobs, counters, and positions mixed into
  logic. The syntax was not the cause. Schema v1 still has four of these hazards:
  1. **Sequential node ids** (`n<max+1>`). When two branches each add a node, both get the same id.
     The spec treats a duplicate id as a hard `DocumentFormatException`.
  2. **Index-based pin references** (`in.data.0`). Adding or removing a parameter or argument silently
     re-targets connections, both on merges and on signature changes.
  3. **Index-based graph keys** (`methods/0`). When a method is reordered, or two branches each add a
     method, the layout is silently attached to the wrong graph.
  4. **STJ indentation puts every scalar on its own line.** A position `[x, y]` takes 4 lines and a
     connection takes 4 lines, so one logical change shows up as a multi-line hunk. A merge of unrelated
     but adjacent entries then conflicts.
- Fix these **in schema v1 now**, before any user files exist. Any later fix needs a migration.
- Custom format: **not now; probably never as the stored format.** NetPrints already commits a
  readable text projection of the logic: the generated `*.netpc.g.cs`. Revisit a DSL only if
  users start hand-authoring graphs at scale.
- Later: build a **NetPrints git merge driver** (`netprints merge %O %A %B %P`) that merges by
  identity. It is feasible (about 1–2 weeks with the existing DTOs and mapper). It helps most on
  layout and add/add merges. It cannot be the only line of defense, because GitHub and GitLab web
  merges do not run custom drivers, so the plain-text format must merge well by itself.

---

## 1. Comparison table

Ratings: ●●● good, ●● acceptable, ● poor. "Effort" is the added cost for NetPrints beyond what P1
already plans.

| Format | Diffability | Mergeability (plain git) | Human editing | LLM friendliness | .NET support / perf | Schema / editor tooling | Effort |
|---|---|---|---|---|---|---|---|
| **JSON, canonical (STJ, custom line rules)**, recommended | ●●● when leaf records are one line each; ●● with default STJ indenting | ●● (●●● with stable ids and one-record-per-line); trailing-comma edits at array ends cause spurious conflicts | ●● (quotes and commas are noisy, but no ambiguity) | ●●● most-trained format; native structured-output / constrained decoding; JSON Schema usable in prompts and tool calls | ●●● in-box, source-gen, AOT, fastest in .NET; polymorphism via `[JsonDerivedType]` | ●●● JSON Schema; VS Code and JetBrains validate and complete via `$schema` / SchemaStore; .NET 9+ `JsonSchemaExporter` | Low (already planned; +~200 LOC canonical writer) |
| JSONC / JSON5 | ●●● trailing commas remove end-of-array diff noise | ●●● slightly better than JSON | ●●● comments, trailing commas | ●●● | ● STJ **reads** comments and trailing commas but cannot **write** them. No mainstream .NET JSON5 writer. Comments are lost on round-trip. | ●● VS Code JSONC mode; JSON5 support weaker | Medium (custom writer) for a small gain |
| YAML | ●●● | ●● indentation-based hunks; a moved block re-indents, and hand-resolved conflicts easily change the structure | ●●● for small files; poor for deep graphs | ●●● (one small benchmark favours YAML for *comprehension*), but whitespace errors are common when *generating* deep structures | ●● YamlDotNet is mature, but AOT needs the separate `Vecc.YamlDotNet.Analyzers.StaticGenerator`, which has limits (structs, generic containers) | ●●● Red Hat YAML LS uses JSON Schema | Medium; Norway/implicit-typing pitfalls (the typed DTOs mitigate this) |
| TOML | ●●● for flat config | ●● | ●● arrays of tables for nested graphs are awkward | ●● | ●● Tomlyn (source-gen, AOT) | ●● Taplo supports JSON Schema | Medium; a poor fit for deep nesting |
| KDL (v2) | ●●● node-shaped syntax, one node per line | ●●● | ●●● | ● little training data; models confuse v1 and v2 syntax | ● KdlSharp (v2) and kdl-net (v1 only) are small projects with few maintainers; no source-gen or AOT story | ● KDL Schema spec exists; almost no editor support | High |
| XML (legacy DataContract) | ●● | ● Grasshopper `.ghx` and LabVIEW show it failing in practice | ● | ●● verbose; worst token efficiency | ●●● in-box | ●● XSD | None (read-only legacy) |
| MessagePack / binary | ● | ● | ● | ● | ●●● | ● | Low, but defeats the product goal |
| **Custom textual DSL** | ●●● if designed for it | ●●● if line-oriented | ●●● | ● unknown to models (needs a grammar in every prompt); a hallucinated syntax still "looks right" | You write the parser, printer, error recovery and formatter | You write the TextMate grammar, LSP, formatter and schema-equivalent validation | High (months), plus permanent maintenance |

LLM note: the only quantitative comparison found ([improvingagents.com, nested formats](https://www.improvingagents.com/blog/best-nested-data-format/))
tested small models (GPT-5 Nano, Llama 3.2 3B, Gemini 2.5 Flash Lite) on *reading* nested data.
YAML scored best on 2 of 3 models, JSON on Llama, and XML worst. The effect is model-dependent and
concerns comprehension, not editing. For *writing and editing*, JSON's advantages matter more. It
has JSON Schema-constrained structured outputs, strict validation with line and position errors, and
unambiguous whitespace. A reasonable LLM workflow is: strip `layout`, hand the model the logic plus
the published schema, validate, then re-canonicalize on save.

---

## 2. Survey: what each tool does and the lesson

### Game engines and editors

**Godot `.tscn` / `.tres`.** An INI-like text format designed for VCS. It has sections for the file
descriptor, `ext_resource`, `sub_resource`, `node` (paths relative to the root) and `connection`
([TSCN format docs](https://docs.godotengine.org/en/stable/engine_details/file_formats/tscn.html)).
Godot 3 used sequential integer resource ids. Teams hit constant conflicts when two people added
resources ([godot#25416](https://github.com/godotengine/godot/issues/25416)), and sub-resources
reordered randomly between saves ([#18283](https://github.com/godotengine/godot/issues/18283)).
Godot 4 switched to string ids (`1_abcde`, `Type_xxxxx`) and `uid://` file UIDs, and deprecated
`load_steps`, a counter that changed on every add. Conflicts remain common enough that a third-party
**semantic merge driver** exists ([gdmerge](https://github.com/hyprtuna/gdmerge)). It merges nodes,
resources and connections by identity, reassigns colliding ids, and installs through
`.gitattributes`. UID files are often set to `merge=ours`
([Bugnet](https://bugnet.io/blog/fix-godot-resource-uid-conflict-after-merge)).
*Lesson:* sequential ids and derived counters are merge poison, and a readable text format is not
enough by itself. Plan for identity-based ids from day one, and a merge driver later.

**Unity YAML scenes and prefabs.** With "Force Text" serialization, each object is a separate
YAML document (`--- !u!1 &<fileID>`). Objects reference each other by arbitrary per-file `fileID`
numbers and cross-file GUIDs ([Format of Text Serialized files](https://docs.unity3d.com/Manual/FormatDescription.html),
[UnityYAML](https://docs.unity3d.com/Manual/UnityYAML.html)). It is text, but it is not reviewable:
diffs are opaque field soups. Unity ships **UnityYAMLMerge / Smart Merge**, a semantic 3-way merge
tool. It is configured as a git mergetool and has `mergerules.txt` for array and float handling
([Smart Merge manual](https://docs.unity3d.com/6000.3/Documentation/Manual/SmartMerge.html)).
Unity's own graph tools had a worse problem. Visual Scripting and Shader Graph (pre-10) stored
polymorphic node JSON **as escaped strings inside the asset**, "all data for a single node is stored
in 1 line". Shader Graph 10 moved to a multi-JSON-object format with string `objectId`s, explicitly
to fix merges ([Unity Graphics PR #222](https://github.com/Unity-Technologies/Graphics/pull/222)).
*Lesson:* never nest serialized blobs; give every object a stable string id; readable diffs need
domain-level naming, not just text.

**Unreal `.uasset`.** Binary. Diff and merge happen only inside the editor. The Blueprint Diff tool
compares graphs side by side through the source-control plugin, and teams use it with git through
editor `-diff` launch scripts ([gist](https://gist.github.com/Panakotta00/c90d1017b89b4853e8b97d13501b2e62),
[Epic community tutorial](https://dev.epicgames.com/community/learning/tutorials/7O6a/unreal-engine-git-plugin-and-the-blueprint-diff-tool)).
Real merging is effectively "pick a side". A visual merge prototype exists ([MergeAssist](https://github.com/KennethBuijssen/MergeAssist)).
Unreal's answer to contention is **locking plus One File Per Actor**, which splits a level into one
external file per actor so people stop colliding on one file
([OFPA docs](https://dev.epicgames.com/documentation/en-us/unreal-engine/one-file-per-actor-in-unreal-engine)).
Two text escapes exist. One is **T3D** clipboard text (`Begin Object Class=/Script/BlueprintGraph...`),
which people already feed to LLMs ([Epic forum](https://forums.unrealengine.com/t/training-genai-to-script-with-blueprints-ascii-text/2236668)).
The other is the experimental JSON-like **`.utxt` Text Asset Format**, which never became mainstream
([forum](https://forums.unrealengine.com/t/save-assets-as-text-rather-than-binary/501522),
[converter](https://github.com/elliotttate/unreal-utxt-converter)).
*Lesson:* this is the gap NetPrints targets. Granularity matters: NetPrints already stores one file
per class, the OFPA analogue. The demand for Blueprint-to-text for LLMs confirms the value of text.

**Blender `.blend`.** A binary memory dump with no text form for node trees. The community built
add-ons that export geometry-node groups to deterministic JSON for git, keeping the `.blend` as a
cache: [GNToolkit](https://github.com/oobma/GNToolkit) ("deterministic JSON... canonical change
detection... commit/pull/conflict loop") and [NodeKit](https://github.com/j10er/NodeKit).
*Lesson:* users rebuild determinism and canonical JSON themselves when a tool lacks them. NetPrints
should provide both natively.

### Flow and automation tools

**Node-RED.** `flows.json` is a flat array of node objects. Each has a random hex `id`, `x`/`y`
**inline**, and `wires` (arrays of target ids) on the source node. By default it is written as
**one line**, so git cannot merge it at all ([node-red#2515](https://github.com/node-red/node-red/issues/2515)).
The fix was `flowFilePretty` (pretty-printing, the default in Projects mode;
[#2085](https://github.com/node-red/node-red/issues/2085)). The Projects feature adds a git-backed
UI and node-level visual diffs in the editor ([Projects](https://nodered.org/docs/user-guide/projects/)).
Community comparers classify changes as "visual (movements)" versus "content"
([flow-compare](https://github.com/gorenje/node-red-contrib-flow-compare)). Large-flow merges are
still painful ([forum](https://discourse.nodered.org/t/resolving-git-merge-conflicts-for-flow-json/32472)).
*Lesson:* pretty-print by default; separate movement from content; a semantic diff viewer helps a lot.

**n8n.** Git-backed "environments" store one JSON file per workflow. The docs say plainly that n8n
"can't detect conflicts on workflows" and recommend one-directional flow and a single owner per
workflow ([n8n push/pull](https://docs.n8n.io/source-control-environments/using/push-pull/),
[Git and n8n](https://docs.n8n.io/source-control-environments/understand/git/)).
*Lesson:* without stable ids and line discipline, the fallback is social: an ownership policy.

**ComfyUI.** The workflow JSON has `nodes` (integer `id`, float `pos`/`size` inline, UI flags) and
`links` (v0.4 tuples, v1 objects), plus **global counters** `lastNodeId` / `lastLinkId`
([Workflow JSON spec](https://docs.comfy.org/specs/workflow_json)). There is also a separate "API
format" with no layout. Any two edits conflict on the counters, and float positions such as
`866.3932495117188` churn.
*Lesson:* no counters in the file; round or snap positions; an execution-only projection without
layout is useful (compare Enso and Dynamo).

**Dynamo `.dyn`.** Dynamo 2.0 moved from XML to JSON. The design deliberately **splits topology
(Nodes, Connectors with GUIDs) from a `View` section (NodeViews: X/Y, annotations, camera)**
([DynamoDS#7747](https://github.com/DynamoDS/Dynamo/issues/7747),
[Dynamo 2.0 blog](https://dynamobim.org/to-dynamo-2-0-and-beyond/)).
*Lesson:* this is the P1 model (logic plus a trailing `layout` section in the same file). GUID ids
avoid add/add collisions.

**Grasshopper `.gh` / `.ghx`.** `.ghx` is XML, adopted hoping git would work. It "hasn't worked out
very well". Component positions cause constant trivial conflicts, and embedded binary streams on
single long lines crash diff tools
([McNeel forum](https://discourse.mcneel.com/t/version-control-for-gh/128645),
[Grasshopper forum](https://www.grasshopper3d.com/forum/topics/version-control)). Graph-level diff
tools emerged instead ([VVD](https://github.com/oderby/VVD), [BranchHopper](https://branchhopper.com/)).
*Lesson:* inline positions dominate the conflict count; never embed blobs.

### Visual languages

**Enso** (especially relevant). A `.enso` file is **real text code** followed by
`\n\n\n#### METADATA ####\n` and **two single-line JSON values**: an id map (`[[{"index":..,"size":..}, uuid], ...]`,
keyed by *character offsets*) and IDE metadata (`{"ide":{"node":{uuid:{"position":{"vector":[x,y]}}}}}`).
Sources: [`ensoFile.ts`](https://github.com/enso-org/enso/blob/develop/app/ydoc-shared/src/ensoFile.ts),
[`metadata.rs`](https://github.com/enso-org/enso/blob/develop/lib/rust/parser/src/metadata.rs),
[default template](https://github.com/enso-org/enso/blob/develop/lib/scala/pkg/src/main/resources/default/src/Main.enso).
Their own issues record what went wrong:
- The id map "tends to take 100x more space than the code it describes"
  ([enso#9257, "Remove IdMap from source file"](https://github.com/enso-org/enso/issues/9257)).
- Offset-keyed metadata breaks on any external edit (you "lose positions, color, etc."), which led
  to a redesign ([enso#11304](https://github.com/enso-org/enso/issues/11304)). The current template
  stores a compressed base64 `snapshot` of the code so the IDE can re-anchor metadata after external
  edits.
- CRLF conversion broke metadata parsing across Windows and Unix ([enso#7994](https://github.com/enso-org/enso/issues/7994)).

*Lessons:* (a) the split into "logic readable, layout in its own section" is right; (b) key the
layout by **stable ids, never by text offsets or indexes**; (c) never put the metadata on one line,
because any two moves then conflict; (d) force LF in `.gitattributes`. NetPrints avoids (b) only if
node ids are stable. The index-based pin and graph keys are the same class of problem.

**LabVIEW.** `.vi` files are binary. Diff and merge use NI's graphical **LVCompare / LVMerge**,
wired into git as difftool and mergetool. They have limits: no support for container types (classes,
libraries, projects), and files must be renamed to open two copies
([SAS Workshops](https://blog.sasworkshops.com/setting-up-lvcompare-and-lvmerge/),
[LabVIEW wiki](https://labviewwiki.org/wiki/Set_up_differencing_capabilities)). The key VCS fix was
**"Separate compiled code from source file"** (LabVIEW 2010). Before it, recompiles rewrote callers:
"a team member who changes 5 VIs might have to check 50 or more VIs into source control"
([Hampel Software](https://hampel-soft.com/blog/separate-compiled-code-from-source/)).
*Lesson:* avoid **ripple saves**. A file must change only when the user changed that graph, never
because a dependency or re-resolved metadata changed. This bears on `SaveAsync` and inline `MethodRef`s (§5).

**Simulink** (added; strong VCS story). `.slx` is zipped XML. MathWorks ships a three-way model
merge ([docs](https://www.mathworks.com/help/simulink/ug/merge-and-resolve-conflicts-using-three-way-merge.html))
and an `mlAutoMerge` git plugin that **auto-merges changes in different subsystems** and falls back
to manual resolution otherwise
([automerge](https://www.mathworks.com/help/simulink/ug/enable-matlab-automerge.html),
[blog 2025](https://blogs.mathworks.com/simulink/2025/06/30/merging-simulink-models-automatically-in-git/)).
*Lesson:* the realistic goal of a merge driver is "auto-merge disjoint changes by identity; hand
real conflicts to a human or a tool". That is achievable for NetPrints.

**Scratch `.sb3`.** A zip holding `project.json`. Blocks form a flat map keyed by random ids, with
`opcode`, `inputs`, `fields`, `parent`/`next` pointers, and x/y on top-level blocks
([Scratch wiki](https://en.scratch-wiki.info/wiki/Scratch_File_Format)). It is not designed for VCS:
zipped and one-line. The `next` **linked-list pointers** mean inserting one block rewrites two
others. Text round-trips exist through scratchblocks ([parse-sb3-blocks](https://github.com/palette-community/parse-sb3-blocks)).
*Lesson:* store edges as a flat set, not as pointers embedded in nodes. P1 already does this.

**Pure Data `.pd`** (added). A line-oriented text format (`#X obj x y name;`, `#X connect 0 0 1 0;`)
where objects are identified by **creation index**. Moves and cut/paste reorder the source, and no
three-way merge tools exist ([Pd forum](https://forum.pdpatchrepo.info/topic/14102/version-control-for-patches),
[format](https://puredata.info/docs/developer/PdFileFormat)).
*Lesson:* line orientation without stable ids still fails, which is exactly the `in.data.0` and
`methods/0` risk.

### Other editors

**Figma.** Proprietary binary (kiwi schema). Collaboration is server-side:
`Map<ObjectID, Map<Property, Value>>` with property-level last-writer-wins, **client-id-prefixed
object ids** so offline clients never collide, and **fractional indexing** for child order
([Figma multiplayer blog](https://www.figma.com/blog/how-figmas-multiplayer-technology-works/)).
Branch merge is all-or-nothing, with per-object conflict choices
([Figma branching](https://help.figma.com/hc/en-us/articles/360063144053-Guide-to-branching)).
*Lesson:* a design to borrow. Treat the document as objects × properties, create ids that cannot
collide, and give order a representation that does not force renumbering. A NetPrints merge driver
is essentially Figma's model applied offline.

**Houdini.** `.hip` is normally binary. File > Save as text writes a plain-text hip, and `hotl`
expands `.hda` asset libraries into a **directory of plain files** "to make it easier to use diffing
and source control" ([SideFX docs](https://www.sidefx.com/docs/houdini/assets/textfiles.html),
[hotl](https://www.sidefx.com/docs/houdini/ref/utils/hotl.html)).
*Lesson:* an optional expanded text form is a retrofit; NetPrints is text-first. Expanding to many
files is an escape hatch if single class files ever grow too large.

**Rive.** `.riv` is a stripped binary runtime format. The `.rev` backup holds the editor-only data,
such as state-machine coordinates ([.riv format](https://rive.app/docs/runtimes/advanced-topic/format)).
Collaboration is cloud-based, not git-based.
*Lesson:* like LabVIEW, keep compiled output apart from source. NetPrints' separate `.netpc.g.cs` is
the right analogue.

---

## 3. Merge strategies (analysis for NetPrints)

**Line-oriented layout rules.** Git merges at hunk granularity, and changes on *adjacent* lines
conflict. So:
- One logical record per line for leaf records: connection, layout entry, pin value, type or
  parameter reference. Default `WriteIndented` breaks `[112, 112]` and `{from,to}` across 4 lines
  each, so every move or edge change becomes a larger hunk, and two edits to neighbouring entries
  conflict.
- Flat, sorted sets for unordered things: connections, layout, maps. P1 already sorts these.
- Stable, non-colliding ids, so two insertions do not produce the same line. With random ids,
  inserts into sorted sets also scatter instead of piling up at the end.
- No counters, hashes, timestamps or derived data (P1 already forbids timestamps and paths; also ban
  counters and anything like `load_steps` or `lastNodeId`).
- JSON's lack of trailing commas means appending after the last array element edits the previous
  line. Both branches appending to the same array always conflict. Sorted-by-id sets mitigate this;
  `nodes` in graph order (append-only) is the remaining hotspot (§5, item 6).

**Layout apart from logic: separate file vs separate section.**

| | Same file, trailing `layout` section (P1) | Sidecar `X.netpc.layout.json` |
|---|---|---|
| Atomic save, rename, copy, `AddGraphAsync` | ●●● one file | ● two files must move together; orphan and stale risk |
| PR review noise | ●● layout hunks at the end of the file | ●●● can be collapsed (`linguist-generated`) |
| Plain-git merge | ●● conflicts in layout block the whole file | ●●● a layout conflict is isolated; resolving it with "ours" is always safe |
| LLM editing | ●●● drop one key | ●●● ignore one file |
| Precedent | Dynamo, Enso | Unity `.meta`-like sidecars (a known source of pain), Rive `.rev` |

Recommendation: **keep the trailing section (P1)**. Key it by stable ids and write one line per
node. Make every entry optional, auto-placing nodes with no entry. Let the future merge driver
resolve layout conflicts automatically. A sidecar is a reasonable P2 option if data shows layout
conflicts dominate. It is only worth it together with a line-oriented layout format (for example
`n2 420 112`), which could use git's built-in `merge=union`. The git docs warn that union
"tends to leave the added lines... in random order"
([gitattributes](https://git-scm.com/docs/gitattributes)), but for a last-entry-wins layout that is
tolerable. Whether GitHub/GitLab web merges honour `merge=union` was **not verified**.

**Custom merge driver (feasibility).** Git runs `merge.<name>.driver = netprints merge %O %A %B %L %P`.
Exit code 0 means clean, non-zero means conflicts; the result is written to `%A`
([gitattributes](https://git-scm.com/docs/gitattributes)). A NetPrints driver in the CLI (a dotnet
tool) would do the following:
1. Parse O/A/B with `JsonDocumentFormat`, migrating each to the current schema.
2. Merge members by stable member id, nodes by node id, pin states by `(node, pin)`, connections as
   a set (add/remove), and layout per node (on conflict take ours, never report a conflict).
3. Remap add/add id collisions (the gdmerge approach).
4. Validate the result: dangling connections, one incoming edge per data input, duplicate method
   signatures.
5. On a true semantic conflict (same property changed differently, or an edge added to a deleted
   node), fall back to `git merge-file` on the canonical text, so the user gets familiar conflict
   markers, and exit 1.

Limits:
- Drivers must be configured per clone. `.gitattributes` names the driver, but the driver command
  lives in git config, so NetPrints should offer `netprints git-install` like gdmerge.
- GitHub and GitLab server-side merges and PR "merge" buttons do not run custom drivers.
- The committed `.netpc.g.cs` conflicts separately. Resolve it by **regenerating**, never by hand.

Effort: about 1–2 weeks after P1, since DTOs, mapper and validation exist. Unity, Simulink and
gdmerge show the approach is standard.

**Diff drivers.** A `diff=netprints` with `textconv = netprints show --summary` could render
"added node CallMethod Console.WriteLine; connected n0.Exec→n2.Exec" for `git diff` and `git log -p`.
It is cheap, but web UIs ignore textconv. The review surface on GitHub is the canonical JSON **plus
the `.netpc.g.cs` diff**, which is already a clear textual projection of the logic. So keep the
`.g.cs` **visible** in PRs (do not mark it `linguist-generated`) and add a CI check that it is up to
date.

---

## 4. .NET tooling notes

- **System.Text.Json.** Source-gen supports polymorphism in metadata mode (not fast-path).
  - By default the discriminator (`$kind`) **must be the first property** when reading. Hand- or
    LLM-reordered files would fail unless `AllowOutOfOrderMetadataProperties` (.NET 9+) is enabled.
    That option buffers the whole object, which is fine for small documents
    ([polymorphism docs](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/polymorphism),
    [API](https://learn.microsoft.com/dotnet/api/system.text.json.jsonserializeroptions.allowoutofordermetadataproperties)).
    P1's `NodeListConverter` reads each element as a `JsonElement` first, so it can tolerate this
    either way.
  - .NET 9 added `IndentSize` / `NewLine` (used by P1) and **`JsonSchemaExporter`** with
    `TransformSchemaNode` for descriptions and customizations
    ([schema exporter](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/extract-schema)).
  - .NET 10 validates that property names do not collide with metadata names.
  - STJ can read comments and trailing commas (`ReadCommentHandling.Skip`, `AllowTrailingCommas`)
    but cannot write them.
  - Per-type inline formatting is not an STJ feature. Implement it with a small canonical writer:
    a `Utf8JsonWriter` pass over the `JsonNode` tree, or `WriteRawValue` in the converters for the
    inline record types. Verify in the spike how raw values interact with indentation.
- **RFC 8785 JCS** fixes key order (UTF-16 sort), number formatting and **no whitespace** ([RFC 8785](https://datatracker.ietf.org/doc/html/rfc8785)).
  It is excellent for hashing and equality and useless as a stored format, because the output is one
  line. Use it (for example via the [cyberphone reference implementations](https://github.com/cyberphone/json-canonicalization))
  only if NetPrints needs content hashes. Keep declared property order (`$kind` first) for the file.
- **YamlDotNet** is the mature de-facto YAML library. AOT needs `Vecc.YamlDotNet.Analyzers.StaticGenerator`
  with a `StaticContext`, and there are known gaps for struct types and unknown generic containers
  ([Andrew Lock](https://andrewlock.net/using-the-yamldotnet-source-generator-for-native-aot/),
  [#1009](https://github.com/aaubry/YamlDotNet/issues/1009)). YAML 1.1 implicit-typing pitfalls
  ("Norway problem", sexagesimal numbers) are well documented
  ([YAML document from hell](https://ruudvanasseldonk.com/2023/01/11/the-yaml-document-from-hell)).
- **TOML.** [Tomlyn](https://github.com/xoofx/Tomlyn) (v1+ targets TOML 1.1, with an STJ-style API,
  an in-package source generator, and NativeAOT support; [docs](https://xoofx.github.io/Tomlyn/docs/source-generation/)).
  It is solid, but TOML fits deep graphs poorly.
- **KDL.** [KdlSharp](https://github.com/AndreyAkinshin/KdlSharp) (v2, POCO serialization, schema)
  and [kdl-net](https://github.com/borland/kdl-net) (v1 only). Both are small ecosystems with no
  source-gen, no AOT story and little editor tooling ([KDL spec](https://kdl.dev/)).
- **JSON Schema in editors.** VS Code's built-in JSON language server and JetBrains IDEs (Rider)
  validate and complete from a `$schema` property, or from the [SchemaStore catalog](https://www.schemastore.org/)
  by `fileMatch` (`*.netpc.json`). Generate the schema from the DTOs with `JsonSchemaExporter`,
  commit it, and check it in CI against a golden file. The exporter's output for STJ polymorphism
  (discriminator `anyOf` / `const`) should be checked in the spike and fixed up in `TransformSchemaNode`
  where needed.

---

## 5. Recommendation for NetPrints

### 5.1 Format decision

- **Format:** JSON (`.netpc.json`), System.Text.Json source-gen DTOs, as P1 plans.
- **Custom DSL:** **not now; later only as an optional *projection*, not as the stored format;
  most likely never.** Rationale:
  - The logic already has a committed, reviewable text form (`.netpc.g.cs`).
  - A DSL costs a parser, printer, formatter, grammar, LSP and LLM prompt burden.
  - The VCS problems in every surveyed tool came from identity and line layout, which JSON can
    solve for far less effort.
  - Revisit if:
    - users demonstrably hand-author graphs, or
    - you want graphs *authored* as text, Enso-style. In that case, consider emitting and parsing a
      restricted C# subset rather than a new language.

### 5.2 Canonical writing rules (proposed v1, additions and changes to §1.1)

1. Encoding and whitespace:
   - UTF-8 without BOM.
   - LF line endings and a single final LF.
   - 2-space indent.
   - Ship `.gitattributes` with `*.netpc.json text eol=lf` and `*.netpc.g.cs text eol=lf` (Enso's
     CRLF lesson).
2. `$schema` is the **first** property. It holds the versioned schema URL (for example
   `https://schemas.netprints.dev/netpc/v1.json`, a placeholder), followed by `schemaVersion`.
   `schemaVersion` stays authoritative. `$schema` is informational, rewritten on save and ignored on read.
3. Properties use declared `[JsonPropertyOrder]` order, and `$kind` comes first in nodes. Do **not**
   apply JCS sorting to the file.
4. **Inline records.** These DTO types are always written on **one line**, regardless of length:
   `ConnectionDocument`, layout entries, `PinStateDocument`, `TypedValue`, `TypeRef`, `ParameterRef`
   and `LocalVariableDocument`. Also node objects that have only common fields (`$kind`, `id`,
   `name`). Everything else is block-indented, one property per line. The rule depends on the type,
   never on line width, so a value growing past N characters never reflows neighbouring lines.
5. **Ids.**
   - Node ids are random for new nodes: `n` plus 6 Crockford base32 lowercase characters
     (~30 bits), checked for uniqueness within the graph, with a seedable generator for tests.
   - Legacy-imported nodes keep deterministic `n0…nK`.
   - Every member (method, constructor, variable, event graph) gets an `id` of the same shape with
     prefix `m`.
   - Ids never change once assigned.
6. **Pin references** are `<dir>.<kind>.<key>`, where `key` is the pin's **constructor-assigned
   name** (for example `in.exec.Exec`, `in.data.value`, `out.exec.Catch`), not its index. If two pins
   in the same (dir, kind) share a name, use `name~2`, `name~3` for the later ones. Dynamic,
   positional pins (`Input0`, `Output0`) are named by position anyway, so they keep today's
   behaviour. The key is independent of user pin renames, which stay in `pins[].name`.
7. **Layout.**
   - A trailing top-level `layout`: map of graph key to a map of node id to `[x, y]`, both
     ordinal-sorted.
   - Graph keys are `class` or `<memberId>[/type|/get|/set]`, not indexes.
   - Coordinates are integers: snap or round on save, so no float churn.
   - Every entry is optional. A missing entry is auto-placed near its connected neighbours, not at
     (0,0).
   - Entries of deleted nodes are dropped on save.
8. **Connections** are sorted ordinally by `from`, then `to`, one line each. Store no derived data.
9. **Omit defaults.** Omit node `name` when it equals the kind's default name. Omit empty arrays and
   `None` enums (already in P1).
10. **Forbidden:** timestamps, absolute paths, machine names, counters (`lastNodeId`-style), content
    hashes, and editor view state (zoom, scroll, selection, collapsed state). View state belongs in
    per-user, git-ignored state (for example `.netprints/`), as in the "Forbidden" row of §1.1.
11. **Tolerant read, canonical write.**
    - Accept comments, trailing commas and out-of-order `$kind` on read. Also accept pin references
      in the old index form as a read-only alias, which helps LLM and hand edits.
    - Always write the canonical form.
    - `netprints format --check` gives CI a check that files are canonical.
12. **No ripple saves.** A graph file is written only if the user edited that class (dirty flag). A
    byte difference caused by re-resolving references against changed assemblies is not a reason to
    write (the LabVIEW lesson).

### 5.3 HelloWorld on disk (recommended)

`samples/HelloWorld/HelloWorld.Program.netpc.json`: logic, then layout in the same file.
Nodes `n0`–`n2` come from legacy import. A node added later in the editor would get an id like `n7hx3kq`.

```json
{
  "$schema": "https://schemas.netprints.dev/netpc/v1.json",
  "schemaVersion": 1,
  "namespace": "HelloWorld",
  "name": "Program",
  "visibility": "Public",
  "classGraph": {
    "nodes": [
      { "$kind": "classReturn", "id": "n0" }
    ]
  },
  "methods": [
    {
      "id": "m4kq7tz",
      "name": "Main",
      "visibility": "Public",
      "modifiers": "Static",
      "graph": {
        "nodes": [
          { "$kind": "methodEntry", "id": "n0" },
          { "$kind": "return", "id": "n1" },
          {
            "$kind": "callMethod",
            "id": "n2",
            "pins": [
              { "pin": "in.data.value", "value": { "type": "System.String", "value": "Hello, World!" } }
            ],
            "method": {
              "name": "WriteLine",
              "declaringType": { "name": "System.Console" },
              "parameters": [
                { "name": "value", "type": { "name": "System.String" } }
              ],
              "modifiers": "Static",
              "visibility": "Public"
            }
          }
        ],
        "connections": [
          { "from": "n0/out.exec.Exec", "to": "n2/in.exec.Exec" },
          { "from": "n2/out.exec.Exec", "to": "n1/in.exec.Exec" }
        ]
      }
    }
  ],
  "layout": {
    "class": {
      "n0": [112, 112]
    },
    "m4kq7tz": {
      "n0": [112, 112],
      "n1": [840, 112],
      "n2": [420, 112]
    }
  }
}
```

What the review diffs look like:
- **Moving `n2`** is a one-line change inside `layout`: `-      "n2": [420, 112]` /
  `+      "n2": [460, 140]`.
- **Adding a node** adds one node block, one line per new connection, and one layout line. Two
  branches adding different nodes get different ids, and their connection and layout lines scatter
  through the sorted sets. The remaining textual hotspot is the append at the end of `nodes`, which
  the merge driver resolves.

`.gitattributes` (emitted by `ProjectConverter` / new-project templates):

```gitattributes
*.netpc.json   text eol=lf
*.netpc.g.cs   text eol=lf
# later, once `netprints git-install` exists:
# *.netpc.json merge=netprints diff=netprints
```

The generated file `HelloWorld.Program.netpc.g.cs` is committed and deliberately left visible in PR
diffs. CI runs `netprints regen --check`, failing if it is stale. On a merge conflict, it is
regenerated rather than hand-merged.

### 5.4 Roadmap

| When | Item |
|---|---|
| P1 (spec change, before any v1 files ship) | Rules 1–12 above; JSON Schema generation and publication; `.gitattributes` template |
| P1.x / P2 | `netprints format --check`, `regen --check`; SchemaStore registration; tolerant reader |
| P2 | `netprints merge` driver and `git-install`; `textconv` summary diff; in-editor visual diff (Node-RED / Blueprint style) |
| Later | Translator order independent of storage order, then `nodes` sorted by id; optional compact member refs (C# XML doc-comment IDs, for example `M:System.Console.WriteLine(System.String)`) |
| Never / on demand | Custom DSL as the stored format; YAML, TOML or KDL formats (`IDocumentFormat` keeps the door open) |

---

## 6. Suggested P1 spec and contract changes (precise list; not applied)

`contracts/document-format.md`:
1. **§1.1** Add rows:
   - `$schema` is the first property (informational).
   - The **inline-record** rule, with its exact type list (§5.2 item 4).
   - Integer layout coordinates.
   - "No counters, hashes, or editor view state".
   - `.gitattributes` `eol=lf`.
   Also add a note that the canonical writer is a custom pass, because STJ `WriteIndented` alone
   cannot produce inline records.
2. **§1.4**:
   - Add `id` (string, required) as the first field of `MethodDocument`, `ConstructorDocument`,
     `VariableDocument` and `EventGraphDocument`.
   - Change graph keys from `methods/<i>`, `constructors/<i>`, `eventGraphs/<i>` and
     `variables/<i>/…` to `<memberId>`, `<memberId>/type|get|set` (`class` unchanged).
   - Make `layout` Req. = no, with per-node entries optional.
3. **§1.4, pin reference.** Change `index` to `key` = constructor-assigned pin name, with `~n`
   disambiguation. Define that the old index form is accepted on read as an alias (or reject it; pick
   one explicitly).
4. **§1.5.** Common field `name`: "omit when equal to the kind's default name".
5. **§1.7.** Replace the example with the §5.3 version, and drop the "whitespace is compacted here"
   caveat, since the canonical output would now look like the example.
6. **§2.3.**
   - Reader options: `AllowTrailingCommas = true`, `ReadCommentHandling = Skip`, and out-of-order
     `$kind` handling in `NodeListConverter`.
   - State that comments are not preserved on write.
7. **§2.4.**
   - Add `string Id` to the member document records.
   - Change the layout value type from `double[]` to `int[2]` (or keep `double` and round, but state
     which).
8. **§2.6, `FromDocument`.** "Missing position → auto-place" instead of `(0,0)`. Ids of unknown
   members are preserved.
9. **§2.8, `SaveAsync`.** Write only classes whose model is dirty (user-edited), not every class
   whose bytes differ. Define what happens when re-resolved references change bytes of a clean class:
   recommended, do not write, and surface an info issue.
10. **§1.5 / §2.6, unknown nodes.** "Raw JSON kept and rewritten verbatim" conflicts with the
    canonical rules when a hand edit is not canonical. Change it to "content preserved, re-emitted
    through the canonical writer". DF-T08 stays byte-identical for canonical input.
11. **New §7, "Schema publication".**
    - Generate `schemas/netpc.v1.schema.json` via `JsonSchemaExporter` + `TransformSchemaNode` from
      the source-gen context.
    - Commit it and test it against a golden file.
    - Plan SchemaStore registration (`fileMatch: ["*.netpc.json"]`).
12. **§6, tests.**
    - DF-T04: extend with inline records and integer coordinates.
    - DF-T05: one-line diff per moved node.
    - DF-T18: random ids for new nodes with a seeded generator; legacy import still `n0…`.
    - New tests:
      - Inserting a parameter into a called method's signature does not re-target other connections.
      - Reordering methods keeps each method's layout.
      - Two simulated branches adding one node each merge by plain `git merge-file` with conflicts
        only at the `nodes` array tail.
      - The schema golden file is up to date.

`data-model.md` §2:
13. `AllocateNodeId()`: change from "`n` + (max k + 1)" to a random id from an injectable, seedable
    `INodeIdGenerator`, checked for uniqueness within the graph. Add member ids
    (`MethodGraph.Id` etc., or on the owning member types).
14. `GraphKeys.For` / `Resolve`: key by member id, not index.
15. `EventGraph.Entries` "in node order" determines emitted method order. Keep it for P1, but record
    that translation order depending on storage order is what blocks sorting `nodes` by id later.

`contracts/project-system.md` (conversion and templates):
16. `ProjectConverter` and new-project creation write a `.gitattributes`: `*.netpc.json text eol=lf`,
    `*.netpc.g.cs text eol=lf`. Only write it if absent; otherwise append the missing lines.

---

## 7. Risks

- **Random ids versus determinism.** "Same document → same bytes" still holds, because ids are
  document data, but test fixtures need a seeded generator. Legacy import must stay deterministic
  (`n0…`) for the DF-T01/T02 goldens.
- **Pin keys by name.** Constructor pin names must be stable across NetPrints versions. Renaming a
  built-in pin becomes a schema migration. Generic and overload changes of a called method can still
  legitimately invalidate connections; drop them with `NPD002` as today.
- **Custom canonical writer.** Custom code in the write path could produce invalid JSON or
  nondeterministic output. Mitigate by always re-parsing the written bytes in tests (DF-T03/T04), and
  by property-based round-trip tests.
- **Node order stays semantic.** Appends to `nodes` on two branches still conflict textually until
  translation order is decoupled from storage order.
- **Merge driver adoption.** It needs per-clone install and is not used by web merges. Teams that
  skip it get plain-git behaviour, which is why the text format itself must stay merge-friendly.
- **`.g.cs` drift.** Committed generated code can go stale or be hand-merged incorrectly. CI
  `regen --check` is mandatory, not optional.
- **Inline `MethodRef`.** Full signatures inline are self-contained and LLM-readable, but verbose. If
  saves refresh them from reflection, they cause ripple rewrites across files (the LabVIEW lesson).
  Rule 12 addresses this.
- **LLM edits.** Models may emit non-canonical but valid JSON, reorder `$kind`, or invent pins. The
  tolerant reader, schema validation with line/position errors, and canonical rewrite cover this.
  Invented pins surface as `NPD002`.
- **Schema exporter gaps.** How `JsonSchemaExporter` handles polymorphism and extension kinds from
  separate `JsonSerializerContext`s was not verified. Extension kinds may need `additionalProperties`
  handling or per-extension schema fragments.
- **Evidence limits.** The LLM format benchmark uses small models and comprehension tasks only. Web
  merge behaviour with `merge=union` was not verified. The Godot 4 id scheme and gdmerge claims come
  from docs and READMEs, not from testing.

## Sources (main)

Godot [TSCN docs](https://docs.godotengine.org/en/stable/engine_details/file_formats/tscn.html), [#25416](https://github.com/godotengine/godot/issues/25416), [gdmerge](https://github.com/hyprtuna/gdmerge) ·
Unity [Smart Merge](https://docs.unity3d.com/6000.3/Documentation/Manual/SmartMerge.html), [text format](https://docs.unity3d.com/Manual/FormatDescription.html), [Shader Graph format PR](https://github.com/Unity-Technologies/Graphics/pull/222) ·
Unreal [OFPA](https://dev.epicgames.com/documentation/en-us/unreal-engine/one-file-per-actor-in-unreal-engine), [Blueprint diff via git](https://gist.github.com/Panakotta00/c90d1017b89b4853e8b97d13501b2e62), [MergeAssist](https://github.com/KennethBuijssen/MergeAssist), [utxt](https://github.com/elliotttate/unreal-utxt-converter) ·
Blender [GNToolkit](https://github.com/oobma/GNToolkit), [NodeKit](https://github.com/j10er/NodeKit) ·
Node-RED [#2515](https://github.com/node-red/node-red/issues/2515), [#2085](https://github.com/node-red/node-red/issues/2085), [Projects](https://nodered.org/docs/user-guide/projects/) ·
n8n [push/pull](https://docs.n8n.io/source-control-environments/using/push-pull/) · ComfyUI [workflow spec](https://docs.comfy.org/specs/workflow_json) ·
Dynamo [#7747](https://github.com/DynamoDS/Dynamo/issues/7747) · Grasshopper [McNeel forum](https://discourse.mcneel.com/t/version-control-for-gh/128645) ·
Enso [ensoFile.ts](https://github.com/enso-org/enso/blob/develop/app/ydoc-shared/src/ensoFile.ts), [#9257](https://github.com/enso-org/enso/issues/9257), [#11304](https://github.com/enso-org/enso/issues/11304), [#7994](https://github.com/enso-org/enso/issues/7994) ·
LabVIEW [LVCompare/LVMerge setup](https://blog.sasworkshops.com/setting-up-lvcompare-and-lvmerge/), [separate compiled code](https://hampel-soft.com/blog/separate-compiled-code-from-source/) ·
Simulink [automerge](https://www.mathworks.com/help/simulink/ug/enable-matlab-automerge.html) · Scratch [file format](https://en.scratch-wiki.info/wiki/Scratch_File_Format) · Pd [forum](https://forum.pdpatchrepo.info/topic/14102/version-control-for-patches) ·
Figma [multiplayer](https://www.figma.com/blog/how-figmas-multiplayer-technology-works/) · Houdini [text files](https://www.sidefx.com/docs/houdini/assets/textfiles.html) · Rive [.riv format](https://rive.app/docs/runtimes/advanced-topic/format) ·
git [gitattributes](https://git-scm.com/docs/gitattributes) · [RFC 8785](https://datatracker.ietf.org/doc/html/rfc8785) ·
STJ [polymorphism](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/polymorphism), [schema exporter](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/extract-schema) ·
[YamlDotNet AOT](https://andrewlock.net/using-the-yamldotnet-source-generator-for-native-aot/) · [Tomlyn](https://xoofx.github.io/Tomlyn/docs/source-generation/) · [KdlSharp](https://github.com/AndreyAkinshin/KdlSharp) ·
[YAML from hell](https://ruudvanasseldonk.com/2023/01/11/the-yaml-document-from-hell) · [LLM nested-format benchmark](https://www.improvingagents.com/blog/best-nested-data-format/) · [SchemaStore](https://www.schemastore.org/)
