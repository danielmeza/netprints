using Avalonia.Controls;
using Avalonia.Input.Platform;
using NetPrints.Editor.Hosting;

namespace NetPrints.Editor.Hosting.Avalonia;

/// <summary>Clipboard through <see cref="TopLevel.Clipboard"/>.</summary>
[Singleton<IClipboardService>(Factory = "Create")]
public sealed class AvaloniaClipboardService(Func<TopLevel?> topLevel) : IClipboardService
{
    /// <summary>Creates the clipboard service over the active window of <paramref name="windows"/>.</summary>
    /// <param name="windows">Picks the window whose clipboard is used.</param>
    /// <returns>The service.</returns>
    internal static AvaloniaClipboardService Create(WindowService windows) => new(() => windows.ActiveWindow);

    /// <inheritdoc/>
    public async Task SetTextAsync(string text)
    {
        if (topLevel()?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    }
}
