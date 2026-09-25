namespace NetPrints.Testing.Ui.Snapshots;

/// <summary>
/// Compares screenshots with committed baseline PNGs. Every actual image is written to the output
/// folder (a CI artifact), with a diff image when it does not match. With
/// <c>NETPRINTS_UPDATE_SNAPSHOTS=1</c> the actual images become the new baselines.
/// </summary>
public sealed class SnapshotStore(string baselineDirectory, string outputDirectory)
{
    public const string UpdateVariable = "NETPRINTS_UPDATE_SNAPSHOTS";

    public string BaselineDirectory { get; } = baselineDirectory;

    public string OutputDirectory { get; } = outputDirectory;

    public static bool IsUpdating => Environment.GetEnvironmentVariable(UpdateVariable) == "1";

    /// <summary>Checks an image against the baseline <paramref name="name"/>.png.</summary>
    public void Match(string name, UiImage actual, SnapshotOptions? options = null)
    {
        string baselinePath = Path.Combine(BaselineDirectory, name + ".png");
        actual.Save(Path.Combine(OutputDirectory, name + ".actual.png"));

        if (IsUpdating)
        {
            actual.Save(baselinePath);
            return;
        }

        if (!File.Exists(baselinePath))
        {
            throw new SnapshotMismatchException(
                $"No baseline {baselinePath}. The actual image is in {OutputDirectory}; run with {UpdateVariable}=1 to create it.");
        }

        var comparison = SnapshotComparer.Compare(actual, new UiImage(File.ReadAllBytes(baselinePath)), options ?? SnapshotOptions.Default);
        if (!comparison.Matches)
        {
            comparison.Diff?.Save(Path.Combine(OutputDirectory, name + ".diff.png"));
            throw new SnapshotMismatchException($"Snapshot '{name}' does not match its baseline: {comparison.Reason}. See {OutputDirectory}.");
        }
    }
}

public sealed class SnapshotMismatchException(string message) : Exception(message);
