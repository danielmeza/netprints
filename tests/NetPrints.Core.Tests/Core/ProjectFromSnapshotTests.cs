using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NetPrints.Core;
using NetPrints.Projects;
using Xunit;

namespace NetPrints.Tests.Core
{
    /// <summary>
    /// <see cref="Project"/>'s snapshot-based model (data-model.md §5, T055): <see cref="Project.FromSnapshot"/>,
    /// <see cref="Project.Snapshot"/>, <see cref="Project.GetGraphFilePath"/>,
    /// <see cref="Project.CreateNewClass(IProjectProfile)"/> and <see cref="Project.LastDiagnostics"/>,
    /// added next to the old members (which stay until T063).
    /// </summary>
    public class ProjectFromSnapshotTests
    {
        private static ProjectSnapshot NewSnapshot(string projectFilePath = "/repo/App/App.csproj", string rootNamespace = "App") =>
            new(
                ProjectFilePath: projectFilePath,
                Name: "App",
                RootNamespace: rootNamespace,
                AssemblyName: "App",
                OutputType: BinaryType.Executable,
                TargetFramework: "net10.0",
                ProfileId: DefaultProjectProfile.ProfileId,
                ReferencesNetPrintsSdk: true,
                GraphFiles: [],
                ExtensionFolders: [],
                References: [],
                DeclaredReferences: [],
                OtherSources: [],
                CompilationOptionsJson: "{}",
                Properties: new Dictionary<string, string>(),
                Messages: []);

        [Fact]
        public void FromSnapshotSetsDerivedMembersAndStartsWithNoClasses()
        {
            ProjectSnapshot snapshot = NewSnapshot();

            Project project = Project.FromSnapshot(snapshot);

            Assert.Same(snapshot, project.Snapshot);
            Assert.Equal("App", project.Name);
            Assert.Equal("App", project.DefaultNamespace);
            Assert.Equal(BinaryType.Executable, project.OutputBinaryType);
            Assert.Equal("/repo/App/App.csproj", project.Path);
            Assert.Equal("net10.0", project.TargetFramework);
            Assert.Equal(DefaultProjectProfile.ProfileId, project.ProfileId);
            Assert.Empty(project.Classes);
            Assert.Empty(project.LastDiagnostics);
        }

        /// <summary>R1-15: <see cref="Project.Snapshot"/> is non-nullable (the only constructor path,
        /// <see cref="Project.FromSnapshot"/>, requires one), so a replacement snapshot's own
        /// <see cref="Project.TargetFramework"/>/<see cref="Project.ProfileId"/> take effect immediately;
        /// there is no snapshot-less state left to throw for.</summary>
        [Fact]
        public void ReplacingTheSnapshotUpdatesTargetFrameworkAndProfileId()
        {
            Project project = Project.FromSnapshot(NewSnapshot());
            ProjectSnapshot replacement = NewSnapshot() with { TargetFramework = "net9.0", ProfileId = "other.profile" };

            project.Snapshot = replacement;

            Assert.Same(replacement, project.Snapshot);
            Assert.Equal("net9.0", project.TargetFramework);
            Assert.Equal("other.profile", project.ProfileId);
        }

        [Fact]
        public void GetGraphFilePathDefaultsUnderTheProjectDirectoryAndKeepsALoadedPath()
        {
            Project project = Project.FromSnapshot(NewSnapshot(projectFilePath: Path.Combine("repo", "App", "App.csproj")));
            var cls = new ClassGraph { Name = "Program", Namespace = "App", Project = project };

            Assert.Equal(Path.Combine("repo", "App", $"{cls.FullName}.netpc.json"), project.GetGraphFilePath(cls));

            cls.LoadedGraphFilePath = Path.Combine("repo", "App", "elsewhere", "Program.netpc.json");
            Assert.Equal(cls.LoadedGraphFilePath, project.GetGraphFilePath(cls));
        }

        [Fact]
        public void CreateNewClassIsUniqueDirtyAndInTheDefaultNamespace()
        {
            Project project = Project.FromSnapshot(NewSnapshot());

            ClassGraph first = project.CreateNewClass(DefaultProjectProfile.Instance);
            ClassGraph second = project.CreateNewClass(DefaultProjectProfile.Instance);

            Assert.Equal("MyClass", first.Name);
            Assert.Equal("MyClass2", second.Name);
            Assert.Equal("App", first.Namespace);
            Assert.True(first.IsDirty);
            Assert.True(second.IsDirty);
            Assert.Equal(new[] { first, second }, project.Classes);
        }
    }
}
