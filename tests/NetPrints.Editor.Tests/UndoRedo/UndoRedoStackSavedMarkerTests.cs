using NetPrints.Editor.UndoRedo;

namespace NetPrints.Editor.Tests.UndoRedo;

/// <summary>The saved-state marker and the undo and redo names of <see cref="UndoRedoStack"/> (research R6).</summary>
public class UndoRedoStackSavedMarkerTests
{
    private static DelegateUndoableCommand Command(string name) => new(name, () => { }, () => { });

    [Fact]
    public void WithoutAMarkTheStackIsNotAtASavedState()
    {
        var stack = new UndoRedoStack();

        Assert.False(stack.IsAtSavedState);
    }

    [Fact]
    public void MarkSavedMakesTheCurrentStateSaved()
    {
        var stack = new UndoRedoStack();
        stack.Do(Command("a"));

        stack.MarkSaved();

        Assert.True(stack.IsAtSavedState);
    }

    [Fact]
    public void AnEditAfterMarkSavedIsNotSaved()
    {
        var stack = new UndoRedoStack();
        stack.MarkSaved();

        stack.Do(Command("a"));

        Assert.False(stack.IsAtSavedState);
    }

    [Fact]
    public void UndoingBackToTheMarkIsSavedAgain()
    {
        var stack = new UndoRedoStack();
        stack.Do(Command("a"));
        stack.MarkSaved();
        stack.Do(Command("b"));

        stack.Undo();

        Assert.True(stack.IsAtSavedState);
    }

    [Fact]
    public void UndoingPastTheMarkIsNotSavedAndRedoingReturnsToIt()
    {
        var stack = new UndoRedoStack();
        stack.Do(Command("a"));
        stack.MarkSaved();

        stack.Undo();
        Assert.False(stack.IsAtSavedState);

        stack.Redo();
        Assert.True(stack.IsAtSavedState);
    }

    [Fact]
    public void ANewEditAtTheSameDepthAfterUndoIsNotSaved()
    {
        var stack = new UndoRedoStack();
        stack.Do(Command("a"));
        stack.MarkSaved();
        stack.Undo();

        stack.Do(Command("other"));

        Assert.False(stack.IsAtSavedState);
    }

    [Fact]
    public void ClearForgetsTheSavedState()
    {
        var stack = new UndoRedoStack();
        stack.MarkSaved();

        stack.Clear();

        Assert.False(stack.IsAtSavedState);
    }

    [Fact]
    public void MarkSavedRaisesChangedSoTheCleanStateCanBeObserved()
    {
        var stack = new UndoRedoStack();
        int changed = 0;
        stack.Changed += (_, _) => changed++;

        stack.MarkSaved();

        Assert.Equal(1, changed);
    }

    [Fact]
    public void NamesComeFromTheCommandsOnTopOfEachStack()
    {
        var stack = new UndoRedoStack();
        Assert.Null(stack.UndoName);
        Assert.Null(stack.RedoName);

        stack.Do(Command("Add node"));
        stack.Do(Command("Move node"));
        Assert.Equal("Move node", stack.UndoName);
        Assert.Null(stack.RedoName);

        stack.Undo();
        Assert.Equal("Add node", stack.UndoName);
        Assert.Equal("Move node", stack.RedoName);

        stack.Undo();
        Assert.Null(stack.UndoName);
        Assert.Equal("Add node", stack.RedoName);
    }

    [Fact]
    public void ForgetSavedStateMakesTheMarkerUnreachable()
    {
        var stack = new UndoRedoStack();
        stack.Do(Command("a"));
        stack.MarkSaved();
        stack.ForgetSavedState();

        Assert.False(stack.IsAtSavedState);
        stack.Do(Command("b"));
        stack.Undo();

        Assert.False(stack.IsAtSavedState);
    }

    [Fact]
    public void MarkSavedAtACapturedPositionIgnoresEditsMadeAfterTheCapture()
    {
        var stack = new UndoRedoStack();
        stack.Do(Command("a"));
        SavePoint point = stack.CapturePosition();
        stack.Do(Command("b"));

        stack.MarkSaved(point);

        Assert.False(stack.IsAtSavedState);
        stack.Undo();
        Assert.True(stack.IsAtSavedState);
    }

    [Fact]
    public void MarkSavedAtACapturedPositionDoesNothingAfterTheSavedStateWasForgotten()
    {
        var stack = new UndoRedoStack();
        stack.Do(Command("a"));
        SavePoint point = stack.CapturePosition();
        stack.ForgetSavedState();

        stack.MarkSaved(point);

        Assert.False(stack.IsAtSavedState);
    }
}
