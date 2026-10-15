using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Input;
using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.ProjectTree;
using NetPrints.Editor.Variables;

namespace NetPrints.Editor.Graph;

/// <summary>
/// Drag &amp; drop from the class editor lists onto the graph (PAR-56, PAR-57).
/// </summary>
public static class GraphDragDrop
{
    /// <summary>A method or constructor dragged from the lists.</summary>
    public static readonly DataFormat<MethodViewModel> MethodFormat = DataFormat.CreateInProcessFormat<MethodViewModel>("netprints-graph");

    /// <summary>A variable dragged from the variable list.</summary>
    public static readonly DataFormat<MemberVariableViewModel> VariableFormat = DataFormat.CreateInProcessFormat<MemberVariableViewModel>("netprints-variable");

    /// <summary>A local variable dragged from the Variables panel's Method group (US5, sub-phase H).</summary>
    public static readonly DataFormat<LocalVariableViewModel> LocalVariableFormat = DataFormat.CreateInProcessFormat<LocalVariableViewModel>("netprints-local-variable");

    /// <summary>Starts a drag of a method or constructor, tagged with <see cref="MethodFormat"/>.</summary>
    /// <param name="e">The pointer-pressed event that starts the drag.</param>
    /// <param name="method">Method or constructor being dragged.</param>
    public static Task StartDragAsync(PointerPressedEventArgs e, MethodViewModel method) =>
        StartDragAsync(e, DataTransferItem.Create(MethodFormat, method));

    /// <summary>Starts a drag of a variable, tagged with <see cref="VariableFormat"/>.</summary>
    /// <param name="e">The pointer-pressed event that starts the drag.</param>
    /// <param name="variable">Variable being dragged.</param>
    public static Task StartDragAsync(PointerPressedEventArgs e, MemberVariableViewModel variable) =>
        StartDragAsync(e, DataTransferItem.Create(VariableFormat, variable));

    /// <summary>Starts a drag of a local variable, tagged with <see cref="LocalVariableFormat"/> (US5).</summary>
    /// <param name="e">The pointer-pressed event that starts the drag.</param>
    /// <param name="variable">Local variable being dragged.</param>
    public static Task StartDragAsync(PointerPressedEventArgs e, LocalVariableViewModel variable) =>
        StartDragAsync(e, DataTransferItem.Create(LocalVariableFormat, variable));

    /// <summary>A variable dragged from a project tree row, tagged with <see cref="TreeVariableFormat"/>.</summary>
    public static readonly DataFormat<Variable> TreeVariableFormat = DataFormat.CreateInProcessFormat<Variable>("netprints-tree-variable");

    /// <summary>
    /// Starts a drag of a project tree row: a method or constructor travels as a <see cref="MethodFormat"/> wrapper that lives
    /// until the drop is handled, a variable as <see cref="TreeVariableFormat"/>. Other rows start no drag.
    /// </summary>
    /// <param name="e">The pointer-pressed event that starts the drag.</param>
    /// <param name="row">The tree row being dragged.</param>
    public static async Task StartDragAsync(PointerPressedEventArgs e, ProjectTreeItemViewModel row)
    {
        switch (row.Model)
        {
            case ExecutionGraph graph when row.Kind is TreeItemKind.Method or TreeItemKind.Constructor:
                using (var method = new MethodViewModel(graph))
                {
                    await StartDragAsync(e, DataTransferItem.Create(MethodFormat, method));
                }

                break;
            case Variable variable when row.Kind is TreeItemKind.Variable:
                await StartDragAsync(e, DataTransferItem.Create(TreeVariableFormat, variable));
                break;
        }
    }

    private static async Task StartDragAsync(PointerPressedEventArgs e, DataTransferItem item)
    {
        using var data = new DataTransfer();
        data.Add(item);
        await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Copy);
    }
}

/// <summary>
/// Starts a list-to-canvas drag only after the pointer moved a few pixels with the left button
/// held, so clicks and double clicks on the list entries keep working.
/// </summary>
public sealed class DragSourceHelper
{
    private const double Threshold = 4;
    private PointerPressedEventArgs? pressed;
    private Point start;
    private object? payload;

    /// <summary>
    /// Records a left-button press as a possible drag start; a later <see cref="Moved"/> past the
    /// threshold starts the drag. Ignored if the button pressed is not the left one, or on other than
    /// the first click of a click sequence (so double clicks are not treated as a drag start).
    /// </summary>
    /// <param name="e">The pointer-pressed event.</param>
    /// <param name="relativeTo">Visual the pointer position is measured relative to.</param>
    /// <param name="item">The <see cref="MethodViewModel"/>, <see cref="MemberVariableViewModel"/> or <see cref="LocalVariableViewModel"/> or <see cref="ProjectTreeItemViewModel"/> that would be dragged.</param>
    public void Pressed(PointerPressedEventArgs e, Visual relativeTo, object item)
    {
        if (e.GetCurrentPoint(relativeTo).Properties.IsLeftButtonPressed && e.ClickCount == 1)
        {
            pressed = e;
            start = e.GetPosition(relativeTo);
            payload = item;
        }
    }

    // async void because it is an event handler. An exception from the drag reaches
    // Dispatcher.UIThread.UnhandledException, where UnhandledExceptionHandler shows it in the error
    // dialog (covered by UnhandledExceptionTests.AsyncVoidHandlerExceptionIsReported).
    /// <summary>
    /// Starts the drag (via <see cref="GraphDragDrop.StartDragAsync(PointerPressedEventArgs, MethodViewModel)"/>
    /// or the variable overload) once the pointer has moved past the threshold from the recorded
    /// <see cref="Pressed"/> position while the left button is still held. Does nothing if no press was
    /// recorded, and clears the recorded press if the left button was released.
    /// </summary>
    /// <param name="e">The pointer-moved event.</param>
    /// <param name="relativeTo">Visual the pointer position is measured relative to; must match the one passed to <see cref="Pressed"/>.</param>
    [SuppressMessage("Usage", "VSTHRD100", Justification = "ADR-0003: async void is deliberate, an event handler (see the comment above).")]
    public async void Moved(PointerEventArgs e, Visual relativeTo)
    {
        if (pressed is null || payload is null)
        {
            return;
        }

        if (!e.GetCurrentPoint(relativeTo).Properties.IsLeftButtonPressed)
        {
            pressed = null;
            return;
        }

        var position = e.GetPosition(relativeTo);
        if (Math.Abs(position.X - start.X) < Threshold && Math.Abs(position.Y - start.Y) < Threshold)
        {
            return;
        }

        var args = pressed;
        var item = payload;
        pressed = null;
        payload = null;

        switch (item)
        {
            case MethodViewModel method:
                await GraphDragDrop.StartDragAsync(args, method);
                break;
            case MemberVariableViewModel variable:
                await GraphDragDrop.StartDragAsync(args, variable);
                break;
            case LocalVariableViewModel local:
                await GraphDragDrop.StartDragAsync(args, local);
                break;
            case ProjectTreeItemViewModel row:
                await GraphDragDrop.StartDragAsync(args, row);
                break;
        }
    }

    /// <summary>Clears a recorded press without starting a drag (the pointer was released before moving past the threshold).</summary>
    public void Released() => pressed = null;
}
