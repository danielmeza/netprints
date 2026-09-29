namespace NetPrints.Editor.Hosting;

/// <summary>
/// The system clipboard.
/// </summary>
public interface IClipboardService
{
    /// <summary>Replaces the clipboard's contents with plain text.</summary>
    /// <param name="text">Text to place on the clipboard.</param>
    Task SetTextAsync(string text);
}
