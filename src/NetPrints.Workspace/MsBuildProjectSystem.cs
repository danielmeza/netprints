#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Projects;
using MSBuildProject = Microsoft.Build.Evaluation.Project;
using RoslynProject = Microsoft.CodeAnalysis.Project;

namespace NetPrints.Workspace;

/// <summary>
/// <see cref="IProjectSystem"/> backed by real MSBuild evaluation, <c>MSBuildWorkspace</c> and
/// out-of-process <c>dotnet</c> invocations (project-system.md §4). Requires
/// <see cref="MsBuildRegistration.EnsureRegistered"/> to have found (or itself finds) an MSBuild
/// instance; every member throws <see cref="ProjectSystemException"/> with
/// <see cref="ProjectSystemException.NoSdkRegistered"/> if none is available.
/// </summary>
public sealed class MsBuildProjectSystem : IProjectSystem
{
    private const string SourceDirectoryGlobSuffix = "/**/*.cs";
    private static readonly string[] DirectoryBuildFileNames = ["Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props"];

    private readonly ProjectSystemOptions options;
    private readonly IProcessRunner processes;
    private readonly ILogger<MsBuildProjectSystem> logger;

    /// <summary>
    /// Creates a project system.
    /// </summary>
    /// <param name="options">Options this instance is configured with.</param>
    /// <param name="processes">Runner used for every out-of-process invocation (restore, build,
    /// <see cref="GetRunCommand"/>).</param>
    /// <param name="logger">Logger events 4001–4004 are written to.</param>
    public MsBuildProjectSystem(ProjectSystemOptions options, IProcessRunner processes, ILogger<MsBuildProjectSystem> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(processes);
        ArgumentNullException.ThrowIfNull(logger);

        this.options = options;
        this.processes = processes;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ProjectSnapshot> LoadAsync(string projectFilePath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectFilePath);
        EnsureRegisteredOrThrow();

        var messages = new List<ProjectMessage>();
        ProjectMessage? restoreFailure = await RestoreIfNeededAsync(projectFilePath, cancellationToken).ConfigureAwait(false);
        if (restoreFailure is not null)
        {
            messages.Add(restoreFailure);
        }

        (MSBuildProject evaluated, ProjectCollection collection, string? retargetedFramework) = Evaluate(projectFilePath);
        try
        {
            if (retargetedFramework is not null)
            {
                messages.Add(new ProjectMessage(ProjectMessageSeverity.Info, ProjectMessage.MultiTargetingUsesFirstFramework,
                    $"Multiple target frameworks are declared; the editor uses '{retargetedFramework}'.", projectFilePath, null, null));
            }

            var workspaceProperties = new Dictionary<string, string>();
            if (retargetedFramework is not null)
            {
                workspaceProperties["TargetFramework"] = retargetedFramework;
            }

            using MSBuildWorkspace workspace = MSBuildWorkspace.Create(workspaceProperties);
            // A ProjectReference resolves to its built output assembly (a flat ResolvedAssembly, like any
            // other reference) rather than becoming a second open project in the workspace: ProjectSnapshot
            // has no concept of a multi-project solution (project-system.md §4).
            workspace.LoadMetadataForReferencedProjects = true;
            RoslynProject roslynProject = await workspace.OpenProjectAsync(projectFilePath, cancellationToken: cancellationToken).ConfigureAwait(false);

            foreach (WorkspaceDiagnostic diagnostic in workspace.Diagnostics)
            {
                messages.Add(new ProjectMessage(ProjectMessageSeverity.Warning, ProjectMessage.WorkspaceDiagnostic,
                    diagnostic.Message, projectFilePath, null, null));
            }

            string outputType = evaluated.GetPropertyValue("OutputType");
            BinaryType binaryType = string.Equals(outputType, "Exe", StringComparison.OrdinalIgnoreCase)
                ? BinaryType.Executable
                : BinaryType.SharedLibrary;

            string profileId = evaluated.GetPropertyValue("NetPrintsProfile");
            if (string.IsNullOrEmpty(profileId))
            {
                profileId = DefaultProjectProfile.ProfileId;
            }

            var extraProperties = new Dictionary<string, string>();
            foreach (string propertyName in this.options.ExtraProperties)
            {
                extraProperties[propertyName] = evaluated.GetPropertyValue(propertyName);
            }

            var snapshot = new ProjectSnapshot(
                ProjectFilePath: projectFilePath,
                Name: evaluated.GetPropertyValue("MSBuildProjectName"),
                RootNamespace: evaluated.GetPropertyValue("RootNamespace"),
                AssemblyName: evaluated.GetPropertyValue("AssemblyName"),
                OutputType: binaryType,
                TargetFramework: retargetedFramework ?? evaluated.GetPropertyValue("TargetFramework"),
                ProfileId: profileId,
                ReferencesNetPrintsSdk: evaluated.GetItems("PackageReference")
                    .Any(item => string.Equals(item.EvaluatedInclude, "NetPrints.Sdk", StringComparison.OrdinalIgnoreCase)),
                GraphFiles: evaluated.GetItems("NetPrintsGraph")
                    .Select(item => item.GetMetadataValue("FullPath"))
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToList(),
                ExtensionFolders: evaluated.GetItems("NetPrintsExtension")
                    .Select(item => item.GetMetadataValue("FullPath"))
                    .ToList(),
                References: BuildResolvedAssemblies(roslynProject),
                DeclaredReferences: BuildDeclaredReferences(evaluated.Xml),
                OtherSources: await BuildOtherSourcesAsync(roslynProject, cancellationToken).ConfigureAwait(false),
                CompilationOptionsJson: BuildCompilationOptionsJson(roslynProject, evaluated),
                Properties: extraProperties,
                Messages: messages);

            Log.Loaded(logger, projectFilePath);
            return snapshot;
        }
        finally
        {
            collection.Dispose();
        }
    }

