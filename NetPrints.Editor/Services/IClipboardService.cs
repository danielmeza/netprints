namespace NetPrints.Editor.Services;

public interface IClipboardService
{
    Task SetTextAsync(string text);
}
