using NetPrints.Editor.State;

namespace NetPrints.Editor.Tests.State;

/// <summary>An <see cref="IEditorFileSystem"/> over dictionaries, with failure injection.</summary>
public sealed class InMemoryEditorFileSystem : IEditorFileSystem
{
    private readonly Dictionary<string, byte[]> files = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTime> writeTimes = new(StringComparer.Ordinal);
    private readonly HashSet<string> directories = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the time stamped on files written from now on.</summary>
    public DateTime Now { get; set; } = new(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);

    /// <summary>Gets or sets an action run before every write; throw from it to fail the write.</summary>
    public Action<string>? BeforeWrite { get; set; }

    /// <summary>Gets or sets an action run before every move; throw from it to fail the move.</summary>
    public Action<string, string>? BeforeMove { get; set; }

    /// <summary>Gets or sets an action run before every <see cref="FileExists"/>; block in it to imitate a slow path.</summary>
    public Action<string>? BeforeFileExists { get; set; }

    public IReadOnlyCollection<string> Files => files.Keys;

    public string ReadText(string path) => System.Text.Encoding.UTF8.GetString(files[path]);

    public bool FileExists(string path)
    {
        BeforeFileExists?.Invoke(path);
        return files.ContainsKey(path);
    }

    public bool DirectoryExists(string path) => directories.Contains(path);

    public void CreateDirectory(string path)
    {
        for (string? current = path; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
        {
            directories.Add(current);
        }
    }

    public void WriteAllBytes(string path, byte[] bytes)
    {
        BeforeWrite?.Invoke(path);
        files[path] = bytes;
        writeTimes[path] = Now;
    }

    public byte[] ReadAllBytes(string path) =>
        files.TryGetValue(path, out byte[]? bytes) ? bytes : throw new FileNotFoundException(path);

    public void Move(string source, string destination)
    {
        BeforeMove?.Invoke(source, destination);
        files[destination] = files[source];
        writeTimes[destination] = writeTimes[source];
        files.Remove(source);
        writeTimes.Remove(source);
    }

    public void DeleteFile(string path)
    {
        files.Remove(path);
        writeTimes.Remove(path);
    }

    public void DeleteDirectory(string path)
    {
        string prefix = path + Path.DirectorySeparatorChar;
        foreach (string file in files.Keys.Where(file => file.StartsWith(prefix, StringComparison.Ordinal)).ToList())
        {
            DeleteFile(file);
        }

        directories.RemoveWhere(directory => directory == path || directory.StartsWith(prefix, StringComparison.Ordinal));
    }

    public IEnumerable<string> EnumerateDirectories(string path) =>
        directories.Where(directory => Path.GetDirectoryName(directory) == path).ToList();

    public IEnumerable<string> EnumerateFiles(string path) =>
        files.Keys.Where(file => Path.GetDirectoryName(file) == path).ToList();

    public DateTime GetLastWriteTimeUtc(string path) =>
        writeTimes.TryGetValue(path, out DateTime time) ? time : throw new FileNotFoundException(path);
}
