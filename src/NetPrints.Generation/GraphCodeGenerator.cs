#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Loading;
using NetPrints.Projects;
using NetPrints.Serialization;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Json;
using NetPrints.Serialization.Mapping;
using NetPrints.Serialization.Migrations;
using NetPrints.Translator;

namespace NetPrints.Generation;

/// <summary>
/// One graph document <see cref="GraphCodeGenerator.GenerateAsync"/> processed: whether its
/// <c>.netpc.g.cs</c> was (re)written, and the diagnostics found while reading or translating it
/// (project-system.md §3).
/// </summary>
/// <param name="Input">Path of the graph document that was read (<see cref="GraphJob.Input"/>).</param>
/// <param name="Output">Path of the generated file (<see cref="GraphJob.Output"/>).</param>
/// <param name="Written">Whether <paramref name="Output"/> was (re)written. <see langword="false"/>
/// both when its bytes already matched the rendered content and when an error stopped generation
/// before rendering — either way, a file left over from a previous, successful generation is
/// untouched.</param>
/// <param name="Diagnostics">Diagnostics found while reading or translating <paramref name="Input"/>,
/// in the order they were found.</param>
public sealed record GeneratedFileResult(string Input, string Output, bool Written, IReadOnlyList<CodeDiagnostic> Diagnostics);

/// <summary>
/// Translates graph documents (<c>.netpc.json</c>) to C# (project-system.md §3): the library
/// <c>NetPrints.Generator.Program</c> execs on behalf of the <c>NetPrints.Sdk</c> build target, and
/// that the editor's live preview and the P2 CLI's <c>netprints generate</c> both call directly
/// (ADR-0009).
/// </summary>
/// <remarks>
/// Translates with <see cref="ExtensionRegistry.Translation"/>, so the node translators and emitters of the
/// extensions the request names take part; a graph holding a node of an extension that is not loaded is
/// reported as <c>NPT003</c> and its <c>.netpc.g.cs</c> is left as it was.
/// </remarks>
public sealed class GraphCodeGenerator
{
    /// <summary>Diagnostic id of a graph that holds a node of an extension that is not loaded.</summary>
    public const string MissingExtensionCode = "NPT003";

    private readonly ExtensionRegistry extensions;
    private readonly DocumentFormatRegistry formats;
    private readonly IDocumentMapper mapper;

