using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Editor.CodeView;
using NetPrints.Editor.ErrorList;
using NetPrints.Editor.Events;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Inspectors;
using NetPrints.Editor.Main;
using NetPrints.Editor.ModelSync;
using NetPrints.Editor.Shell;
using NetPrints.Editor.UndoRedo;
using NetPrints.Editor.Variables;
using NetPrints.Graph;
using NetPrints.Serialization;
using NetPrints.Translator;

namespace NetPrints.Editor.ClassEditor;

/// <summary>Which inspector the class editor shows on the right (PAR-33).</summary>
public enum InspectorKind
{
    /// <summary>The class inspector (name, namespace, visibility, modifiers).</summary>
    Class,

    /// <summary>The variable inspector, for <see cref="ClassEditorViewModel.SelectedVariable"/>.</summary>
    Variable,

    /// <summary>The method inspector, for <see cref="ClassEditorViewModel.SelectedMethod"/>.</summary>
    Method,
}

/// <summary>
/// View model of a class editor window (PAR-22..37, PAR-60).
/// </summary>
public sealed partial class ClassEditorViewModel : ObservableObject, IRecipient<OpenGraphMessage>, IRecipient<NavigateToNodeMessage>, IRecipient<SelectInspectorMessage>, IDisposable
{
    internal static readonly IReadOnlyList<MemberVisibility> Visibilities =
    [
        MemberVisibility.Internal,
        MemberVisibility.Private,
        MemberVisibility.Protected,
        MemberVisibility.Public,
    ];


    /// <summary>Longest retained Output text, in characters (roughly 1 MB): older lines are
    /// dropped, oldest first, once exceeded.</summary>
    private const int MaxOutputChars = 1_000_000;

    private const string OutputTruncatedMarker = "… earlier output truncated …";

    /// <summary>How long a graph open must run before the busy overlay appears (batch D1):
    /// generous enough that a normal open on a small graph never flickers it.</summary>
    internal static readonly TimeSpan BusyIndicatorDelay = TimeSpan.FromMilliseconds(150);

    private readonly Queue<string> outputLines = new();
    private readonly Subject<string> outputReceived = new();
    private int outputCharCount;
    private bool outputTruncated;
    private IDisposable? outputFlush;
    private CancellationTokenSource? openGraphCts;

    /// <summary>The method, constructor or event graph currently loading through <see cref="OpenGraphThroughPipelineAsync"/>, or null (OWN-07).</summary>
    private object? pendingOpenTarget;

    /// <summary>
    /// Wraps <paramref name="cls"/>: builds its method/constructor/variable collections, subscribes
    /// to model and reflection-reload events, starts buffering process output, and computes the
    /// initial overridable methods and generated-code preview.
    /// </summary>
    /// <param name="cls">Class to edit.</param>
    /// <param name="context">Host services shared across the editor.</param>
    public ClassEditorViewModel(ClassGraph cls, EditorContext context)
    {
        ClassContext = new ClassContext(cls, context, UndoRedo);
        // RegisterAll, not Register: this implements more than one IRecipient<T>.
        Messenger.RegisterAll(this);
        VariablesPanel = new VariablesPanelViewModel(Services, Variables);
        ErrorList = new ErrorListViewModel(cls, context.CodeAnalysis, Messenger);
        ClassContext.MembersChanged += OnMembersChanged;
        ClassInspector.PropertyChanged += (_, e) => OnPropertyChanged(e.PropertyName);

        UndoRedo.Changed += (_, _) =>
        {
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
        };

        context.Reflection.Reloaded += OnReflectionReloaded;
        context.Processes.OutputReceived += OnProcessOutputReceived;

        // Coalesced: a chatty program (e.g. Console.WriteLine in a loop) would otherwise post one
        // dispatcher operation and rebuild the whole Output string per line, which is O(n^2) in the
        // total output and floods the UI thread.
        outputFlush = outputReceived
            .Buffer(TimeSpan.FromMilliseconds(50), Context.Scheduler)
            .Where(batch => batch.Count > 0)
            .Subscribe(batch => Context.Dispatcher.Post(() => AppendOutput(batch)));

        RefreshOverridableMethods();
    }

    /// <summary>The class's context, which the editor owns: its member view models, dirty tracking and class inspector.</summary>
    public ClassContext ClassContext { get; }

