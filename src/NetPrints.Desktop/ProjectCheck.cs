using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Locator;
using Microsoft.Extensions.Logging;
using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Editor.Hosting;
using NetPrints.Extensibility.Loading;
using NetPrints.Projects;
using NetPrints.Serialization;
using NetPrints.Serialization.Mapping;
using NetPrints.Serialization.Stores;
using NetPrints.Translator;
using NetPrints.Workspace;

namespace NetPrints.Desktop;

/// <summary>
/// Headless project check (release contract §5, research R19): loads, translates, analyzes and builds
/// a project with the same services the editor uses (Workspace, Serialization, Core) and, with
/// <c>--run</c>, runs its built output — no Avalonia type is touched, so it needs no display. Backs
/// <c>Program.Main</c>'s <c>--check-project</c> branch, used by <c>scripts/smoke-desktop.sh</c> to
/// verify the self-contained editor archive can still open and build a real project.
/// </summary>
internal static class ProjectCheck
{
    /// <summary>Exit code: a document, translation, analysis or build error.</summary>
    public const int ExitErrors = 1;

    /// <summary>Exit code: no project path was given.</summary>
    public const int ExitBadArguments = 2;

    /// <summary>Exit code: no compatible .NET SDK is registered (<see cref="ProjectSystemException.NoSdkRegistered"/>).</summary>
    public const int ExitNoSdk = 3;

    /// <summary>Exit code: <c>--run</c>'s process exited with a non-zero code.</summary>
    public const int ExitRunFailed = 4;

    // Placeholder for ProjectSystemOptions.NetPrintsSdkVersion, unused here: this check never calls
    // IProjectSystem.CreateAsync (the only member that substitutes it), same literal as NetPrints.Cli.
    private const string UnusedSdkVersionPlaceholder = "1.0.0-dev";

