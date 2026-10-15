using System.Diagnostics;

namespace NetPrints.Editor.StartPage;

/// <summary>Opens an address with the operating system's default browser.</summary>
internal sealed class ShellUrlLauncher : IUrlLauncher
{
    /// <inheritdoc/>
    public void Open(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return;
        }

        using Process? started = Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }
}