    /// <summary>
    /// Creates a generator backed by <paramref name="extensions"/>, <paramref name="formats"/> and <paramref name="mapper"/>.
    /// </summary>
    /// <param name="extensions">Loaded extensions whose translation environment classes are translated with.</param>
    /// <param name="formats">Document formats a graph's file name is resolved against.</param>
    /// <param name="mapper">Mapper used to build a <see cref="ClassGraph"/> from each graph's document.</param>
    public GraphCodeGenerator(ExtensionRegistry extensions, DocumentFormatRegistry formats, IDocumentMapper mapper)
    {
        this.extensions = extensions ?? throw new ArgumentNullException(nameof(extensions));
        this.formats = formats ?? throw new ArgumentNullException(nameof(formats));
        this.mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    /// <summary>
    /// Creates a generator whose document formats and mapper know the node kinds and JSON metadata
    /// <paramref name="extensions"/> contributes.
    /// </summary>
    /// <param name="extensions">Loaded extensions, the built-in one included.</param>
    /// <returns>The generator.</returns>
    public static GraphCodeGenerator Create(ExtensionRegistry extensions)
    {
        ArgumentNullException.ThrowIfNull(extensions);
        var mapper = new DocumentMapper(extensions.NodeConverters, NullLogger<DocumentMapper>.Instance);
        var jsonFormat = new JsonDocumentFormat(new NetPrintsJsonOptions(extensions.NodeConverters),
            new DocumentMigrator([], NullLogger<DocumentMigrator>.Instance));
        return new GraphCodeGenerator(extensions, new DocumentFormatRegistry([jsonFormat]), mapper);
    }

    /// <summary>
    /// Loads the extensions of <paramref name="request"/>: only its explicit <see cref="GenerateRequest.Extensions"/>
    /// folders and the built-in extension, never a user extension directory (project-system.md §3).
    /// </summary>
    /// <param name="request">The request whose extension folders are loaded.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The registry, and one <c>NPX</c> error per extension that failed or contribution that was rejected.</returns>
    public static (ExtensionRegistry Registry, IReadOnlyList<CodeDiagnostic> Diagnostics) LoadExtensions(GenerateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var options = new ExtensionLoaderOptions([], [.. request.Extensions], [BuiltInExtension.InProcessEntry]);
        ExtensionRegistry registry = new ExtensionLoader(options, NullLoggerFactory.Instance).Load(cancellationToken);

        var diagnostics = new List<CodeDiagnostic>();
        foreach (ExtensionLoadResult.Failed failure in registry.Results.OfType<ExtensionLoadResult.Failed>())
        {
            string source = failure.ManifestPath
                ?? request.Extensions.FirstOrDefault(folder => Path.GetFileName(folder.TrimEnd('/', '\\')) == failure.Id)
                ?? failure.Id;
            diagnostics.Add(ExtensionError(failure.Code, $"Extension '{failure.Id}' was not loaded: {failure.Reason}", source));
        }

        foreach (ExtensionContributionIssue issue in registry.Issues)
        {
            diagnostics.Add(ExtensionError(issue.Code, $"Extension '{issue.ExtensionId}' contribution '{issue.Contribution}' was rejected: {issue.Reason}", issue.ExtensionId));
        }

        return (registry, diagnostics);
    }

    private static CodeDiagnostic ExtensionError(string code, string message, string source) =>
        new(CodeDiagnosticSeverity.Error, code, message, ClassFullName: null, GraphKey: null, NodeId: null, SourcePath: source, Span: null);

    /// <summary>
    /// Generates every graph of <paramref name="request"/>.
    /// </summary>
    /// <param name="request">Graphs to generate, and the project they belong to.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>One result per graph of <paramref name="request"/>, in request order.</returns>
    public async Task<IReadOnlyList<GeneratedFileResult>> GenerateAsync(GenerateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // A throwaway host for the classes built in this call: FromDocument requires one, but nothing
        // here registers a class on it or resolves another class through it, so a fresh, unshared
        // project per request is enough (it is never built).
        string projectName = Path.GetFileNameWithoutExtension(request.ProjectPath);
        Project project = Project.FromSnapshot(new ProjectSnapshot(
            request.ProjectPath, projectName, request.RootNamespace ?? string.Empty, projectName, BinaryType.SharedLibrary,
            "net10.0", request.Profile, ReferencesNetPrintsSdk: true, GraphFiles: [], ExtensionFolders: [],
            References: [], DeclaredReferences: [], OtherSources: [], CompilationOptionsJson: "{}",
            Properties: new Dictionary<string, string>(), Messages: []));

        var results = new List<GeneratedFileResult>(request.Graphs.Count);
        foreach (GraphJob job in request.Graphs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await GenerateOneAsync(job, project, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    private async Task<GeneratedFileResult> GenerateOneAsync(GraphJob job, Project project, CancellationToken cancellationToken)
    {
        var id = new DocumentId(Path.GetFileName(job.Input));

        IDocumentFormat? format = formats.Find(id, DocumentKind.Class);
        if (format is null)
        {
            var diagnostic = new CodeDiagnostic(CodeDiagnosticSeverity.Error, DocumentIssue.DocumentUnreadable,
                $"No document format recognizes '{job.Input}'.", ClassFullName: null, GraphKey: null, NodeId: null,
                SourcePath: job.Input, Span: null);
            return new GeneratedFileResult(job.Input, job.Output, false, [diagnostic]);
        }

        ClassDocument document;
        try
        {
            await using FileStream input = File.OpenRead(job.Input);
            document = await format.ReadClassAsync(input, id, cancellationToken).ConfigureAwait(false);
        }
        catch (DocumentFormatException ex)
        {
            return new GeneratedFileResult(job.Input, job.Output, false, [ToDiagnostic(ex, job.Input)]);
        }
        catch (IOException ex)
        {
            return new GeneratedFileResult(job.Input, job.Output, false, [ToUnreadableDiagnostic(ex, job.Input)]);
        }

        var issues = new List<DocumentIssue>();
        ClassGraph cls;
        try
        {
            cls = mapper.FromDocument(document, project, issues, id);
        }
        catch (DocumentFormatException ex)
        {
            return new GeneratedFileResult(job.Input, job.Output, false, [ToDiagnostic(ex, job.Input)]);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidCastException or InvalidOperationException)
        {
            // Backstop (R1-05): a malformed reference the mapper couldn't recognize as a document
            // problem on its own must still exit with a diagnostic (and exit code 1), not exit 3 as an
            // internal error.
            return new GeneratedFileResult(job.Input, job.Output, false, [ToUnreadableDiagnostic(ex, job.Input)]);
        }

        var diagnostics = new List<CodeDiagnostic>(issues.Count);
        bool missingExtension = false;
        foreach (DocumentIssue issue in issues)
        {
            CodeDiagnostic diagnostic = issue.ToDiagnostic() with { ClassFullName = cls.FullName, SourcePath = job.Input };
            if (issue.Code == DocumentIssue.UnknownNodeKind)
            {
                missingExtension = true;
                diagnostic = diagnostic with
                {
                    Severity = CodeDiagnosticSeverity.Error,
                    Id = MissingExtensionCode,
                    Message = $"{issue.Message} Its extension is not loaded: add a NetPrintsExtension item for it.",
                };
            }

            diagnostics.Add(diagnostic);
        }

        if (missingExtension)
        {
            return new GeneratedFileResult(job.Input, job.Output, false, diagnostics);
        }

        TranslatedClass translated;
        try
        {
            translated = new ClassTranslator(extensions.Translation).Translate(cls);
        }
        catch (TranslationException ex)
        {
            diagnostics.Add(DiagnosticMapper.FromTranslation(ex, cls) with { SourcePath = job.Input });
            return new GeneratedFileResult(job.Input, job.Output, false, diagnostics);
        }

        string rendered = RenderFile(translated, Path.GetFileName(job.Input));
        bool written = await WriteIfChangedAsync(job.Output, rendered, cancellationToken).ConfigureAwait(false);

        return new GeneratedFileResult(job.Input, job.Output, written, diagnostics);
    }

    /// <summary>
    /// Renders the generated file for <paramref name="graphFileName"/>: the auto-generated header
    /// (project-system.md §3) immediately followed by <paramref name="translated"/>'s code, with every
    /// <c>\r\n</c> replaced by <c>\n</c>, ending with exactly one <c>\n</c>.
    /// </summary>
    /// <param name="translated">Class translated from the graph's graphs (compilation-and-diagnostics.md
    /// §2); only its <see cref="TranslatedClass.Code"/> is written, its source map is metadata for the
    /// caller (T090's <c>classesByGeneratedPath</c>).</param>
    /// <param name="graphFileName">File name (no directory) of the graph document the code was
    /// translated from, named in the header.</param>
    /// <returns>The complete, deterministic file content.</returns>
    public static string RenderFile(TranslatedClass translated, string graphFileName)
    {
        ArgumentException.ThrowIfNullOrEmpty(graphFileName);
        ArgumentNullException.ThrowIfNull(translated);

        string header = "// <auto-generated>\n" +
            $"//     Generated by NetPrints from {graphFileName}. Do not edit.\n" +
            "// </auto-generated>\n";

        string body = translated.Code.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');
        return body.Length == 0 ? header : $"{header}{body}\n";
    }

    private static CodeDiagnostic ToDiagnostic(DocumentFormatException ex, string sourcePath)
    {
        LinePositionSpan? span = ex.Line is { } line
            ? new LinePositionSpan(
                new LinePosition((int)(line - 1), (int)(ex.BytePosition ?? 0)),
                new LinePosition((int)(line - 1), (int)(ex.BytePosition ?? 0)))
            : null;

        return new CodeDiagnostic(CodeDiagnosticSeverity.Error, DocumentIssue.DocumentUnreadable, ex.Message,
            ClassFullName: null, GraphKey: null, NodeId: null, SourcePath: sourcePath, Span: span);
    }

    private static CodeDiagnostic ToUnreadableDiagnostic(Exception ex, string sourcePath) =>
        new(CodeDiagnosticSeverity.Error, DocumentIssue.DocumentUnreadable, ex.Message,
            ClassFullName: null, GraphKey: null, NodeId: null, SourcePath: sourcePath, Span: null);

    private static async Task<bool> WriteIfChangedAsync(string path, string content, CancellationToken cancellationToken)
    {
        byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content);

        if (File.Exists(path))
        {
            byte[] existing = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            if (existing.AsSpan().SequenceEqual(bytes))
            {
                return false;
            }
        }

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string tempPath = $"{path}.tmp-{Guid.NewGuid():N}";
        try
        {
            await File.WriteAllBytesAsync(tempPath, bytes, cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, path, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }

        return true;
    }
}