    /// <summary>The wrapped model class.</summary>
    public ClassGraph Class => ClassContext.Class;

    /// <summary>Host services shared across the editor.</summary>
    public EditorContext Context => ClassContext.Context;

    /// <summary>Messenger scoped to this class editor.</summary>
    public IMessenger Messenger => ClassContext.Messenger;

    /// <summary>Undo/redo history of this class editor.</summary>
    public UndoRedoStack UndoRedo { get; } = new();

    /// <summary>Narrow services shared with child view models that must not depend on this class editor directly (FR-038).</summary>
    public ClassEditorServices Services => ClassContext.Services;

    /// <summary>The project the class belongs to, or <see langword="null"/> if it has not been added to one.</summary>
    public Project? Project => Class.Project;

    /// <summary>Gets or sets how the window finds the open project's session, which owns every compile and run; null when the window runs without one.</summary>
    public Func<ProjectSessionViewModel?>? SessionSource { get; set; }

    /// <summary>View models for <see cref="Class"/>'s methods.</summary>
    public ObservableViewModelCollection<MethodViewModel, MethodGraph> Methods => ClassContext.Methods;

    /// <summary>View models for <see cref="Class"/>'s constructors.</summary>
    public ObservableViewModelCollection<MethodViewModel, ConstructorGraph> Constructors => ClassContext.Constructors;

    /// <summary>View models for <see cref="Class"/>'s variables.</summary>
    public ObservableViewModelCollection<MemberVariableViewModel, Variable> Variables => ClassContext.Variables;

    /// <summary>View models for <see cref="Class"/>'s event graphs (US4).</summary>
    public ObservableViewModelCollection<EventGraphViewModel, EventGraph> EventGraphs => ClassContext.EventGraphs;

    /// <summary>The Variables panel's "Class" and "Method: &lt;name&gt;" groups (FR-030, US5).</summary>
    public VariablesPanelViewModel VariablesPanel { get; }

    /// <summary>The class inspector shown for the class.</summary>
    public ClassInspectorViewModel ClassInspector => ClassContext.ClassInspector;

    /// <summary>The read-only C# code view of the class inspector (US6, FR-031..035).</summary>
    public CodeViewViewModel CodeView => ClassContext.CodeView;

    /// <summary>The class editor's Errors tab (US6, FR-032, FR-034).</summary>
    public ErrorListViewModel ErrorList { get; }

    /// <summary>Runs the registered commands for the window's key bindings; null until the main window attaches it.</summary>
    [ObservableProperty]
    public partial CommandInvoker? Commands { get; set; }

    /// <summary>The graph shown in the canvas, or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedMethodInList), nameof(SelectedConstructorInList), nameof(SelectedEventGraphInList))]
    public partial NodeGraphViewModel? OpenedGraph { get; set; }

    /// <summary>
    /// The Methods list's own highlight (R2-16, OWN-07b): a one-way projection of
    /// <see cref="OpenedGraph"/>, not a second write target sharing <see cref="SelectedMethod"/> with
    /// <see cref="SelectedConstructorInList"/>. Opening a constructor or an event graph clears this
    /// (and <see cref="SelectedMethod"/> stays whatever the inspector still shows), instead of both
    /// the Methods and Constructors lists — or a list and an event graph's row — staying highlighted
    /// together.
    /// </summary>
    public MethodViewModel? SelectedMethodInList => Methods.FirstOrDefault(m => m.Graph == OpenedGraph?.Graph);

    /// <summary>The Constructors list's own highlight (R2-16): see <see cref="SelectedMethodInList"/>.</summary>
    public MethodViewModel? SelectedConstructorInList => Constructors.FirstOrDefault(m => m.Graph == OpenedGraph?.Graph);

