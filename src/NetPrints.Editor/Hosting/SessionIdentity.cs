namespace NetPrints.Editor.Hosting;

/// <summary>What identifies one project session to its services.</summary>
/// <param name="ProjectFilePath">The full path of the project file, or <see langword="null"/> for a session with no project yet (the New project dialog).</param>
/// <param name="Warn">Shows the first problem of the session to the user.</param>
internal sealed record SessionData(string? ProjectFilePath, Action<string> Warn);

/// <summary>Holds the <see cref="SessionData"/> of the session scope that owns it; set once, right after the scope is created.</summary>
[SessionScoped<SessionIdentity>]
internal sealed class SessionIdentity
{
    private SessionData? data;

    /// <summary>Gets the session's data.</summary>
    /// <exception cref="InvalidOperationException">The scope was not initialized.</exception>
    public SessionData Data => data ?? throw new InvalidOperationException("The session scope was not initialized.");

    /// <summary>Gets the project file path, or throws when the session has no project.</summary>
    /// <exception cref="InvalidOperationException">The session has no project file.</exception>
    public string ProjectFilePath => Data.ProjectFilePath ?? throw new InvalidOperationException("The session has no project file.");

    /// <summary>Sets the data once.</summary>
    /// <param name="value">The session's data.</param>
    /// <exception cref="InvalidOperationException">The data was already set.</exception>
    public void Initialize(SessionData value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (data is not null)
        {
            throw new InvalidOperationException("The session scope was already initialized.");
        }

        data = value;
    }
}