    /// <inheritdoc/>
    public async Task<ProjectSnapshot> ApplyAsync(string projectFilePath, IReadOnlyList<ProjectEdit> edits, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectFilePath);
        ArgumentNullException.ThrowIfNull(edits);
        EnsureRegisteredOrThrow();

        string projectDirectory = GetDirectoryOrThrow(projectFilePath);

        using (var collection = new ProjectCollection())
        {
            ProjectRootElement root = ProjectRootElement.Open(projectFilePath, collection, preserveFormatting: true);
            foreach (ProjectEdit edit in edits)
            {
                ApplyEdit(root, edit, projectDirectory);
            }

            string tempPath = $"{projectFilePath}.tmp-{Guid.NewGuid():N}";
            try
            {
                root.Save(tempPath);
                File.Move(tempPath, projectFilePath, overwrite: true);
            }
            catch
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }

                throw;
            }
        }

        return await LoadAsync(projectFilePath, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<string> CreateAsync(string directory, string projectName, IProjectProfile profile, string rootNamespace, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        ArgumentException.ThrowIfNullOrEmpty(projectName);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrEmpty(rootNamespace);

        string csprojPath = Path.Combine(directory, $"{projectName}.csproj");
        if (File.Exists(csprojPath))
        {
            throw new IOException($"A project file already exists at '{csprojPath}'.");
        }

        await ProjectFiles.EnsureGitAttributesAsync(directory, cancellationToken).ConfigureAwait(false);

        string content = profile.ProjectTemplate
            .Replace("{ProjectName}", projectName, StringComparison.Ordinal)
            .Replace("{RootNamespace}", rootNamespace, StringComparison.Ordinal)
            .Replace("{TargetFramework}", profile.DefaultTargetFramework, StringComparison.Ordinal)
            .Replace("{ProfileId}", profile.Id, StringComparison.Ordinal)
            .Replace("{NetPrintsSdkVersion}", options.NetPrintsSdkVersion, StringComparison.Ordinal);

        await File.WriteAllTextAsync(csprojPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken).ConfigureAwait(false);

        return csprojPath;
    }

    /// <inheritdoc/>
    public async Task<BuildResult> BuildAsync(string projectFilePath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectFilePath);
        EnsureRegisteredOrThrow();

        string projectDirectory = GetDirectoryOrThrow(projectFilePath);
        var request = new ProcessStartRequest(
            "dotnet",
            ["build", projectFilePath, "-nologo", "-tl:off", "-v:quiet", "-clp:NoSummary", "-t:Build", "-getProperty:TargetPath"],
            projectDirectory,
            new Dictionary<string, string> { ["DOTNET_CLI_UI_LANGUAGE"] = "en", ["MSBUILDTERMINALLOGGER"] = "off" });

        ProcessResult result = await processes.RunAsync(request, cancellationToken).ConfigureAwait(false);
        string log = string.Concat(result.StandardOutput, result.StandardError);
        List<ProjectMessage> messages = MsBuildMessageParser.Parse(log)
            .Distinct()
            .OrderBy(message => message.File, StringComparer.Ordinal)
            .ThenBy(message => message.Line)
            .ThenBy(message => message.Code, StringComparer.Ordinal)
            .ToList();
        string? outputAssemblyPath = string.IsNullOrWhiteSpace(result.StandardOutput) ? null : result.StandardOutput.Trim();

        bool success = result.ExitCode == 0;
        Log.Built(logger, projectFilePath, success);
        return new BuildResult(success, messages, outputAssemblyPath, log);
    }

    /// <inheritdoc/>
    public ProcessStartRequest GetRunCommand(string projectFilePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectFilePath);
        string projectDirectory = GetDirectoryOrThrow(projectFilePath);
        return new ProcessStartRequest("dotnet", ["run", "--project", projectFilePath, "--no-build"], projectDirectory);
    }

    private void EnsureRegisteredOrThrow()
    {
        if (!MsBuildRegistration.EnsureRegistered(logger))
        {
            throw new ProjectSystemException(ProjectSystemException.NoSdkRegistered,
                "No .NET SDK or Visual Studio MSBuild instance could be found.");
        }
    }

    private static string GetDirectoryOrThrow(string projectFilePath) =>
        Path.GetDirectoryName(projectFilePath) is { Length: > 0 } directory
            ? directory
            : throw new ArgumentException($"'{projectFilePath}' has no directory.", nameof(projectFilePath));

    private async Task<ProjectMessage?> RestoreIfNeededAsync(string projectFilePath, CancellationToken cancellationToken)
    {
        if (!NeedsRestore(projectFilePath))
        {
            return null;
        }

        Log.Restoring(logger, projectFilePath);
        var request = new ProcessStartRequest("dotnet", ["restore", projectFilePath, "-nologo", "-v:quiet"], GetDirectoryOrThrow(projectFilePath));
        ProcessResult result = await processes.RunAsync(request, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode == 0)
        {
            return null;
        }

        Log.RestoreFailed(logger, projectFilePath, result.ExitCode);
        string message = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
        return new ProjectMessage(ProjectMessageSeverity.Error, ProjectMessage.RestoreFailed, message, projectFilePath, null, null);
    }

    private static bool NeedsRestore(string projectFilePath)
    {
        string projectDirectory = GetDirectoryOrThrow(projectFilePath);
        string assetsPath = Path.Combine(projectDirectory, "obj", "project.assets.json");
        if (!File.Exists(assetsPath))
        {
            return true;
        }

        DateTime assetsTime = File.GetLastWriteTimeUtc(assetsPath);
        if (File.GetLastWriteTimeUtc(projectFilePath) > assetsTime)
        {
            return true;
        }

        foreach (string importPath in FindDirectoryBuildFiles(projectDirectory))
        {
            if (File.GetLastWriteTimeUtc(importPath) > assetsTime)
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> FindDirectoryBuildFiles(string startDirectory)
    {
        for (DirectoryInfo? directory = new DirectoryInfo(startDirectory); directory is not null; directory = directory.Parent)
        {
            foreach (string fileName in DirectoryBuildFileNames)
            {
                string candidate = Path.Combine(directory.FullName, fileName);
                if (File.Exists(candidate))
                {
                    yield return candidate;
                }
            }
        }
    }

    /// <summary>
    /// Evaluates <paramref name="projectFilePath"/> in-process, retargeting to the first declared
    /// framework (project-system.md §1: "NPW004 info") when the project multi-targets
    /// (<c>TargetFrameworks</c>) instead of single-targeting (<c>TargetFramework</c>).
    /// </summary>
    private static (MSBuildProject Evaluated, ProjectCollection Collection, string? RetargetedFramework) Evaluate(string projectFilePath)
    {
        var collection = new ProjectCollection();
        MSBuildProject project = EvaluateOrThrow(projectFilePath, null, collection);

        if (!string.IsNullOrEmpty(project.GetPropertyValue("TargetFramework")))
        {
            return (project, collection, null);
        }

        string[] frameworks = project.GetPropertyValue("TargetFrameworks")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (frameworks.Length == 0)
        {
            return (project, collection, null);
        }

        collection.Dispose();
        string firstFramework = frameworks[0];
        var retargeted = new ProjectCollection();
        MSBuildProject retargetedProject = EvaluateOrThrow(projectFilePath,
            new Dictionary<string, string> { ["TargetFramework"] = firstFramework }, retargeted);
        return (retargetedProject, retargeted, firstFramework);
    }

    private static MSBuildProject EvaluateOrThrow(string projectFilePath, IDictionary<string, string>? globalProperties, ProjectCollection collection)
    {
        try
        {
            return new MSBuildProject(projectFilePath, globalProperties, toolsVersion: null, collection);
        }
        catch (InvalidProjectFileException ex)
        {
            collection.Dispose();
            throw new ProjectSystemException(ProjectSystemException.EvaluationFailed, ex.Message, ex);
        }
    }

    private static List<ResolvedAssembly> BuildResolvedAssemblies(RoslynProject roslynProject) =>
        roslynProject.MetadataReferences
            .OfType<PortableExecutableReference>()
            .Where(reference => reference.FilePath is not null)
            .Select(reference => new ResolvedAssembly(reference.FilePath!, FindDocumentationPath(reference.FilePath!)))
            .OrderBy(reference => reference.Path, StringComparer.Ordinal)
            .ToList();

    private static string? FindDocumentationPath(string assemblyPath)
    {
        string candidate = Path.ChangeExtension(assemblyPath, ".xml");
        return File.Exists(candidate) ? candidate : null;
    }

    private static async Task<List<SourceFile>> BuildOtherSourcesAsync(RoslynProject roslynProject, CancellationToken cancellationToken)
    {
        var sources = new List<SourceFile>();
        foreach (Document document in roslynProject.Documents)
        {
            if (document.FilePath is not { Length: > 0 } path || path.EndsWith(".netpc.g.cs", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            SourceText text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
            sources.Add(new SourceFile(path, text.ToString()));
        }

        return sources;
    }

    private static string BuildCompilationOptionsJson(RoslynProject roslynProject, MSBuildProject evaluated)
    {
        string languageVersion = (roslynProject.ParseOptions as CSharpParseOptions)?.LanguageVersion.ToDisplayString() ?? "default";
        string nullableContext = ((roslynProject.CompilationOptions as CSharpCompilationOptions)?.NullableContextOptions
            ?? NullableContextOptions.Disable).ToString();
        string implicitUsingsValue = evaluated.GetPropertyValue("ImplicitUsings");
        bool implicitUsings = string.Equals(implicitUsingsValue, "enable", StringComparison.OrdinalIgnoreCase)
            || string.Equals(implicitUsingsValue, "true", StringComparison.OrdinalIgnoreCase);

        return JsonSerializer.Serialize(new CompilationOptionsInfo(languageVersion, nullableContext, implicitUsings));
    }

    /// <summary>
    /// Narrow, deliberately minimal shape behind <see cref="ProjectSnapshot.CompilationOptionsJson"/>
    /// ("serialized language version, nullable, usings" per project-system.md §4's own comment): the
    /// full contract for what <c>CodeAnalysisSession</c> (compilation-and-diagnostics.md §2, T089/T092+)
    /// needs is not specified beyond that phrase, so this is a reasonable, revisitable placeholder —
    /// see implementation-notes.md.
    /// </summary>
    private sealed record CompilationOptionsInfo(string LanguageVersion, string Nullable, bool ImplicitUsings);

    private static List<ProjectReferenceInfo> BuildDeclaredReferences(ProjectRootElement root)
    {
        var result = new List<ProjectReferenceInfo>();

        foreach (ProjectItemElement item in root.Items)
        {
            switch (item.ItemType)
            {
                case "PackageReference":
                    result.Add(new ProjectReferenceInfo(DeclaredReferenceKind.Package, item.Include,
                        GetMetadataOrNull(item, "Version"), Included: true, Editable: false));
                    break;
                case "ProjectReference":
                    result.Add(new ProjectReferenceInfo(DeclaredReferenceKind.Project, item.Include, null, true, false));
                    break;
                case "Reference" when GetMetadataOrNull(item, "HintPath") is not null:
                    result.Add(new ProjectReferenceInfo(DeclaredReferenceKind.Assembly, item.Include, null, true, true));
                    break;
                case "Compile" or "None" when string.Equals(GetMetadataOrNull(item, "NetPrintsSourceDirectory"), "true", StringComparison.OrdinalIgnoreCase):
                    result.Add(new ProjectReferenceInfo(DeclaredReferenceKind.SourceDirectory, StripSourceDirectoryGlob(item.Include),
                        null, string.Equals(item.ItemType, "Compile", StringComparison.Ordinal), true));
                    break;
            }
        }

        return result;
    }

    private static string? GetMetadataOrNull(ProjectItemElement item, string name) =>
        item.Metadata.FirstOrDefault(metadata => string.Equals(metadata.Name, name, StringComparison.Ordinal))?.Value;

    private static void ApplyEdit(ProjectRootElement root, ProjectEdit edit, string projectDirectory)
    {
        switch (edit)
        {
            case ProjectEdit.SetOutputType setOutputType:
                SetProperty(root, "OutputType", setOutputType.Value == BinaryType.Executable ? "Exe" : "Library");
                break;
            case ProjectEdit.SetProfile setProfile:
                SetProperty(root, "NetPrintsProfile", setProfile.ProfileId);
                break;
            case ProjectEdit.AddAssemblyReference addAssembly:
                AddAssemblyReference(root, addAssembly.AssemblyPath, projectDirectory);
                break;
            case ProjectEdit.AddSourceDirectory addSourceDirectory:
                AddSourceDirectory(root, addSourceDirectory.DirectoryPath, projectDirectory);
                break;
            case ProjectEdit.SetSourceDirectoryIncluded setIncluded:
                SetSourceDirectoryIncluded(root, setIncluded.DirectoryPath, setIncluded.Included, projectDirectory);
                break;
            case ProjectEdit.RemoveReference removeReference:
                RemoveReference(root, removeReference.Kind, removeReference.Include, projectDirectory);
                break;
            case ProjectEdit.AddNetPrintsSdk addSdk:
                AddNetPrintsSdk(root, addSdk.Version);
                break;
            default:
                throw new NotSupportedException($"Unknown project edit '{edit.GetType()}'.");
        }
    }

    private static void SetProperty(ProjectRootElement root, string name, string value)
    {
        ProjectPropertyElement? existing = root.Properties
            .FirstOrDefault(property => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.Value = value;
        }
        else
        {
            root.AddProperty(name, value);
        }
    }

    private static string MakeRelativeIfUnder(string projectDirectory, string path)
    {
        string fullPath = Path.GetFullPath(path);
        string fullProjectDirectory = Path.GetFullPath(projectDirectory);
        string prefix = fullProjectDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? fullProjectDirectory
            : fullProjectDirectory + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(prefix, StringComparison.Ordinal) ? Path.GetRelativePath(fullProjectDirectory, fullPath) : fullPath;
    }

    private static void AddAssemblyReference(ProjectRootElement root, string assemblyPath, string projectDirectory)
    {
        string hintPath = MakeRelativeIfUnder(projectDirectory, assemblyPath);

        foreach (ProjectItemElement item in root.Items)
        {
            if (!string.Equals(item.ItemType, "Reference", StringComparison.Ordinal))
            {
                continue;
            }

            string? existingHintPath = GetMetadataOrNull(item, "HintPath");
            if (existingHintPath is not null && string.Equals(existingHintPath, hintPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        string simpleName = Path.GetFileNameWithoutExtension(assemblyPath);
        ProjectItemElement newItem = root.AddItem("Reference", simpleName);
        newItem.AddMetadata("HintPath", hintPath);
    }

    private static string SourceDirectoryGlob(string projectDirectory, string directoryPath) =>
        MakeRelativeIfUnder(projectDirectory, directoryPath) + SourceDirectoryGlobSuffix;

    private static string StripSourceDirectoryGlob(string include) =>
        include.EndsWith(SourceDirectoryGlobSuffix, StringComparison.Ordinal)
            ? include[..^SourceDirectoryGlobSuffix.Length]
            : include;

    private static void AddSourceDirectory(ProjectRootElement root, string directoryPath, string projectDirectory)
    {
        string glob = SourceDirectoryGlob(projectDirectory, directoryPath);
        ProjectItemElement item = root.AddItem("Compile", glob);
        item.AddMetadata("NetPrintsSourceDirectory", "true", expressAsAttribute: true);
    }

    private static void SetSourceDirectoryIncluded(ProjectRootElement root, string directoryPath, bool included, string projectDirectory)
    {
        string glob = SourceDirectoryGlob(projectDirectory, directoryPath);
        string wantedType = included ? "Compile" : "None";

        ProjectItemElement? existing = root.Items.FirstOrDefault(item =>
            (string.Equals(item.ItemType, "Compile", StringComparison.Ordinal) || string.Equals(item.ItemType, "None", StringComparison.Ordinal))
            && string.Equals(item.Include, glob, StringComparison.Ordinal)
            && string.Equals(GetMetadataOrNull(item, "NetPrintsSourceDirectory"), "true", StringComparison.OrdinalIgnoreCase));

        if (existing is null || string.Equals(existing.ItemType, wantedType, StringComparison.Ordinal))
        {
            return;
        }

        var group = (ProjectItemGroupElement)existing.Parent!;
        ProjectItemElement replacement = group.AddItem(wantedType, glob);
        replacement.AddMetadata("NetPrintsSourceDirectory", "true", expressAsAttribute: true);
        group.RemoveChild(existing);
    }

    private static void RemoveReference(ProjectRootElement root, DeclaredReferenceKind kind, string include, string projectDirectory)
    {
        if (kind is DeclaredReferenceKind.Package or DeclaredReferenceKind.Project)
        {
            throw new ArgumentException($"A {kind} reference is not editable in P1 and cannot be removed.", nameof(kind));
        }

        string matchInclude = kind == DeclaredReferenceKind.SourceDirectory ? SourceDirectoryGlob(projectDirectory, include) : include;

        bool Matches(ProjectItemElement item) => kind == DeclaredReferenceKind.Assembly
            ? string.Equals(item.ItemType, "Reference", StringComparison.Ordinal) && string.Equals(item.Include, matchInclude, StringComparison.Ordinal)
            : (string.Equals(item.ItemType, "Compile", StringComparison.Ordinal) || string.Equals(item.ItemType, "None", StringComparison.Ordinal))
                && string.Equals(item.Include, matchInclude, StringComparison.Ordinal)
                && string.Equals(GetMetadataOrNull(item, "NetPrintsSourceDirectory"), "true", StringComparison.OrdinalIgnoreCase);

        ProjectItemElement? found = root.Items.FirstOrDefault(Matches);
        if (found is not null)
        {
            ((ProjectItemGroupElement)found.Parent!).RemoveChild(found);
        }
    }

    private static void AddNetPrintsSdk(ProjectRootElement root, string version)
    {
        ProjectItemElement item = root.AddItem("PackageReference", "NetPrints.Sdk");
        item.AddMetadata("Version", version, expressAsAttribute: true);
        item.AddMetadata("PrivateAssets", "all", expressAsAttribute: true);
    }
}
