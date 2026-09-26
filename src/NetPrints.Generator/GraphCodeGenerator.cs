#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Text;
using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Serialization;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Mapping;
using NetPrints.Translator;

namespace NetPrints.Generator;

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
/// Translates graph documents (<c>.netpc.json</c>) to C# (project-system.md §3): the library the
/// <c>NetPrints.Sdk</c> build target execs (<see cref="Program"/>), and, later, the editor's live
/// preview and the P2 CLI's <c>netprints generate</c> both call directly.
/// </summary>
/// <remarks>
/// Does not yet take an extension registry: nothing under <c>src/NetPrints.Extensibility</c> exists
/// until sub-phase F (T068). <see cref="GenerateRequest.Extensions"/> is parsed and carried through
/// today so the request file format does not change again, but is not consulted here; T065/T068 add
/// an <c>ExtensionRegistry</c> constructor parameter and wire it in once it exists
/// (implementation-notes.md, T044).
/// </remarks>
public sealed class GraphCodeGenerator
{
    private readonly DocumentFormatRegistry formats;
    private readonly IDocumentMapper mapper;

    /// <summary>
    /// Creates a generator backed by <paramref name="formats"/> and <paramref name="mapper"/>.
    /// </summary>
    /// <param name="formats">Document formats a graph's file name is resolved against.</param>
    /// <param name="mapper">Mapper used to build a <see cref="ClassGraph"/> from each graph's document.</param>
    public GraphCodeGenerator(DocumentFormatRegistry formats, IDocumentMapper mapper)
    {
        this.formats = formats ?? throw new ArgumentNullException(nameof(formats));
        this.mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

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
        // project per request is enough (no legacy default references: this project is never compiled).
        Project project = Project.CreateNew(
            Path.GetFileNameWithoutExtension(request.ProjectPath),
            request.RootNamespace ?? string.Empty,
            addDefaultReferences: false);

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

        var diagnostics = new List<CodeDiagnostic>(issues.Count);
        foreach (DocumentIssue issue in issues)
        {
            diagnostics.Add(issue.ToDiagnostic() with { ClassFullName = cls.FullName, SourcePath = job.Input });
        }

        string translated = new ClassTranslator().TranslateClass(cls);
        string rendered = RenderFile(translated, Path.GetFileName(job.Input));
        bool written = await WriteIfChangedAsync(job.Output, rendered, cancellationToken).ConfigureAwait(false);

        return new GeneratedFileResult(job.Input, job.Output, written, diagnostics);
    }

    /// <summary>
    /// Renders the generated file for <paramref name="graphFileName"/>: the auto-generated header
    /// (project-system.md §3) immediately followed by <paramref name="translatedCode"/>, with every
    /// <c>\r\n</c> replaced by <c>\n</c>, ending with exactly one <c>\n</c>.
    /// </summary>
    /// <param name="translatedCode">C# source translated from the class's graphs.</param>
    /// <param name="graphFileName">File name (no directory) of the graph document the code was
    /// translated from, named in the header.</param>
    /// <returns>The complete, deterministic file content.</returns>
    /// <remarks>
    /// Takes the translated C# as a plain <see cref="string"/> rather than the contract's
    /// <c>TranslatedClass</c>: that record (with its source map) is added in T089
    /// (implementation-notes.md, T044).
    /// </remarks>
    public static string RenderFile(string translatedCode, string graphFileName)
    {
        ArgumentException.ThrowIfNullOrEmpty(graphFileName);
        ArgumentNullException.ThrowIfNull(translatedCode);

        string header = "// <auto-generated>\n" +
            $"//     Generated by NetPrints from {graphFileName}. Do not edit.\n" +
            "// </auto-generated>\n";

        string body = translatedCode.Replace("\r\n", "\n").TrimEnd('\n');
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
