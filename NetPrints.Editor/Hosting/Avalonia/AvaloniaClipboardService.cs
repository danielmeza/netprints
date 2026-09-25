using Avalonia.Controls;
using Avalonia.Input.Platform;
using NetPrints.Editor.Hosting;

namespace NetPrints.Editor.Hosting.Avalonia;

/// <summary>Clipboard through <see cref="TopLevel.Clipboard"/>.</summary>
public sealed class AvaloniaClipboardService(Func<TopLevel?> topLevel) : IClipboardService
{
    public async Task SetTextAsync(string text)
    {
        if (topLevel()?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    }
}