    /// <summary>
    /// Runs the check for <c>NetPrints.Desktop --check-project &lt;path.csproj&gt; [--run]</c>. Calls
    /// <see cref="MsBuildRegistration.EnsureRegistered"/> itself (idempotent alongside <c>Program.Main</c>'s
    /// own call) and builds the real <see cref="IProjectSystem"/> for it.
    /// </summary>
    /// <param name="projectPath">Full path of the <c>.csproj</c> to check, or <see langword="null"/>/empty for bad arguments.</param>
    /// <param name="run">Whether to also run the project's built output.</param>
    /// <param name="output">Where the check's step-by-step report is written.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The process exit code (0 on success).</returns>
    public static async Task<int> RunAsync(string? projectPath, bool run, TextWriter output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);

        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddSimpleConsole().SetMinimumLevel(LogLevel.Warning));
        bool msBuildAvailable = MsBuildRegistration.EnsureRegistered(loggerFactory.CreateLogger(nameof(MsBuildRegistration)));
        return await RunAsync(projectPath, run, output, loggerFactory, msBuildAvailable, cancellationToken);
    }

    /// <summary>
    /// Like <see cref="RunAsync(string?, bool, TextWriter, CancellationToken)"/>, for a caller
    /// (<c>Program.Main</c>) that already registered MSBuild and built the process-wide
    /// <see cref="ILoggerFactory"/>: reuses both instead of building a second factory pinned to
    /// <see cref="LogLevel.Warning"/> (ignoring <c>NETPRINTS_LOG_LEVEL</c>) and calling
    /// <see cref="MsBuildRegistration.EnsureRegistered"/> a second time.
    /// </summary>
    /// <param name="projectPath">Full path of the <c>.csproj</c> to check, or <see langword="null"/>/empty for bad arguments.</param>
    /// <param name="run">Whether to also run the project's built output.</param>
    /// <param name="output">Where the check's step-by-step report is written.</param>
    /// <param name="loggerFactory">The caller's already-created, process-wide logger factory.</param>
    /// <param name="msBuildAvailable">Whether the caller already confirmed an MSBuild instance is registered.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The process exit code (0 on success).</returns>
    public static Task<int> RunAsync(string? projectPath, bool run, TextWriter output, ILoggerFactory loggerFactory, bool msBuildAvailable, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        IProjectSystem projects = msBuildAvailable
            ? new MsBuildProjectSystem(new ProjectSystemOptions([], UnusedSdkVersionPlaceholder), new ProcessRunner(), loggerFactory.CreateLogger<MsBuildProjectSystem>())
            : new NoSdkProjectSystem();

        return RunAsync(projectPath, run, output, msBuildAvailable, MsBuildRegistration.RegisteredInstance, projects, new ProcessRunner(), loggerFactory, cancellationToken);
    }

    /// <summary>
    /// Core of <see cref="RunAsync(string?, bool, TextWriter, CancellationToken)"/>, with every
    /// MSBuild-dependent input supplied by the caller: tests exercise the "no SDK" path
    /// (<paramref name="msBuildAvailable"/> <see langword="false"/>, <paramref name="projects"/> a
    /// <see cref="NoSdkProjectSystem"/>) without touching the real, process-wide
    /// <see cref="MSBuildLocator"/> state.
    /// </summary>
    /// <param name="projectPath">Full path of the <c>.csproj</c> to check, or <see langword="null"/>/empty for bad arguments.</param>
    /// <param name="run">Whether to also run the project's built output.</param>
    /// <param name="output">Where the check's step-by-step report is written.</param>
    /// <param name="msBuildAvailable">Whether an MSBuild instance is registered.</param>
    /// <param name="registeredInstance">The registered instance, if any, printed on the <c>msbuild:</c> line.</param>
    /// <param name="projects">Project system used to load and build the project.</param>
    /// <param name="processes">Runner used for <c>--run</c>.</param>
    /// <param name="loggerFactory">Creates every logger this check needs.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The process exit code (0 on success).</returns>
    internal static async Task<int> RunAsync(string? projectPath, bool run, TextWriter output,
        bool msBuildAvailable, VisualStudioInstance? registeredInstance, IProjectSystem projects,
        IProcessRunner processes, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(processes);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        if (string.IsNullOrEmpty(projectPath) || projectPath.StartsWith("--", StringComparison.Ordinal))
        {
            await Console.Error.WriteLineAsync("Usage: NetPrints.Desktop --check-project <path.csproj> [--run]");
            return ExitBadArguments;
        }

        projectPath = Path.GetFullPath(projectPath);

        string informationalVersion = typeof(ProjectCheck).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
        await output.WriteLineAsync($"NetPrints {informationalVersion.Split('+')[0]}");

        if (!msBuildAvailable)
        {
            await output.WriteLineAsync($"error {ProjectSystemException.NoSdkRegistered}: {NoSdkProjectSystem.Message}");
            return ExitNoSdk;
        }

        await output.WriteLineAsync($"msbuild: {registeredInstance?.MSBuildPath ?? "<unknown>"} ({registeredInstance?.Version.ToString() ?? "<unknown>"})");

        await using var extensions = new ExtensionHost(ExtensionLoaderOptions.BuiltInOnly, loggerFactory);

        try
        {
            ProjectSnapshot snapshot = await projects.LoadAsync(projectPath, cancellationToken);
            await extensions.LoadForProjectAsync(snapshot.ExtensionFolders, cancellationToken);
            if (snapshot.ExtensionFolders.Count > 0)
            {
                // The project's own extensions may request MSBuild properties the first evaluation did not (FR-025).
                snapshot = await projects.LoadAsync(projectPath, cancellationToken);
            }

            await output.WriteLineAsync($"project: {snapshot.ProjectFilePath}");
            ResolvedAssembly? console = snapshot.References.FirstOrDefault(
                reference => string.Equals(Path.GetFileNameWithoutExtension(reference.Path), "System.Console", StringComparison.Ordinal));
            await output.WriteLineAsync($"references: {snapshot.References.Count} (System.Console: {console?.Path ?? "<missing>"})");

            (DocumentFormatRegistry formats, IDocumentMapper mapper) = PersistenceBinding.CreateSerializers(extensions.Current, loggerFactory);
            var persistence = new ProjectPersistence(projects, formats, mapper,
                (directory, watch) => new FileSystemDocumentStore(directory, DefaultScheduler.Instance, loggerFactory.CreateLogger<FileSystemDocumentStore>(), watch),
                loggerFactory.CreateLogger<ProjectPersistence>());

            ProjectLoadResult loaded = await persistence.LoadAsync(snapshot, cancellationToken);
            await output.WriteLineAsync($"graphs: {loaded.Project.Classes.Count}");

            DocumentIssue[] documentErrors = [.. loaded.Issues.Where(issue => issue.Severity == DocumentIssueSeverity.Error)];
            if (documentErrors.Length > 0)
            {
                foreach (DocumentIssue issue in documentErrors)
                {
                    await output.WriteLineAsync(CodeDiagnosticFormat.ToCanonicalLine(issue.ToDiagnostic()));
                }

                return ExitErrors;
            }

            ProjectTranslationResult translation = ProjectTranslation.TranslateAll(loaded.Project, extensions.Current.Translation);
            var session = new CodeAnalysisSession(snapshot.References, snapshot.OtherSources, snapshot.CompilationOptionsJson);
            IReadOnlyList<CodeDiagnostic> analyzed = await session.AnalyzeAsync([.. translation.Classes.Values], cancellationToken);
            CodeDiagnostic[] diagnostics = [.. translation.Diagnostics, .. analyzed];
            int errorCount = diagnostics.Count(diagnostic => diagnostic.Severity == CodeDiagnosticSeverity.Error);
            int warningCount = diagnostics.Count(diagnostic => diagnostic.Severity == CodeDiagnosticSeverity.Warning);
            await output.WriteLineAsync($"analysis: {errorCount} errors, {warningCount} warnings");
            if (errorCount > 0)
            {
                foreach (CodeDiagnostic diagnostic in diagnostics.Where(diagnostic => diagnostic.Severity == CodeDiagnosticSeverity.Error))
                {
                    await output.WriteLineAsync(CodeDiagnosticFormat.ToCanonicalLine(diagnostic));
                }

                return ExitErrors;
            }

            BuildResult build = await projects.BuildAsync(projectPath, cancellationToken);
            await output.WriteLineAsync(build.Success ? "build: succeeded" : "build: failed");
            foreach (CodeDiagnostic message in DiagnosticMapper.FromBuild(build.Messages))
            {
                await output.WriteLineAsync(CodeDiagnosticFormat.ToCanonicalLine(message));
            }

            if (!build.Success)
            {
                return ExitErrors;
            }

            if (run && snapshot.OutputType == BinaryType.Executable)
            {
                ProcessStartRequest runRequest = projects.GetRunCommand(projectPath);
                ProcessResult result = await processes.RunAsync(runRequest, cancellationToken);
                await output.WriteAsync(result.StandardOutput);
                await output.WriteAsync(result.StandardError);
                await output.WriteLineAsync($"run: exit {result.ExitCode}");
                if (result.ExitCode != 0)
                {
                    return ExitRunFailed;
                }
            }

            return 0;
        }
        catch (ProjectSystemException ex)
        {
            await output.WriteLineAsync($"error {ex.Code}: {ex.Message}");
            return ExitErrors;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A hand-edited or malformed graph can make translation or analysis throw outside the
            // handled DocumentIssue/CodeDiagnostic paths above; report it instead of crashing (AGENTS.md
            // "an expected problem in the user's project is a diagnostic, not an exception").
            await Console.Error.WriteLineAsync(ex.ToString());
            return ExitErrors;
        }
    }
}
