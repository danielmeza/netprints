using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NetPrints.Compilation;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Output;

/// <summary>
/// The Output panel: the build output (its start, its diagnostics, its result), then the program's standard output and error from
/// <see cref="RunStateTracker"/>; a run brings the panel forward once, when the program starts. A compile clears everything; a run clears the output of the run before. The tracker reports from
/// whatever thread the build or the program runs on, so every change goes through <see cref="IUiDispatcher"/>.
/// </summary>
public sealed partial class OutputPanelViewModel : ObservableObject, IShellPanelContent
{
    /// <summary>How many lines are kept; the oldest go first.</summary>
    public const int MaxLines = 1000;

    private const int DrainChunk = 500;

    private readonly ConcurrentQueue<Action> pendingChanges = new();
    private int drainPosted;
    private PanelContext? context;
    private RunStateTracker? tracker;
    private RunPhase phase;

    /// <summary>Gets the lines shown, oldest first.</summary>
    public ObservableCollection<OutputLineViewModel> Lines { get; } = [];

    /// <inheritdoc/>
    public void Attach(PanelContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        this.context = context;
        tracker = context.Context.RunState;
        phase = tracker.Snapshot().Phase;
        tracker.PhaseChanged += OnPhaseChanged;
        tracker.LineAppended += OnLineAppended;
    }

    /// <inheritdoc/>
    public void Detach()
    {
        if (tracker is { } attached)
        {
            attached.PhaseChanged -= OnPhaseChanged;
            attached.LineAppended -= OnLineAppended;
        }

        tracker = null;
        context = null;
    }

    private void OnPhaseChanged(object? sender, EventArgs e)
    {
        if (context is { } attached && tracker is { } source)
        {
            RunPhase next = source.Snapshot().Phase;
            Enqueue(attached, () => Apply(next));
        }
    }

    private void OnLineAppended(object? sender, RunOutputLine line)
    {
        if (context is { } attached)
        {
            Enqueue(attached, () => Add(new OutputLineViewModel(line.Stream == ProcessStream.Error ? OutputLineKind.Error : OutputLineKind.Output, line.Text)));
        }
    }

    private void Enqueue(PanelContext attached, Action change)
    {
        pendingChanges.Enqueue(change);
        PostDrain(attached);
    }

    private void PostDrain(PanelContext attached)
    {
        if (Interlocked.Exchange(ref drainPosted, 1) == 0)
        {
            attached.Context.Dispatcher.Post(Drain);
        }
    }

    private void Drain()
    {
        for (int i = 0; i < DrainChunk && pendingChanges.TryDequeue(out Action? change); i++)
        {
            change();
        }

        Volatile.Write(ref drainPosted, 0);
        if (!pendingChanges.IsEmpty && context is { } attached)
        {
            PostDrain(attached);
        }
    }

    private void Apply(RunPhase next)
    {
        RunPhase previous = phase;
        phase = next;
        if (context is not { } attached)
        {
            return;
        }

        switch (next)
        {
            case RunPhase.Building:
                Lines.Clear();
                Add(new OutputLineViewModel(OutputLineKind.Build, "Build started."));
                break;
            case RunPhase.Running:
                RemoveProgramOutput();
                attached.Api.ShowPanel(PanelContributions.OutputId);
                if (previous == RunPhase.Building)
                {
                    AddBuildResult();
                }

                break;
            default:
                if (previous == RunPhase.Building)
                {
                    AddBuildResult();
                }

                break;
        }
    }

    private void AddBuildResult()
    {
        if (context?.Shell.Session?.Project is not { } project)
        {
            return;
        }

        foreach (CodeDiagnostic diagnostic in project.LastDiagnostics)
        {
            Add(new OutputLineViewModel(OutputLineKind.Build, CodeDiagnosticFormat.ToCanonicalLine(diagnostic)));
        }

        Add(new OutputLineViewModel(OutputLineKind.Build, project.CompilationMessage));
    }

    private void RemoveProgramOutput()
    {
        for (int i = Lines.Count - 1; i >= 0; i--)
        {
            if (Lines[i].Kind != OutputLineKind.Build)
            {
                Lines.RemoveAt(i);
            }
        }
    }

    private void Add(OutputLineViewModel line)
    {
        Lines.Add(line);
        while (Lines.Count > MaxLines)
        {
            Lines.RemoveAt(0);
        }
    }
}
