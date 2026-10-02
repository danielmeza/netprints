namespace NetPrints.Editor.Hosting;

/// <summary>A line the followed program wrote, as <see cref="RunStateTracker.LineAppended"/> reports it.</summary>
/// <param name="Stream">The stream the line came from.</param>
/// <param name="Text">The line, without its line ending.</param>
public sealed record RunOutputLine(ProcessStream Stream, string Text);
