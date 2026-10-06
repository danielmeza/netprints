# Projects

A NetPrints project is an ordinary SDK-style `.csproj`. There is no separate project format and
nothing to convert: any tool that understands MSBuild — `dotnet build`, Visual Studio, Rider, VS
Code — understands a NetPrints project too.

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>MyNamespace</RootNamespace>
    <NetPrintsProfile>netprints.default</NetPrintsProfile>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="NetPrints.Sdk" Version="<version>" PrivateAssets="all" />
  </ItemGroup>

</Project>
```

The `NetPrints.Sdk` package is the only thing that makes it a NetPrints project: it contributes the
`NetPrintsGraph` item (every `*.netpc.json` file under the project by default) and a build target
that turns each graph into C#. Nothing else about the project file is special — references,
package references, target framework and output type all work exactly as they do in any other .NET
project.

## The start page

With no project open the document area shows the **Start** page (**Help › Start page** shows it again at any time):

- **Recent projects**: the projects you opened or created, most recent first, with pinned projects at the top and at
  most 20 unpinned ones. A row opens the project; the pin button keeps it at the top; the remove button forgets it
  (the project's files are never touched). The search box filters by name or path, and a project whose file is gone
  stays in the list marked "(unavailable)" until you remove it.
- **Open folder or project…** opens a `.csproj`, or a folder that holds exactly one. A start-up argument that is not
  one of those leaves the start page with a message naming the path.
- **New project…** asks for a template, a project name (also its namespace) and an empty or new folder, checks them
  before writing anything, creates the project and opens it. **Console app** creates a project with a `Program`
  class graph that has an empty `public static void Main()`, so it builds and runs at once; **Class library**
  creates a project with no classes yet. If creating fails, the folder it created is removed and the dialog shows why.
- **Samples** copies a bundled sample into a folder you choose (inside a folder named after the sample) and opens the
  copy; the bundled files are never changed. A built copy needs the `NetPrints.Sdk` package version its project file
  names.
- **What's new** shows the notes of the running version and a link to the release notes on GitHub.

Opening or creating a project closes the start page; closing the project brings it back.

## Restored layout and sessions

The editor remembers, per user, the window position, size and maximized state, the panel layout (sizes, docking,
floating panes) and the recent list, in versioned JSON files in your application-data folder, never in the project.
It also remembers, per project, the open graph tabs in order, the active tab and each graph's zoom and position, and
restores them when the project opens. A graph or panel that no longer exists is skipped, a window that would be off
every screen is moved to the centre of the primary one, and a state file that is unreadable or from a newer version is
ignored with a log entry: the editor starts with the defaults. **View › Reset layout** restores the default layout.

## The editor window

The editor opens one project per window, from **File › Open folder or project…** or as the first argument
on the command line. There is no launcher and no separate window per class. The window has:

- a **menu bar** (File, Edit, View, Go, Build, Help) and a **command bar** with the common commands (save, compile,
  run, undo, redo, class settings, references);
- a **project tree** with the project, its classes, and per class its methods, constructors, variables and event
  graphs; double-click or press Enter to open one, and use the context menu to add, rename or remove members;
- a **document area** with one tab per open graph (close with the button, middle-click or Ctrl+W; Ctrl+Tab and
  Ctrl+Shift+Tab switch tabs) and the project settings;
- an **inspector** for the selected class, method, constructor or variable, with the **Variables** panel beside it
  for a class's member variables and the active method's local variables;
- a **bottom panel** with Errors (activate an entry to open its graph and select the node), Output (build and
  program output) and C# (the generated code of the active class);
- a status bar.

Panes dock to any side, tab together and float into their own window; a graph tab can float and keep full editing
there. Closing a pane hides it, and the View menu lists every panel to show it again; **View › Reset layout**
restores the default. Opening or creating another project unloads the current one first.

## Generated code (`.netpc.g.cs`)

Building the project runs the NetPrints generator before compilation. For each graph file
`Foo.netpc.json` it writes `Foo.netpc.g.cs` next to it, and the project's normal `Compile` items
pick it up. This file is:

- **committed.** It is checked into source control like any other generated-but-reviewed file (the
  pattern .NET already uses for `.g.cs` from source generators you want to see in review).
- **regenerated, never hand-merged.** If you edit it directly, the next build overwrites your
  changes. If a merge conflicts on it, resolve the conflict in the `.netpc.json` graph (or just take
  either side) and rebuild — do not try to hand-merge the generated C#. When merging graphs with git's
  merge driver (see [Git workflow](git.md#merging-graphs)), the driver merges only the `.netpc.json`
  graph file; git then merges the generated `.netpc.g.cs` as text, which conflicts when both sides
  changed the graph.
- **kept visible in PRs.** It is not marked `linguist-generated`, so GitHub shows it in diffs. This
  is deliberate: the generated C# is often the easiest way to review what a graph change actually
  does, and a reviewer should not have to open the editor to see it.

A build only regenerates a `.netpc.g.cs` file when its graph (or the generator, or the project file,
or a referenced extension's manifest) actually changed; an up-to-date build does nothing (MSBuild
reports the generate target as skipped).

To check if generated code is current without building, use:

```bash
netprints generate --check
# or the alias
netprints regen --check
```

This exits 0 if all generated files are up to date, or 1 if any are stale or missing. Use this in CI
to ensure that graphs have not drifted from their generated files (see [Git workflow](git.md#keeping-generated-code-current)).

## Editing outside the editor

Because the graph is text and the generated code is committed, a lot of a NetPrints project can be
read and reasoned about without opening the editor: the diff of a `.netpc.json` file, or the diff of
its `.netpc.g.cs`, both show a plain-text review. See [Graph file format](graph-format.md) for what
those diffs look like.

VS Code has no built-in awareness of the pair of files, but its generic file-nesting feature groups
them in the Explorer. Add this to `.vscode/settings.json`:

```json
{
  "explorer.fileNesting.enabled": true,
  "explorer.fileNesting.patterns": { "*.netpc.json": "${capture}.netpc.g.cs" }
}
```

With this, `Foo.netpc.g.cs` nests under `Foo.netpc.json` in the file tree instead of appearing as a
separate sibling file.

## SDK requirement

Building a NetPrints project needs the .NET 10 SDK (the generator and the editor both target
`net10.0`); running a project's compiled output only needs the matching .NET runtime. Visual Studio
2022, Visual Studio 2026 and Rider all build NetPrints projects the same way `dotnet build` does,
since generation is a plain MSBuild target that shells out to the generator — there is no
IDE-specific integration to install.
