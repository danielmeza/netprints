#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NetPrints.Core;
using NetPrints.Projects;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Mapping;
using NetPrints.Serialization.Stores;

namespace NetPrints.Serialization;

/// <summary>
/// Result of <see cref="ProjectPersistence.LoadAsync"/>: the built project, the snapshot it was built
/// from, and any non-fatal issues found while loading its classes (document-format.md §2.8).
/// </summary>
/// <param name="Project">The loaded project, with <see cref="Core.Project.Classes"/> populated in
/// <see cref="ProjectSnapshot.GraphFiles"/> order.</param>
/// <param name="Snapshot">The snapshot <paramref name="Project"/> was built from.</param>
/// <param name="Issues">Issues found while loading its classes: a malformed graph is reported here and
/// skipped instead of failing the whole load.</param>
public sealed record ProjectLoadResult(Project Project, ProjectSnapshot Snapshot, IReadOnlyList<DocumentIssue> Issues);

/// <summary>
/// Result of <see cref="ProjectPersistence.SaveAsync"/>: the files it wrote, in write order.
/// </summary>
/// <param name="WrittenFiles">Full paths of the files that were written (a class's graph, its
/// generated C#, or both); empty if every class was already clean.</param>
public sealed record ProjectSaveResult(IReadOnlyList<string> WrittenFiles);

/// <summary>
/// Loads, saves and adds class graphs of a project backed by a <c>.csproj</c> (document-format.md
/// §2.8): the facade <c>MainEditorVM</c> and the CLI use instead of talking to
/// <see cref="IProjectSystem"/>, <see cref="DocumentFormatRegistry"/> and <see cref="IDocumentMapper"/>
/// directly. Stateless; safe to share. The model it builds or edits is owned by the caller's thread.
/// </summary>
public sealed class ProjectPersistence
{
    private readonly IProjectSystem projects;
    private readonly DocumentFormatRegistry formats;
    private readonly IDocumentMapper mapper;
    private readonly Func<string, IDocumentStore> createStore;
    private readonly ILogger<ProjectPersistence> logger;

    /// <summary>
    /// Creates a persistence facade.
    /// </summary>
    /// <param name="projects">Project system <see cref="LoadAsync"/> reads the <c>.csproj</c> through.</param>
    /// <param name="formats">Document formats a graph file's format is resolved against.</param>
    /// <param name="mapper">Mapper used to convert classes to and from their document form.</param>
    /// <param name="createStore">Creates the document store a project's graphs are read from and
    /// written to, given the project's directory.</param>
    /// <param name="logger">Logger for a class graph that could not be loaded (event 3007).</param>
    public ProjectPersistence(IProjectSystem projects, DocumentFormatRegistry formats, IDocumentMapper mapper,
        Func<string, IDocumentStore> createStore, ILogger<ProjectPersistence> logger)
    {
        this.projects = projects ?? throw new ArgumentNullException(nameof(projects));
        this.formats = formats ?? throw new ArgumentNullException(nameof(formats));
        this.mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        this.createStore = createStore ?? throw new ArgumentNullException(nameof(createStore));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Loads a project: <see cref="IProjectSystem.LoadAsync"/>, then each of its
    /// <see cref="ProjectSnapshot.GraphFiles"/> through the registered document formats, in order.
    /// A graph that cannot be read (a malformed file, or one no format recognizes) is reported as a
    /// <see cref="DocumentIssue.DocumentUnreadable"/> issue and skipped; the project opens without it.
    /// </summary>
    /// <param name="projectFilePath">Full path of the <c>.csproj</c> file to load.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The loaded project, its snapshot, and any issues found loading its classes.</returns>
    public async Task<ProjectLoadResult> LoadAsync(string projectFilePath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectFilePath);

        ProjectSnapshot snapshot = await projects.LoadAsync(projectFilePath, cancellationToken).ConfigureAwait(false);
        Project project = Project.FromSnapshot(snapshot);
        string projectDirectory = GetDirectoryOrThrow(projectFilePath);

        IDocumentStore store = createStore(projectDirectory);
        try
        {
            var issues = new List<DocumentIssue>();
            var classes = new List<ClassGraph>();

            foreach (string graphFilePath in snapshot.GraphFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                DocumentId id = FileSystemDocumentStore.ToDocumentId(projectDirectory, graphFilePath);
                ClassGraph? cls = await TryLoadClassAsync(store, id, project, issues, cancellationToken).ConfigureAwait(false);
                if (cls is not null)
                {
                    cls.LoadedGraphFilePath = graphFilePath;
                    classes.Add(cls);
                }
            }

            project.Classes.ReplaceRange(classes);
            return new ProjectLoadResult(project, snapshot, issues);
        }
        finally
        {
            store.Dispose();
        }
    }

