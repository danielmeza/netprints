using Avalonia;
using Avalonia.Input;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Variables;

namespace NetPrints.Editor.Graph;

/// <summary>
/// Drag &amp; drop from the class editor lists onto the graph (PAR-56, PAR-57).
/// </summary>
public static class GraphDragDrop
{
    /// <summary>A method or constructor dragged from the lists.</summary>
    public static readonly DataFormat<MethodVM> MethodFormat = DataFormat.CreateInProcessFormat<MethodVM>("netprints-graph");

    /// <summary>A variable dragged from the variable list.</summary>
    public static readonly DataFormat<MemberVariableVM> VariableFormat = DataFormat.CreateInProcessFormat<MemberVariableVM>("netprints-variable");

    /// <summary>Starts a drag of a method or constructor, tagged with <see cref="MethodFormat"/>.</summary>
    /// <param name="e">The pointer-pressed event that starts the drag.</param>
    /// <param name="method">Method or constructor being dragged.</param>
    public static Task StartDragAsync(PointerPressedEventArgs e, MethodVM method) =>
        StartDragAsync(e, DataTransferItem.Create(MethodFormat, method));

    /// <summary>Starts a drag of a variable, tagged with <see cref="VariableFormat"/>.</summary>
    /// <param name="e">The pointer-pressed event that starts the drag.</param>
    /// <param name="variable">Variable being dragged.</param>
    public static Task StartDragAsync(PointerPressedEventArgs e, MemberVariableVM variable) =>
        StartDragAsync(e, DataTransferItem.Create(VariableFormat, variable));

    private static async Task StartDragAsync(PointerPressedEventArgs e, DataTransferItem item)
    {
        var data = new DataTransfer();
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
    /// <param name="item">The <see cref="MethodVM"/> or <see cref="MemberVariableVM"/> that would be dragged.</param>
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
    /// Starts the drag (via <see cref="GraphDragDrop.StartDragAsync(PointerPressedEventArgs, MethodVM)"/>
    /// or the variable overload) once the pointer has moved past the threshold from the recorded
    /// <see cref="Pressed"/> position while the left button is still held. Does nothing if no press was
    /// recorded, and clears the recorded press if the left button was released.
    /// </summary>
    /// <param name="e">The pointer-moved event.</param>
    /// <param name="relativeTo">Visual the pointer position is measured relative to; must match the one passed to <see cref="Pressed"/>.</param>
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
            case MethodVM method:
                await GraphDragDrop.StartDragAsync(args, method);
                break;
            case MemberVariableVM variable:
                await GraphDragDrop.StartDragAsync(args, variable);
                break;
        }
    }

    /// <summary>Clears a recorded press without starting a drag (the pointer was released before moving past the threshold).</summary>
    public void Released() => pressed = null;
}
