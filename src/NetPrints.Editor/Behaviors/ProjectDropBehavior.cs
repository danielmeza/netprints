using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Xaml.Interactivity;

namespace NetPrints.Editor.Behaviors;

/// <summary>
/// Accepts files and folders dropped on the attached control and runs <see cref="Command"/> with their local paths
/// (an <see cref="IReadOnlyList{T}"/> of <see cref="string"/>). The control must allow drops (<c>DragDrop.AllowDrop</c>).
/// A drop that holds no local file or folder, such as dragged text, is ignored.
/// </summary>
public sealed class ProjectDropBehavior : StyledElementBehavior<Control>
{
    /// <summary>Identifies <see cref="Command"/>.</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<ProjectDropBehavior, ICommand?>(nameof(Command));

    /// <summary>Gets or sets the command run with the dropped paths as its parameter.</summary>
    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject?.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AssociatedObject?.AddHandler(DragDrop.DropEvent, OnDrop);
    }

    /// <inheritdoc/>
    protected override void OnDetaching()
    {
        AssociatedObject?.RemoveHandler(DragDrop.DragOverEvent, OnDragOver);
        AssociatedObject?.RemoveHandler(DragDrop.DropEvent, OnDrop);
        base.OnDetaching();
    }

    private static List<string> LocalPaths(DragEventArgs e) =>
        [.. (e.DataTransfer.TryGetFiles() ?? []).Select(item => item.TryGetLocalPath()).OfType<string>()];

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (LocalPaths(e).Count > 0)
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        List<string> paths = LocalPaths(e);
        if (paths.Count == 0 || Command is not { } command)
        {
            return;
        }

        e.Handled = true;
        if (command.CanExecute(paths))
        {
            command.Execute(paths);
        }
    }
}
