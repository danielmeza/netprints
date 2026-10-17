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

    /// <summary>One row per API version, oldest first. Changing the API files means a new hash in the last row, or a new row with the new <see cref="ExtensionApi.Version"/>.</summary>
    private static readonly (string Version, string Hash)[] Recorded =
    [
        ("0.4", "0a90c47c4c9b7f8e912d2a7ac4567b6b82912a91252d7d8acfda7c587f56d628"),
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
    public void TheRecordedVersionsAscend()
    {
        List<Version> versions = [.. Recorded.Select(row => Version.Parse(row.Version))];

        Assert.Equal(versions.OrderBy(version => version), versions);
        Assert.Equal(versions.Count, versions.Distinct().Count());
    }
}