    private async Task<ClassGraph?> TryLoadClassAsync(IDocumentStore store, DocumentId id, Project project,
        List<DocumentIssue> issues, CancellationToken cancellationToken)
    {
        IDocumentFormat? format = formats.Find(id, DocumentKind.Class);
        if (format is null)
        {
            ReportUnreadable(id, $"No document format recognizes '{id}'.", issues);
            return null;
        }

        try
        {
            ClassDocument document;
            await using (Stream input = await store.OpenReadAsync(id, cancellationToken).ConfigureAwait(false))
            {
                document = await format.ReadClassAsync(input, id, cancellationToken).ConfigureAwait(false);
            }

            return mapper.FromDocument(document, project, issues, id);
        }
        catch (DocumentFormatException ex)
        {
            ReportUnreadable(id, ex.Message, issues);
            return null;
        }
    }

    private void ReportUnreadable(DocumentId id, string reason, List<DocumentIssue> issues)
    {
        issues.Add(new DocumentIssue(DocumentIssueSeverity.Error, DocumentIssue.DocumentUnreadable, reason, id));
        Log.ClassLoadFailed(logger, id, reason);
    }

    /// <summary>
    /// Saves every dirty class of <paramref name="project"/> (data-model.md §2): for each, in project
    /// order, <see cref="ClassGraph.EnsureUniqueMemberIds"/> is called, the class is mapped and written
    /// to <see cref="Core.Project.GetGraphFilePath"/>, and <paramref name="renderGenerated"/>'s text is
    /// written next to it (the <c>.netpc.g.cs</c>) — each only when its bytes differ from the file on
    /// disk. A clean class is neither mapped nor written. The <c>.csproj</c> itself is never written.
    /// </summary>
    /// <param name="project">Project whose dirty classes are saved.</param>
    /// <param name="renderGenerated">Renders a class's generated C# file (typically
    /// <c>GraphCodeGenerator.RenderFile</c> over its translated code).</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The files that were written, in write order.</returns>
    public async Task<ProjectSaveResult> SaveAsync(Project project, Func<ClassGraph, string> renderGenerated, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(renderGenerated);

        string projectDirectory = GetDirectoryOrThrow(project.Path);
        IDocumentStore store = createStore(projectDirectory);
        try
        {
            var written = new List<string>();

            foreach (ClassGraph cls in project.Classes)
            {
                if (!cls.IsDirty)
                {
                    continue;
                }

                cancellationToken.ThrowIfCancellationRequested();

                cls.EnsureUniqueMemberIds();

                string graphPath = project.GetGraphFilePath(cls);
                ClassDocument document = mapper.ToDocument(cls);
                byte[] graphBytes = await RenderAsync(
                    (stream, ct) => formats.Default.WriteClassAsync(document, stream, ct), cancellationToken).ConfigureAwait(false);

                DocumentId graphId = FileSystemDocumentStore.ToDocumentId(projectDirectory, graphPath);
                if (await WriteIfDifferentAsync(store, graphId, graphBytes, cancellationToken).ConfigureAwait(false))
                {
                    written.Add(graphPath);
                }

                cls.LoadedGraphFilePath = graphPath;

                string generatedPath = ToGeneratedPath(graphPath);
                byte[] generatedBytes = Encoding.UTF8.GetBytes(renderGenerated(cls));
                DocumentId generatedId = FileSystemDocumentStore.ToDocumentId(projectDirectory, generatedPath);
                if (await WriteIfDifferentAsync(store, generatedId, generatedBytes, cancellationToken).ConfigureAwait(false))
                {
                    written.Add(generatedPath);
                }

                cls.MarkClean();
            }

            return new ProjectSaveResult(written);
        }
        finally
        {
            store.Dispose();
        }
    }

