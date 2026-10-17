using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NetPrints.Extensibility;
using NetPrints.Tests.Samples;
using Xunit;

namespace NetPrints.Tests.Extensibility;

/// <summary>FR-020, research R12: the recorded hash of the public API files changes only together with <see cref="ExtensionApi.Version"/>.</summary>
public class ExtensionApiVersionTests
{
    private static readonly string[] ApiFiles =
    [
        "src/NetPrints.Extensibility.Abstractions/PublicAPI.Shipped.txt",
        "src/NetPrints.Extensibility.Abstractions/PublicAPI.Unshipped.txt",
        "src/NetPrints.Extensibility/PublicAPI.Shipped.txt",
        "src/NetPrints.Extensibility/PublicAPI.Unshipped.txt",
    ];

    /// <summary>
    /// One row per API version, oldest first. While the last row is not shipped, changing the API files means a new hash in it. Once a version
    /// ships, its row is marked shipped and its API files carry no unshipped entries: a later change needs a new row with the new
    /// <see cref="ExtensionApi.Version"/>, and rehashing the shipped row fails.
    /// </summary>
    private static readonly (string Version, string Hash, bool Shipped)[] Recorded =
    [
        ("0.4", "461d9ac28853096aec4eb6a93b653b43f2cda614e3f91ab4f20f0f2b5167dfcc", false),
    ];

    private static string CurrentHash()
    {
        string root = SampleProjectFactory.FindRepositoryRoot();
        var text = new StringBuilder();
        foreach (string file in ApiFiles)
        {
            text.Append(file).Append('\n').Append(File.ReadAllText(Path.Combine(root, file)).Replace("\r\n", "\n", StringComparison.Ordinal)).Append('\n');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()))).ToLowerInvariant();
    }

    [Fact]
    public void TheVersionIsTheLastRecordedOne()
    {
        Assert.Equal(Recorded[^1].Version, $"{ExtensionApi.Version.Major}.{ExtensionApi.Version.Minor}");
    }

    [Fact]
    public void TheApiFilesMatchTheHashRecordedForTheVersion()
    {
        Assert.Equal(Recorded[^1].Hash, CurrentHash());
    }

    [Fact]
    public void AShippedVersionHasNoUnshippedEntries()
    {
        string root = SampleProjectFactory.FindRepositoryRoot();
        string[] unshipped = [.. ApiFiles.Where(file => file.EndsWith("Unshipped.txt", StringComparison.Ordinal))];
        List<string> entries =
        [
            .. unshipped.SelectMany(file => File.ReadLines(Path.Combine(root, file)))
                .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#')),
        ];

        Assert.All(Recorded[..^1], row => Assert.True(row.Shipped, $"{row.Version} is followed by a newer version, so it must be marked shipped"));
        if (Recorded[^1].Shipped)
        {
            Assert.True(entries.Count == 0, $"{Recorded[^1].Version} is shipped; add a row for the next ExtensionApi.Version before changing the API ({entries.Count} unshipped entries)");
        }
    }

    [Fact]
    public void TheRecordedVersionsAscend()
    {
        List<Version> versions = [.. Recorded.Select(row => Version.Parse(row.Version))];

        Assert.Equal(versions.OrderBy(version => version), versions);
        Assert.Equal(versions.Count, versions.Distinct().Count());
    }
}
