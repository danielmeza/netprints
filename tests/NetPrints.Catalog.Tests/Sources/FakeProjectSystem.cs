using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Projects;

namespace NetPrints.Catalog.Tests.Sources;

/// <summary>Answers <see cref="LoadAsync"/> from a function and records what was loaded, with the project file's text at that moment.</summary>
internal sealed class FakeProjectSystem(Func<string, ProjectSnapshot> snapshots, List<string> events) : IProjectSystem
{
    public List<string> LoadedPaths { get; } = [];

    public List<string> LoadedTexts { get; } = [];

    public Task<ProjectSnapshot> LoadAsync(string projectFilePath, CancellationToken cancellationToken)
    {
        events.Add("load");
        LoadedPaths.Add(projectFilePath);
        LoadedTexts.Add(File.Exists(projectFilePath) ? File.ReadAllText(projectFilePath) : string.Empty);
        return Task.FromResult(snapshots(projectFilePath));
    }

    public static ProjectSnapshot Snapshot(string path, IReadOnlyList<ResolvedAssembly> references, IReadOnlyDictionary<string, string>? properties = null, IReadOnlyList<ProjectMessage>? messages = null) =>
        new(path, "Temp", "Temp", "Temp", BinaryType.SharedLibrary, "net10.0", string.Empty, false, [], [], references, [], [], "{}", properties ?? new Dictionary<string, string>(), messages ?? []);

    public Task<ProjectSnapshot> ApplyAsync(string projectFilePath, IReadOnlyList<ProjectEdit> edits, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<string> CreateAsync(string directory, string projectName, IProjectProfile profile, string rootNamespace, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<BuildResult> BuildAsync(string projectFilePath, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public ProcessStartRequest GetRunCommand(string projectFilePath) =>
        throw new NotSupportedException();
}

/// <summary>Records the processes it is asked to run and answers with a fixed result.</summary>
internal sealed class FakeProcessRunner(List<string> events) : IProcessRunner
{
    public List<ProcessStartRequest> Requests { get; } = [];

    public ProcessResult Result { get; set; } = new(0, string.Empty, string.Empty);

    public Task<ProcessResult> RunAsync(ProcessStartRequest request, CancellationToken cancellationToken)
    {
        events.Add("process");
        Requests.Add(request);
        return Task.FromResult(Result);
    }
}