    /// <summary>
    /// Copies <paramref name="sourceGraphPath"/> byte for byte into <paramref name="project"/>'s
    /// directory (its file name preserved) and loads and adds it, clean (project-system.md, "Existing
    /// Class"). A legacy <c>.netpc</c> file is not accepted (research.md R21).
    /// </summary>
    /// <param name="project">Project to add the graph to.</param>
    /// <param name="sourceGraphPath">Full path of the <c>.netpc.json</c> file to copy in.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The added class.</returns>
    /// <exception cref="NotSupportedException"><paramref name="sourceGraphPath"/> does not end with
    /// <c>.netpc.json</c>.</exception>
    public async Task<ClassGraph> AddGraphAsync(Project project, string sourceGraphPath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrEmpty(sourceGraphPath);

        const string classExtension = ".netpc.json";
        if (!sourceGraphPath.EndsWith(classExtension, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                $"'{sourceGraphPath}' is not a '{classExtension}' graph file; a legacy '.netpc' file is not accepted (research.md R21).");
        }

        string projectDirectory = GetDirectoryOrThrow(project.Path);
        string targetPath = Path.Combine(projectDirectory, Path.GetFileName(sourceGraphPath));
        byte[] bytes = await File.ReadAllBytesAsync(sourceGraphPath, cancellationToken).ConfigureAwait(false);

        IDocumentStore store = createStore(projectDirectory);
        try
        {
            DocumentId id = FileSystemDocumentStore.ToDocumentId(projectDirectory, targetPath);
            await store.WriteAsync(id, (stream, ct) => stream.WriteAsync(bytes, ct), cancellationToken).ConfigureAwait(false);

            ClassDocument document;
            await using (Stream input = await store.OpenReadAsync(id, cancellationToken).ConfigureAwait(false))
            {
                document = await formats.Default.ReadClassAsync(input, id, cancellationToken).ConfigureAwait(false);
            }

            var issues = new List<DocumentIssue>();
            ClassGraph cls = mapper.FromDocument(document, project, issues, id);
            cls.LoadedGraphFilePath = targetPath;
            project.Classes.Add(cls);
            return cls;
        }
        finally
        {
            store.Dispose();
        }
    }

    private static async ValueTask<byte[]> RenderAsync(Func<Stream, CancellationToken, ValueTask> write, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await write(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }

    private static async ValueTask<bool> WriteIfDifferentAsync(IDocumentStore store, DocumentId id, byte[] newBytes, CancellationToken cancellationToken)
    {
        if (await store.ExistsAsync(id, cancellationToken).ConfigureAwait(false))
        {
            byte[] existing;
            await using (Stream current = await store.OpenReadAsync(id, cancellationToken).ConfigureAwait(false))
            {
                using var buffer = new MemoryStream();
                await current.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
                existing = buffer.ToArray();
            }

            if (existing.AsSpan().SequenceEqual(newBytes))
            {
                return false;
            }
        }

        await store.WriteAsync(id, (stream, ct) => stream.WriteAsync(newBytes, ct), cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static string ToGeneratedPath(string graphPath)
    {
        const string jsonSuffix = ".json";
        return graphPath.EndsWith(jsonSuffix, StringComparison.OrdinalIgnoreCase)
            ? string.Concat(graphPath.AsSpan(0, graphPath.Length - jsonSuffix.Length), ".g.cs")
            : throw new ArgumentException($"Graph file '{graphPath}' does not end with '{jsonSuffix}'.", nameof(graphPath));
    }

    private static string GetDirectoryOrThrow(string projectFilePath) =>
        Path.GetDirectoryName(projectFilePath) is { Length: > 0 } directory
            ? directory
            : throw new InvalidOperationException($"Project path '{projectFilePath}' has no directory.");
}
