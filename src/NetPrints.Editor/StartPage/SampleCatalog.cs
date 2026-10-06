namespace NetPrints.Editor.StartPage;

/// <summary>The samples bundled with the editor, and the copy of one to a folder the user chose.</summary>
/// <param name="root">The folder that holds one folder per sample.</param>
internal sealed class SampleCatalog(string root)
{
    private const string CompiledPrefix = "Compiled_";

    /// <summary>Gets the catalog of the samples next to the running editor.</summary>
    public static SampleCatalog Bundled => new(Path.Combine(AppContext.BaseDirectory, "samples"));

    /// <summary>Gets the samples, by name.</summary>
    public IReadOnlyList<SampleDescriptor> Samples
    {
        get
        {
            if (!Directory.Exists(root))
            {
                return [];
            }

            List<SampleDescriptor> samples = [];
            foreach (string directory in Directory.GetDirectories(root).Order(StringComparer.Ordinal))
            {
                if (Directory.GetFiles(directory, "*.csproj").Order(StringComparer.Ordinal).FirstOrDefault() is { } project)
                {
                    samples.Add(new SampleDescriptor(Path.GetFileName(directory), directory, Path.GetFileName(project)));
                }
            }

            return samples;
        }
    }

    /// <summary>Copies a sample, without its build output, to an empty or new folder; a failure removes what was copied.</summary>
    /// <param name="sample">The sample.</param>
    /// <param name="folder">The target folder.</param>
    /// <param name="cancellationToken">Cancels the copy.</param>
    /// <returns>The path of the copy's <c>.csproj</c>.</returns>
    /// <exception cref="IOException">The target is a file or a folder that is not empty; nothing was written.</exception>
    public Task<string> CopyAsync(SampleDescriptor sample, string folder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sample);
        string target = Path.GetFullPath(folder);
        if (File.Exists(target) || (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any()))
        {
            throw new IOException($"'{target}' is not empty. Choose an empty or new folder.");
        }

        return Task.Run(() => Copy(sample, target, cancellationToken), cancellationToken);
    }

    private static string Copy(SampleDescriptor sample, string target, CancellationToken cancellationToken)
    {
        bool created = !Directory.Exists(target);
        try
        {
            Directory.CreateDirectory(target);
            CopyFolder(sample.Directory, target, cancellationToken);
            return Path.Combine(target, sample.ProjectFileName);
        }
        catch
        {
            Remove(target, created);
            throw;
        }
    }

    private static void CopyFolder(string source, string target, CancellationToken cancellationToken)
    {
        foreach (string file in Directory.GetFiles(source))
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }

        foreach (string directory in Directory.GetDirectories(source))
        {
            string name = Path.GetFileName(directory);
            if (name is "bin" or "obj" || name.StartsWith(CompiledPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            string child = Path.Combine(target, name);
            Directory.CreateDirectory(child);
            CopyFolder(directory, child, cancellationToken);
        }
    }

    private static void Remove(string target, bool created)
    {
        try
        {
            if (created)
            {
                Directory.Delete(target, recursive: true);
                return;
            }

            foreach (string entry in Directory.GetFileSystemEntries(target))
            {
                if (Directory.Exists(entry))
                {
                    Directory.Delete(entry, recursive: true);
                }
                else
                {
                    File.Delete(entry);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Left as it is: the caller reports the original failure.
        }
    }
}
