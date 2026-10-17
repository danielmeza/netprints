using System.Diagnostics.CodeAnalysis;
using NetPrints.Core;
using NetPrints.Projects;
using NetPrints.Reflection;

namespace NetPrints.Extensibility.Editor;

/// <summary>The open project as an extension sees it.</summary>
[Experimental(ExperimentalApiIds.EditorContributions, UrlFormat = ExperimentalApiIds.UrlFormat)]
public interface IProjectSession
{
    /// <summary>Gets the project.</summary>
    Project Project { get; }

    /// <summary>Gets the C# target of the project.</summary>
    CSharpTarget Target { get; }

    /// <summary>Gets the reflection provider of the project.</summary>
    IReflectionProvider Reflection { get; }

    /// <summary>Gets the graph being edited, or <see langword="null"/> when no graph is open.</summary>
    IGraphEditor? ActiveGraph { get; }

    /// <summary>Gets the calling extension's session-scoped services.</summary>
    IServiceProvider Services { get; }
}

/// <summary>Everything a command handler may look at, built for each invocation.</summary>
[Experimental(ExperimentalApiIds.EditorContributions, UrlFormat = ExperimentalApiIds.UrlFormat)]
public interface ICommandContext
{
    /// <summary>Gets the shell.</summary>
    IEditorShell Shell { get; }

    /// <summary>Gets the open project session, or <see langword="null"/> while the start page shows.</summary>
    IProjectSession? Session { get; }

    /// <summary>Gets the graph being edited, or <see langword="null"/> when the active document is not a graph.</summary>
    IGraphEditor? ActiveGraph { get; }

    /// <summary>Gets the current selection.</summary>
    CommandSelection Selection { get; }

    /// <summary>Gets the command parameter, such as the id of a recent project, or <see langword="null"/>.</summary>
    object? Parameter { get; }

    /// <summary>Gets the scope the invocation comes from.</summary>
    CommandScope Scope { get; }

    /// <summary>Gets the dialogs.</summary>
    IDialogService Dialogs { get; }

    /// <summary>Gets the Output panel.</summary>
    IEditorOutput Output { get; }

    /// <summary>Gets the calling extension's own service provider.</summary>
    IServiceProvider Services { get; }
}

/// <summary>What a panel's view model factory may look at.</summary>
[Experimental(ExperimentalApiIds.EditorContributions, UrlFormat = ExperimentalApiIds.UrlFormat)]
public interface IPanelContext
{
    /// <summary>Gets the shell.</summary>
    IEditorShell Shell { get; }

    /// <summary>Gets the dialogs.</summary>
    IDialogService Dialogs { get; }

    /// <summary>Gets the Output panel.</summary>
    IEditorOutput Output { get; }

    /// <summary>Gets the calling extension's own service provider.</summary>
    IServiceProvider Services { get; }
}

/// <summary>What a start page tile's view model factory may look at.</summary>
[Experimental(ExperimentalApiIds.EditorContributions, UrlFormat = ExperimentalApiIds.UrlFormat)]
public interface IStartPageContext
{
    /// <summary>Gets the shell.</summary>
    IEditorShell Shell { get; }

    /// <summary>Gets the dialogs.</summary>
    IDialogService Dialogs { get; }

    /// <summary>Gets the Output panel.</summary>
    IEditorOutput Output { get; }

    /// <summary>Gets the calling extension's own service provider.</summary>
    IServiceProvider Services { get; }
}

/// <summary>What a status bar item may look at.</summary>
[Experimental(ExperimentalApiIds.EditorContributions, UrlFormat = ExperimentalApiIds.UrlFormat)]
public interface IStatusBarContext
{
    /// <summary>Gets the shell.</summary>
    IEditorShell Shell { get; }

    /// <summary>Gets the open project session, or <see langword="null"/> while the start page shows.</summary>
    IProjectSession? Session { get; }

    /// <summary>Gets the graph being edited, or <see langword="null"/> when the active document is not a graph.</summary>
    IGraphEditor? ActiveGraph { get; }

    /// <summary>Gets the calling extension's own service provider.</summary>
    IServiceProvider Services { get; }
}

/// <summary>What a settings page may look at.</summary>
[Experimental(ExperimentalApiIds.EditorContributions, UrlFormat = ExperimentalApiIds.UrlFormat)]
public interface ISettingsPageContext
{
    /// <summary>Gets the shell.</summary>
    IEditorShell Shell { get; }

    /// <summary>Gets the dialogs.</summary>
    IDialogService Dialogs { get; }

    /// <summary>Gets the calling extension's own service provider.</summary>
    IServiceProvider Services { get; }
}

/// <summary>What a dialog's view model factory may look at.</summary>
[Experimental(ExperimentalApiIds.EditorContributions, UrlFormat = ExperimentalApiIds.UrlFormat)]
public interface IDialogContext
{
    /// <summary>Gets the shell.</summary>
    IEditorShell Shell { get; }

    /// <summary>Gets the open project session, or <see langword="null"/> while the start page shows.</summary>
    IProjectSession? Session { get; }

    /// <summary>Gets the dialogs.</summary>
    IDialogService Dialogs { get; }

    /// <summary>Gets the calling extension's own service provider.</summary>
    IServiceProvider Services { get; }
}

/// <summary>The behaviour behind a command. Handlers are stateless: everything they need comes in the context.</summary>
[Experimental(ExperimentalApiIds.EditorContributions, UrlFormat = ExperimentalApiIds.UrlFormat)]
public interface ICommandHandler
{
    /// <summary>Whether the command can run in <paramref name="context"/>.</summary>
    /// <param name="context">The invocation context.</param>
    /// <returns><see langword="true"/> when <see cref="ExecuteAsync"/> may be called.</returns>
    bool CanExecute(ICommandContext context);

    /// <summary>Runs the command.</summary>
    /// <param name="context">The invocation context.</param>
    /// <param name="cancellationToken">Cancels a long-running command.</param>
    /// <returns>A task that completes when the command has finished.</returns>
    Task ExecuteAsync(ICommandContext context, CancellationToken cancellationToken);

    /// <summary>A label that replaces the descriptor's, such as "Undo Add node".</summary>
    /// <param name="context">The invocation context.</param>
    /// <returns>The label, or null to keep the descriptor's.</returns>
    string? DynamicLabel(ICommandContext context) => null;

    /// <summary>Whether the command is the chosen one of a group of choices, such as the active theme.</summary>
    /// <param name="context">The invocation context.</param>
    /// <returns>Whether it is chosen, or null when the command is not a choice (its menu entry has no mark).</returns>
    bool? IsChecked(ICommandContext context) => null;
}
