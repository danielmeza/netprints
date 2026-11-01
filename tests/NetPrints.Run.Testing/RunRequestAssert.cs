using NetPrints.Projects;
using Xunit;

namespace NetPrints.Run.Testing;

/// <summary>Compares the process requests the resolver, the CLI and the editor produce on the fields they must agree on (SC-008).</summary>
public static class RunRequestAssert
{
    /// <summary>Asserts that two requests agree on program, arguments, working directory, environment and removals.</summary>
    /// <param name="expected">The expected request.</param>
    /// <param name="actual">The request under test.</param>
    public static void Same(ProcessStartRequest expected, ProcessStartRequest actual)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);

        Assert.Equal(expected.FileName, actual.FileName);
        Assert.Equal(expected.Arguments, actual.Arguments);
        Assert.Equal(expected.WorkingDirectory, actual.WorkingDirectory);
        Assert.Equal(Sorted(expected.EnvironmentVariables), Sorted(actual.EnvironmentVariables));
        Assert.Equal(expected.EnvironmentRemovals.Order(StringComparer.Ordinal), actual.EnvironmentRemovals.Order(StringComparer.Ordinal));
    }

    private static List<string> Sorted(IReadOnlyDictionary<string, string>? variables) =>
        [.. (variables ?? new Dictionary<string, string>()).OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}")];
}
