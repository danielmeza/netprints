using System;
using System.Collections.Generic;
using System.IO;
using NetPrints.Core;
using NetPrints.Projects;

namespace NetPrints.Tests
{
    /// <summary>Builds in-memory <see cref="Project"/>s for tests, backed by a synthetic <see cref="ProjectSnapshot"/>.</summary>
    internal static class TestProjects
    {
        /// <summary>
        /// Creates an empty project (no classes) whose <see cref="Project.Path"/> is
        /// <paramref name="projectPath"/>, or a <c>.csproj</c> under the temp directory when omitted.
        /// </summary>
        public static Project Create(string name, string rootNamespace, string? projectPath = null, BinaryType outputType = BinaryType.SharedLibrary) =>
            Project.FromSnapshot(new ProjectSnapshot(
                projectPath ?? Path.Combine(Path.GetTempPath(), $"{name}.csproj"), name, rootNamespace, name, outputType,
                "net10.0", DefaultProjectProfile.ProfileId, ReferencesNetPrintsSdk: true,
                GraphFiles: [], ExtensionFolders: [], References: [], DeclaredReferences: [],
                OtherSources: [], CompilationOptionsJson: "{}", Properties: new Dictionary<string, string>(), Messages: []));
    }
}
