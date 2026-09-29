#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using NetPrints.Compilation;
using NetPrints.Projects;

namespace NetPrints.Core
{
    /// <summary>
    /// The kind of binary a project compiles to.
    /// </summary>
    public enum BinaryType
    {
        /// <summary>
        /// A library with no entry point (a .dll with no runnable command).
        /// </summary>
        SharedLibrary,

        /// <summary>
        /// A runnable executable (see <c>IProjectSystem.GetRunCommand</c>).
        /// </summary>
        Executable,
    }

    /// <summary>
    /// Project model.
    /// </summary>
    public partial class Project : ModelObject
    {
        /// <summary>
        /// Classes contained in this project.
        /// </summary>
        public ObservableRangeCollection<ClassGraph> Classes { get; } = new ObservableRangeCollection<ClassGraph>();

        /// <summary>
        /// Name of the project.
        /// </summary>
        [ObservableProperty]
        public partial string Name { get; set; }

        /// <summary>
        /// Path to the last successfully compiled assembly. Null before the first successful
        /// compilation, or when the last compilation failed.
        /// </summary>
        [ObservableProperty]
        public partial string? LastCompiledAssemblyPath { get; set; }

        /// <summary>
        /// Path to the project file. Set from <see cref="ProjectSnapshot.ProjectFilePath"/> by
        /// <see cref="FromSnapshot"/>; not itself part of the persisted project data.
        /// </summary>
        [ObservableProperty]
        public partial string Path { get; set; }

        /// <summary>
        /// Default namespace of newly created classes.
        /// </summary>
        [ObservableProperty]
        public partial string DefaultNamespace { get; set; }

        /// <summary>
        /// Type of the binary that we want to output.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanCompileAndRun))]
        public partial BinaryType OutputBinaryType { get; set; }

        private Project()
        {
        }

        #region Snapshot-based model (data-model.md §5, T055)

        /// <summary>
        /// The project's most recently loaded or applied snapshot (project-system.md §4,
        /// <c>IProjectSystem.LoadAsync</c>/<c>ApplyAsync</c>): always set, since the only constructor
        /// path (<see cref="FromSnapshot"/>) requires one. The setter exists only for the editor to
        /// replace it with a newer snapshot (e.g. <c>MainEditorVM</c>, <c>ReferenceListVM</c>).
        /// <see cref="TargetFramework"/> and <see cref="ProfileId"/> are derived from it and re-raise
        /// their own change notification whenever it is replaced.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TargetFramework))]
        [NotifyPropertyChangedFor(nameof(ProfileId))]
        public partial ProjectSnapshot Snapshot { get; set; }

        /// <summary>Target framework moniker of <see cref="Snapshot"/> (e.g. <c>"net10.0"</c>).</summary>
        public string TargetFramework => Snapshot.TargetFramework;

        /// <summary>Reverse-DNS id of <see cref="Snapshot"/>'s <c>NetPrintsProfile</c>.</summary>
        public string ProfileId => Snapshot.ProfileId;

        /// <summary>
        /// Diagnostics from the project's last build (project-system.md §4,
        /// <c>IProjectSystem.BuildAsync</c>'s <c>BuildResult.Messages</c> mapped to
        /// <see cref="CodeDiagnostic"/>). Empty on success or before the first build.
        /// </summary>
        [ObservableProperty]
        public partial ObservableRangeCollection<CodeDiagnostic> LastDiagnostics { get; set; } = new ObservableRangeCollection<CodeDiagnostic>();

