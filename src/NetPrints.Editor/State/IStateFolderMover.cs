namespace NetPrints.Editor.State;

/// <summary>Moves a whole folder tree, for the one-time state migration.</summary>
public interface IStateFolderMover
{
    /// <summary>Moves a folder and everything in it to a path that does not exist yet.</summary>
    /// <param name="source">The folder to move.</param>
    /// <param name="destination">The new path; its parent exists.</param>
    /// <exception cref="IOException">The folder could not be moved; the source is left as it was.</exception>
    /// <exception cref="UnauthorizedAccessException">The folder could not be moved; the source is left as it was.</exception>
    void Move(string source, string destination);
}
