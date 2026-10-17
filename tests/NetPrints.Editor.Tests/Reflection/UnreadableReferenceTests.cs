using Microsoft.Extensions.Logging;
using NetPrints.Core;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.Tests.Shell;
using NetPrints.Projects;

namespace NetPrints.Editor.Tests.Reflection;

/// <summary>FR-083: a reference that cannot be read is skipped and reported once, and the other references still load.</summary>
public sealed class UnreadableReferenceTests : IDisposable
{
    private readonly string directory = TestPaths.CreateTempDirectory();
    private readonly List<(LogLevel Level, string Message)> entries = [];

    public void Dispose() => TestPaths.TryDelete(directory);

    private string WriteBadReference(string name)
    {
        string path = Path.Combine(directory, name);
        File.WriteAllText(path, "this is not a portable executable");
        return path;
    }

    private ProjectSnapshot SnapshotWith(params string[] unreadable)
    {
        ProjectSnapshot runtime = TestSnapshots.WithRuntimeAssemblies("P", "N");
        return runtime with { References = [.. runtime.References, .. unreadable.Select(path => new ResolvedAssembly(path, null))] };
    }

    private ReflectionHost NewHost() =>
        new(new InlineDispatcher(), TestExtensions.CreateBuiltIn(), new ListLogger(entries));

    [Fact(Timeout = 120000)]
    public async Task AnUnreadableReferenceIsSkippedReportedOnceAndTheOthersLoad()
    {
        string bad = WriteBadReference("Broken.Reference.dll");
        ReflectionHost host = NewHost();

        await host.ReloadAsync(Project.FromSnapshot(SnapshotWith(bad)), TestContext.Current.CancellationToken);

        Assert.True(host.IsLoaded);
        Assert.True(host.NonStaticTypes.Count > 4000);
        string warning = Assert.Single(host.LastWarnings, text => text.StartsWith(ProjectMessage.UnreadableReference, StringComparison.Ordinal));
        Assert.Contains("Broken.Reference.dll", warning, StringComparison.Ordinal);
        (LogLevel level, string message) = Assert.Single(entries, entry => entry.Message.Contains(ProjectMessage.UnreadableReference, StringComparison.Ordinal));
        Assert.Equal(LogLevel.Warning, level);
        Assert.Contains("Broken.Reference.dll", message, StringComparison.Ordinal);
    }

    [Fact(Timeout = 120000)]
    public async Task EachUnreadableReferenceIsReportedOnceAndAReloadReportsThemAgain()
    {
        string first = WriteBadReference("One.dll");
        string second = WriteBadReference("Two.dll");
        ReflectionHost host = NewHost();
        var project = Project.FromSnapshot(SnapshotWith(first, second));

        await host.ReloadAsync(project, TestContext.Current.CancellationToken);

        Assert.Equal(2, host.LastWarnings.Count(text => text.StartsWith(ProjectMessage.UnreadableReference, StringComparison.Ordinal)));

        await host.ReloadAsync(project, TestContext.Current.CancellationToken);

        Assert.Equal(2, host.LastWarnings.Count(text => text.StartsWith(ProjectMessage.UnreadableReference, StringComparison.Ordinal)));
    }

    [Fact(Timeout = 120000)]
    public async Task ReadableReferencesProduceNoWarning()
    {
        ReflectionHost host = NewHost();

        await host.ReloadAsync(Project.FromSnapshot(SnapshotWith()), TestContext.Current.CancellationToken);

        Assert.DoesNotContain(host.LastWarnings, text => text.StartsWith(ProjectMessage.UnreadableReference, StringComparison.Ordinal));
        Assert.DoesNotContain(entries, entry => entry.Message.Contains(ProjectMessage.UnreadableReference, StringComparison.Ordinal));
    }

    private sealed class ListLogger(List<(LogLevel Level, string Message)> entries) : ILogger<ReflectionHost>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (entries)
            {
                entries.Add((logLevel, formatter(state, exception)));
            }
        }
    }
}
