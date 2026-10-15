namespace NetPrints.Editor.State;

/// <summary>The well-known per-user folders the editor derives its own folders from.</summary>
/// <param name="Home">The user's home folder.</param>
/// <param name="ApplicationData">The config folder: <c>$XDG_CONFIG_HOME</c> or <c>~/.config</c> on Linux, <c>~/Library/Application Support</c> on macOS, the roaming <c>%APPDATA%</c> on Windows.</param>
/// <param name="LocalApplicationData">The local data folder: <c>%LOCALAPPDATA%</c> on Windows.</param>
public sealed record UserFolders(string Home, string ApplicationData, string LocalApplicationData)
{
    /// <summary>Gets the folders of the current user.</summary>
    public static UserFolders Current => new(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
}
