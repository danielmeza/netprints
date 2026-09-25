namespace NetPrints.Editor.Hosting;

public interface IClipboardService
{
    Task SetTextAsync(string text);
}