    /// <summary>The Event graphs list's own highlight (OWN-07b): see <see cref="SelectedMethodInList"/>.</summary>
    public EventGraphViewModel? SelectedEventGraphInList => EventGraphs.FirstOrDefault(g => g.Graph == OpenedGraph?.Graph);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowClassInspector), nameof(ShowVariableInspector), nameof(ShowMethodInspector))]
    public partial InspectorKind Inspector { get; set; } = InspectorKind.Class;

    /// <summary>Whether the class inspector should be shown.</summary>
    public bool ShowClassInspector => Inspector == InspectorKind.Class;

    /// <summary>Whether the variable inspector should be shown (a variable is also selected).</summary>
    public bool ShowVariableInspector => Inspector == InspectorKind.Variable && SelectedVariable is not null;

    /// <summary>Whether the method inspector should be shown (a method is also selected).</summary>
    public bool ShowMethodInspector => Inspector == InspectorKind.Method && SelectedMethod is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowVariableInspector))]
    public partial MemberVariableViewModel? SelectedVariable { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowMethodInspector))]
    public partial MethodViewModel? SelectedMethod { get; set; }

    /// <summary>Whether a graph is still opening past <see cref="BusyIndicatorDelay"/> (batch D1):
    /// drives the "Opening &lt;name&gt;…" overlay. Never true for an open that finishes quickly.</summary>
    [ObservableProperty]
    public partial bool IsOpeningGraph { get; set; }

    /// <summary>Name shown by the busy overlay while <see cref="IsOpeningGraph"/> is true.</summary>
    [ObservableProperty]
    public partial string? OpeningGraphName { get; set; }

    /// <summary>
    /// stdout/stderr of every program Run has started (PAR-10), across every open class window
    /// (they all show the same log): the Output tab, so the console output is visible on every
    /// platform instead of only on the editor's own terminal.
    /// </summary>
    [ObservableProperty]
    public partial string Output { get; set; } = "";

    /// <summary>Index of the selected tab in the errors/output panel; switches to Output when a run starts.</summary>
    [ObservableProperty]
    public partial int SelectedBottomTab { get; set; }

    /// <summary>Methods of the base types that can be overridden (PAR-26).</summary>
    [ObservableProperty]
    public partial IReadOnlyList<MethodSpecifier> OverridableMethods { get; set; } = [];

    /// <summary>Selection of the "Override a method" chooser; resets after use (PAR-26).</summary>
    [ObservableProperty]
    public partial MethodSpecifier? SelectedOverride { get; set; }

    /// <summary>Window title (PAR-22).</summary>
    public string Title => Class.Name ?? "";

    /// <summary>The class name with its namespace (the window's automation name).</summary>
    public string FullName => Class.FullName ?? "";

    /// <summary>The visibility values offered by the class's visibility chooser.</summary>
    public IReadOnlyList<MemberVisibility> PossibleVisibilities => Visibilities;

    /// <summary>The class's name, without namespace.</summary>
    public string Name { get => ClassInspector.Name; set => ClassInspector.Name = value; }

    /// <summary>The class's namespace.</summary>
    public string Namespace { get => ClassInspector.Namespace; set => ClassInspector.Namespace = value; }

    /// <summary>The class's visibility.</summary>
    public MemberVisibility Visibility { get => ClassInspector.Visibility; set => ClassInspector.Visibility = value; }

    /// <summary>The class's modifiers.</summary>
    public ClassModifiers Modifiers { get => ClassInspector.Modifiers; set => ClassInspector.Modifiers = value; }

    /// <summary>Whether <see cref="ClassModifiers.Sealed"/> is set.</summary>
    public bool IsSealed { get => ClassInspector.IsSealed; set => ClassInspector.IsSealed = value; }

    /// <summary>Whether <see cref="ClassModifiers.Abstract"/> is set.</summary>
    public bool IsAbstract { get => ClassInspector.IsAbstract; set => ClassInspector.IsAbstract = value; }

    /// <summary>Whether <see cref="ClassModifiers.Static"/> is set.</summary>
    public bool IsStatic { get => ClassInspector.IsStatic; set => ClassInspector.IsStatic = value; }

    /// <summary>Whether <see cref="ClassModifiers.Partial"/> is set.</summary>
    public bool IsPartial { get => ClassInspector.IsPartial; set => ClassInspector.IsPartial = value; }

    private void OnReflectionReloaded(object? sender, EventArgs e) => RefreshOverridableMethods();

    private void RefreshOverridableMethods()
    {
        // Filled on Reloaded once the host has loaded.
        OverridableMethods = Context.Reflection.IsLoaded
            ? Class.AllBaseTypes.SelectMany(Context.Reflection.Provider.GetOverridableMethodsForType).ToList()
            : [];
    }

    partial void OnSelectedOverrideChanged(MethodSpecifier? value)
    {
        if (value is null)
        {
            return;
        }

        CreateOverride(value);

        // Reset the chooser after the selection change has been processed by the view.
        Context.Dispatcher.Post(() => SelectedOverride = null);
    }

    partial void OnOpenedGraphChanged(NodeGraphViewModel? oldValue, NodeGraphViewModel? newValue)
    {
        VariablesPanel.OnOpenedGraphChanged(newValue?.Graph as ExecutionGraph);
    }

    /// <summary>Disposes the current <see cref="OpenedGraph"/> (the property's only setter) and replaces it with a new one.</summary>
    private void ReplaceOpenedGraph(NodeGraphViewModel? replacement)
    {
        OpenedGraph?.Dispose();
        OpenedGraph = replacement;
    }

    /// <summary>Opens a graph in the canvas.</summary>
    public void OpenGraph(NodeGraph graph) => ReplaceOpenedGraph(new NodeGraphViewModel(graph, Services));

    /// <summary>
    /// Cancels a still-loading <see cref="OpenMethodAsync"/> (R2-02): called by every other way to
    /// change the canvas, so a method whose load finishes late can never override a navigation the
    /// user made to something else in the meantime. Clears <see cref="pendingOpenTarget"/> immediately
    /// (F-03) rather than waiting for the cancelled load's own <c>finally</c>, which can run hundreds
    /// of milliseconds later and, until then, would drop a re-click on that same item.
    /// </summary>
    private void CancelPendingOpen()
    {
        openGraphCts?.Cancel();
        pendingOpenTarget = null;
    }

    void IRecipient<OpenGraphMessage>.Receive(OpenGraphMessage message)
    {
        CancelPendingOpen();
        OpenGraph(message.Graph);
    }

    /// <summary>
    /// Opens the graph <see cref="NavigateToNodeMessage.GraphKey"/> resolves to (if not already
    /// open) and, when known, reveals <see cref="NavigateToNodeMessage.NodeId"/> (FR-034, ED-T03,
    /// OWN-04): a diagnostic with no node mapping still opens the graph. Does nothing when the key
    /// does not resolve (the class changed since the diagnostic was reported).
    /// </summary>
    void IRecipient<NavigateToNodeMessage>.Receive(NavigateToNodeMessage message)
    {
        if (GraphKeys.Resolve(Class, message.GraphKey) is not { } graph)
        {
            return;
        }

        if (OpenedGraph is null || OpenedGraph.Graph != graph)
        {
            CancelPendingOpen();
            OpenGraph(graph);
        }

        if (message.NodeId is { } nodeId)
        {
            OpenedGraph?.RevealNode(nodeId);
        }
    }

    /// <summary>
    /// Shows the inspector for a variable or method selected from its own list entry (PAR-24, 29):
    /// <see cref="MemberVariableViewModel"/> sends this instead of calling back into this class editor
    /// directly (FR-038).
    /// </summary>
    void IRecipient<SelectInspectorMessage>.Receive(SelectInspectorMessage message)
    {
        SelectedVariable = message.Target;
        Inspector = InspectorKind.Variable;
    }

    // Model changes, including undo and redo, can remove what the inspector or the canvas shows.
    // The editor reacts to the model instead of each command cleaning up after itself.

    private void OnMembersChanged(object? sender, EventArgs e)
    {
        DropDetachedState();

        // A member's own VM instance can be replaced (eg. undo/redo) without OpenedGraph changing:
        // re-evaluate which list row (if any) that VM re-projects to (R2-16, OWN-07b).
        OnPropertyChanged(nameof(SelectedMethodInList));
        OnPropertyChanged(nameof(SelectedConstructorInList));
        OnPropertyChanged(nameof(SelectedEventGraphInList));
    }

    /// <summary>Graphs that belong to the class: its own graph, methods, constructors and variable graphs.</summary>
    private bool BelongsToClass(NodeGraph graph) =>
        graph == Class
        || (graph is MethodGraph method && Class.Methods.Contains(method))
        || (graph is ConstructorGraph constructor && Class.Constructors.Contains(constructor))
        || (graph is EventGraph eventGraph && Class.EventGraphs.Contains(eventGraph))
        || Class.Variables.Any(v => v.GetterMethod == graph || v.SetterMethod == graph || v.TypeGraph == graph);

    private void DropDetachedState()
    {
        if (SelectedVariable is not null && !Class.Variables.Contains(SelectedVariable.Variable))
        {
            SelectedVariable = null;
            if (Inspector == InspectorKind.Variable)
            {
                Inspector = InspectorKind.Class;
            }
        }

        if (SelectedMethod is not null && !BelongsToClass(SelectedMethod.Graph))
        {
            SelectedMethod = null;
            if (Inspector == InspectorKind.Method)
            {
                Inspector = InspectorKind.Class;
            }
        }

        if (OpenedGraph is not null && !BelongsToClass(OpenedGraph.Graph))
        {
            ReplaceOpenedGraph(null);
        }
    }

    private ClassTranslator NewTranslator() => new(Context.Extensions.Current.Translation);

    // Toolbar (PAR-23)

    /// <summary>Class button: shows the class inspector and opens the class graph.</summary>
    [RelayCommand]
    private void ShowClass()
    {
        CancelPendingOpen();
        Inspector = InspectorKind.Class;
        OpenGraph(Class);
    }

    /// <summary>Saves every edited class of the whole project (not just this one) (document-format.md §2.8).
    /// A class that fails to translate is reported through <see cref="Core.Project.LastDiagnostics"/> (the
    /// Errors tab), not a dialog: its graph is still saved and the rest of the project's dirty classes
    /// still save too (R1-01).</summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (Project is not { } project)
        {
            return;
        }

        try
        {
            ProjectSaveResult result = await Context.Persistence.SaveAsync(project, cls => RenderGenerated(project, cls), CancellationToken.None);
            if (result.Diagnostics.Count > 0)
            {
                project.LastDiagnostics = new ObservableRangeCollection<CodeDiagnostic>(result.Diagnostics);
            }
        }
        catch (Exception ex)
        {
            await Context.Dialogs.ShowErrorAsync("Failed to save project", ex.ToString());
        }
    }

    /// <summary>Renders a class's generated C# file the same way a build would (project-system.md §3).</summary>
    private string RenderGenerated(Project project, ClassGraph cls) =>
        NetPrints.Generation.GraphCodeGenerator.RenderFile(NewTranslator().Translate(cls), Path.GetFileName(project.GetGraphFilePath(cls)));

    /// <summary>Compiles the whole project through the session (PAR-09).</summary>
    [RelayCommand]
    private Task CompileAsync() => Project is { CanCompile: true } && SessionSource?.Invoke() is { } session ? session.CompileAsync() : Task.CompletedTask;

    [RelayCommand]
    private Task RunAsync()
    {
        if (Project is not { CanCompileAndRun: true } || SessionSource?.Invoke() is not { } session)
        {
            return Task.CompletedTask;
        }

        SelectedBottomTab = 1; // Output, once per run (not re-forced on every line after it).
        return session.RunAsync();
    }

    [RelayCommand]
    private void ClearOutput()
    {
        outputLines.Clear();
        outputCharCount = 0;
        outputTruncated = false;
        Output = "";
    }

    private void OnProcessOutputReceived(string line) => outputReceived.OnNext(line);

    /// <summary>
    /// Appends a batch of output lines and rebuilds <see cref="Output"/> once (not per line: O(n)
    /// in the batch, not O(n^2) in the total output), dropping the oldest lines past
    /// <see cref="MaxOutputChars"/>.
    /// </summary>
    private void AppendOutput(IList<string> lines)
    {
        foreach (string line in lines)
        {
            outputLines.Enqueue(line);
            outputCharCount += line.Length + 1;
        }

        while (outputCharCount > MaxOutputChars && outputLines.Count > 1)
        {
            outputCharCount -= outputLines.Dequeue().Length + 1;
            outputTruncated = true;
        }

        Output = outputTruncated
            ? OutputTruncatedMarker + Environment.NewLine + string.Join(Environment.NewLine, outputLines)
            : string.Join(Environment.NewLine, outputLines);
    }

    // Lists (PAR-24..30)

    /// <summary>Creates a method named Method, Method1, ... with connected entry and return nodes and opens it.</summary>
    [RelayCommand]
    private void CreateMethod()
    {
        MethodGraph method = ClassContext.CreateMethod();
        CancelPendingOpen();
        OpenGraph(method);
    }

    /// <summary>Creates a public constructor and opens it (PAR-28).</summary>
    [RelayCommand]
    private void CreateConstructor()
    {
        ConstructorGraph constructor = ClassContext.CreateConstructor();
        CancelPendingOpen();
        OpenGraph(constructor);
    }

    /// <summary>Creates and opens an override of a base method (PAR-26).</summary>
    public void CreateOverride(MethodSpecifier methodSpecifier)
    {
        MethodGraph? method = ClassContext.CreateOverride(methodSpecifier);
        if (method is not null)
        {
            CancelPendingOpen();
            OpenGraph(method);
        }
    }

    /// <summary>Creates a variable named Variable, Variable1, ... of type object (undoable, PAR-30).</summary>
    [RelayCommand]
    private void CreateVariable()
    {
        ClassContext.CreateVariable();
    }

    /// <summary>Creates an event graph named EventGraph, EventGraph1, ... (undoable, US4) and opens it.</summary>
    [RelayCommand]
    private void CreateEventGraph()
    {
        EventGraph eventGraph = ClassContext.CreateEventGraph();
        CancelPendingOpen();
        OpenGraph(eventGraph);
    }

    /// <summary>
    /// Selects and opens an event graph's canvas (US4, OWN-07), through the same open pipeline as
    /// <see cref="OpenMethodAsync"/>: a single click opens it immediately, and last click wins whether
    /// the previous one was a method, a constructor or another event graph.
    /// </summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task OpenEventGraphAsync(EventGraphViewModel? eventGraph) =>
        eventGraph is null ? Task.CompletedTask : OpenGraphThroughPipelineAsync(eventGraph, eventGraph.Graph, eventGraph.Name);

    /// <summary>Removes an event graph (undoable, US4); clears the canvas when it shows it.</summary>
    [RelayCommand]
    private void RemoveEventGraph(EventGraphViewModel? eventGraph)
    {
        if (eventGraph is null)
        {
            return;
        }

        ClassContext.RemoveEventGraph(eventGraph.Graph);
    }

    /// <summary>
    /// Selects and opens a method or constructor's graph (PAR-24), in one command: a single click on
    /// its list entry both shows the method inspector and opens the canvas. Previously a single click
    /// only selected it, requiring a second, discoverable double-click gesture the owner reported as
    /// "the graph doesn't open" (batch D1). The open itself goes through <see cref="OpenGraphThroughPipelineAsync"/>,
    /// shared with <see cref="OpenEventGraphAsync"/> (OWN-07): last click wins whether the previous one
    /// was a method or an event graph.
    /// </summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task OpenMethodAsync(MethodViewModel? method)
    {
        if (method is null)
        {
            return Task.CompletedTask;
        }

        SelectedMethod = method;
        Inspector = InspectorKind.Method;
        return OpenGraphThroughPipelineAsync(method, method.Graph, method.Name);
    }

    /// <summary>
    /// Shared open pipeline for every list row that opens a graph (methods, constructors, event
    /// graphs; PAR-24, US4, OWN-07): invoked directly by each item kind's own command, not through a
    /// two-way <c>SelectedItem</c> binding's changed hook, so an item that stays a list's
    /// <c>SelectedItem</c> across an unrelated canvas change (Create Event Graph, Class, another list's
    /// item) still reopens on its next click — exactly the "Open: switch away, then reopen" case
    /// <c>EventGraphTests</c> covers, and the owner's "add event graph, click it, click Main" sequence.
    /// A click on the item already loading is ignored; a click on the already-open item with nothing
    /// pending is a no-op; a click on a different item (of any kind) cancels the pending one — last
    /// click wins (R2-02), generalized past methods alone. Reflection-backed overload lookups for the
    /// graph's nodes are warmed on a background thread first (AGENTS.md: heavy work off the UI
    /// thread), and <see cref="IsOpeningGraph"/> only turns on if that takes longer than
    /// <see cref="BusyIndicatorDelay"/>. <c>AllowConcurrentExecutions</c> on each caller's command keeps
    /// its <c>CanExecute</c> true while a previous click is still opening: without it,
    /// <c>InvokeCommandAction</c> silently drops a click made during a load, and the supersede logic
    /// below is never reached from the UI.
    /// </summary>
    private async Task OpenGraphThroughPipelineAsync(object item, NodeGraph graph, string name)
    {
        if (Equals(item, pendingOpenTarget) || (pendingOpenTarget is null && OpenedGraph?.Graph == graph))
        {
            return;
        }

        pendingOpenTarget = item;

        // F-04: install the new CTS synchronously before any await, so an overlapping execution
        // (AllowConcurrentExecutions) can never observe or dispose a CTS that is still live. Only the
        // previous one, captured into a local, is cancelled and disposed below.
        CancellationTokenSource? previous = openGraphCts;
        var cts = new CancellationTokenSource();
        openGraphCts = cts;
        CancellationToken token = cts.Token;

        if (previous is not null)
        {
            await previous.CancelAsync();
            previous.Dispose();
        }

        IDisposable indicator = Context.Scheduler.Schedule(BusyIndicatorDelay, () =>
        {
            OpeningGraphName = name;
            IsOpeningGraph = true;
        });

        try
        {
            await WarmOverloadsAsync(graph, token);
            token.ThrowIfCancellationRequested();
            OpenGraph(graph);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a later click; OpenedGraph is untouched.
        }
        finally
        {
            indicator.Dispose();
            IsOpeningGraph = false;
            OpeningGraphName = null;

            // Own the clear only if no later execution has since installed its own CTS (F-03):
            // comparing against the captured item would also match a newer execution opening the
            // same item again while this cancelled one's finally is still pending.
            if (ReferenceEquals(openGraphCts, cts))
            {
                pendingOpenTarget = null;
            }
        }
    }

    /// <summary>
    /// Pre-resolves every <see cref="CallMethodNode"/>/<see cref="ConstructorNode"/> overload list of
    /// <paramref name="graph"/> on a background thread, so the reflection provider's memoized cache is
    /// already warm when <see cref="NetPrints.Editor.Graph.Nodes.NodeViewModel"/> recomputes the same
    /// overloads synchronously while building the canvas (AGENTS.md: heavy work off the UI thread, not
    /// papered over with a delay).
    /// </summary>
    private async Task WarmOverloadsAsync(NodeGraph graph, CancellationToken cancellationToken)
    {
        if (!Context.Reflection.IsLoaded)
        {
            return;
        }

        List<MethodSpecifier> methods = [];
        List<TypeSpecifier> constructorTypes = [];
        foreach (var node in graph.Nodes)
        {
            switch (node)
            {
                case CallMethodNode { MethodSpecifier: { } method }:
                    methods.Add(method);
                    break;
                case ConstructorNode { ConstructorSpecifier: { } ctor }:
                    constructorTypes.Add(ctor.DeclaringType);
                    break;
            }
        }

        if (methods.Count == 0 && constructorTypes.Count == 0)
        {
            return;
        }

        var provider = Context.Reflection.Provider;
        await Task.Run(() =>
        {
            foreach (var method in methods)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _ = provider.GetPublicMethodOverloads(method).Count();
            }

            foreach (var type in constructorTypes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _ = provider.GetConstructors(type).Count();
            }
        }, cancellationToken);
    }

    /// <summary>Removes a method or constructor; clears the inspector and canvas when they show it (PAR-24, 27).</summary>
    [RelayCommand]
    private void RemoveMethod(MethodViewModel? method)
    {
        if (method is null)
        {
            return;
        }

        ClassContext.RemoveMethod(method.Graph);
    }

    // Keyboard (PAR-37)

    /// <summary>Deletes the selected nodes except method entry, class return and main return nodes.</summary>
    [RelayCommand]
    private void DeleteSelectedNodes() => OpenedGraph?.DeleteSelectedNodes();

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => UndoRedo.Undo();

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => UndoRedo.Redo();

    private bool CanUndo() => UndoRedo.CanUndo;

    private bool CanRedo() => UndoRedo.CanRedo;

    /// <summary>
    /// Stops output buffering, disposes <see cref="CodeView"/> and <see cref="ErrorList"/>,
    /// unsubscribes from every model and host event, clears <see cref="OpenedGraph"/>, and disposes
    /// the method/constructor/variable collections (and, through them, every member view model).
    /// </summary>
    public void Dispose()
    {
        ErrorList.Dispose();
        outputFlush?.Dispose();
        outputReceived.Dispose();
        openGraphCts?.Cancel();
        openGraphCts?.Dispose();

        Context.Reflection.Reloaded -= OnReflectionReloaded;
        Context.Processes.OutputReceived -= OnProcessOutputReceived;
        ClassContext.MembersChanged -= OnMembersChanged;
        Messenger.UnregisterAll(this);
        ReplaceOpenedGraph(null);
        VariablesPanel.Dispose();
        ClassContext.Dispose();
    }
}