        /// <summary>
        /// Creates a project from <paramref name="snapshot"/> (project-system.md §4):
        /// <see cref="Name"/>, <see cref="DefaultNamespace"/> (<see cref="ProjectSnapshot.RootNamespace"/>),
        /// <see cref="OutputBinaryType"/> and <see cref="Path"/>
        /// (<see cref="ProjectSnapshot.ProjectFilePath"/>) are set from it. <see cref="Classes"/> starts
        /// empty; the caller adds classes loaded through <c>ProjectPersistence</c> (T056).
        /// </summary>
        /// <param name="snapshot">Snapshot to build the project from.</param>
        /// <returns>The new project.</returns>
        public static Project FromSnapshot(ProjectSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            return new Project
            {
                Snapshot = snapshot,
                Name = snapshot.Name,
                DefaultNamespace = snapshot.RootNamespace,
                OutputBinaryType = snapshot.OutputType,
                Path = snapshot.ProjectFilePath,
            };
        }

        /// <summary>
        /// Returns the file <paramref name="cls"/> is (or would be) saved to (data-model.md §5): the
        /// path it was loaded from (<see cref="ClassGraph.LoadedGraphFilePath"/>), or, for a class
        /// created in memory and never yet saved,
        /// <c>&lt;project directory&gt;/&lt;cls.FullName&gt;.netpc.json</c>.
        /// </summary>
        /// <param name="cls">Class to get the graph file path for.</param>
        /// <returns>The class's graph file path.</returns>
        public string GetGraphFilePath(ClassGraph cls)
        {
            ArgumentNullException.ThrowIfNull(cls);
            return cls.LoadedGraphFilePath ?? System.IO.Path.Combine(GetProjectDirectory(Path), $"{cls.FullName}.netpc.json");
        }

        /// <summary>
        /// Adds and returns a new, empty class built from <paramref name="profile"/>'s first class
        /// template (extension-points.md §5), named uniquely ("MyClass", "MyClass2", … — PAR-12) in
        /// <see cref="DefaultNamespace"/>. Starts dirty (<see cref="ClassGraph.IsDirty"/>): nothing has
        /// been saved for it yet.
        /// </summary>
        /// <param name="profile">Profile whose first class template builds the new class.</param>
        /// <returns>The newly created class.</returns>
        public ClassGraph CreateNewClass(IProjectProfile profile)
        {
            ArgumentNullException.ThrowIfNull(profile);

            string qualifiedName = NetPrintsUtil.GetUniqueName($"{DefaultNamespace}.MyClass", Classes.Select(c => c.FullName).ToList());
            string name = qualifiedName.Split('.').Last();

            ClassGraph cls = profile.ClassTemplates[0].Create(this, name);
            cls.MarkDirty();
            Classes.Add(cls);

            return cls;
        }

        #endregion

        /// <summary>
        /// Directory of a project file. Throws if <paramref name="projectPath"/> has no directory
        /// (a root or relative path) instead of returning a value from a null-directory silently.
        /// </summary>
        private static string GetProjectDirectory(string projectPath) =>
            System.IO.Path.GetDirectoryName(projectPath)
                ?? throw new InvalidOperationException($"Project path '{projectPath}' has no directory.");

        /// <summary>
        /// Whether the project can currently be compiled and run: not already compiling, and output
        /// type is <see cref="BinaryType.Executable"/>.
        /// </summary>
        public bool CanCompileAndRun
        {
            get => CanCompile && OutputBinaryType == BinaryType.Executable;
        }

        /// <summary>
        /// Whether the project can currently be compiled: not already compiling (see <see cref="IsCompiling"/>).
        /// </summary>
        public bool CanCompile
        {
            get => !IsCompiling;
        }

        /// <summary>
        /// Human-readable status shown while and after compiling (eg. "Ready", "Compiling...",
        /// "Build succeeded", "Build failed with N error(s)").
        /// </summary>
        [ObservableProperty]
        public partial string CompilationMessage { get; set; } = "Ready";

        /// <summary>
        /// Whether a build is currently running.
        /// </summary>
        [ObservableProperty]
        public partial bool IsCompiling { get; set; }

        /// <summary>
        /// Whether the last build succeeded.
        /// </summary>
        [ObservableProperty]
        public partial bool LastCompilationSucceeded { get; set; }
    }
}
