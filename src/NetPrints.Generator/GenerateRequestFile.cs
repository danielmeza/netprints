#nullable enable
using System;
using System.Collections.Generic;
using System.IO;

namespace NetPrints.Generator;

/// <summary>
/// One graph document to generate: <see cref="Input"/> is read and translated, and the result is
/// written to <see cref="Output"/> when it differs from what is already there (project-system.md §3).
/// </summary>
/// <param name="Input">Path of the graph document (<c>.netpc.json</c>) to read.</param>
/// <param name="Output">Path of the generated <c>.netpc.g.cs</c> file to write.</param>
public sealed record GraphJob(string Input, string Output);

/// <summary>
/// A parsed <c>netprints.generate.rsp</c> request: one project's worth of graphs to (re)generate
/// (project-system.md §2, §3). Written, one <c>key=value</c> line at a time, by <c>NetPrints.Sdk.targets</c>'
/// <c>NetPrintsGenerate</c> target, and read back by <see cref="GenerateRequestFile.Parse"/>.
/// </summary>
/// <param name="ProjectPath">Full path of the project file the request was written for
/// (<c>$(MSBuildProjectFullPath)</c>); used only for diagnostics, never read from disk here.</param>
/// <param name="RootNamespace">The project's <c>$(RootNamespace)</c>, or <see langword="null"/> if the
/// request had no <c>rootNamespace</c> line.</param>
/// <param name="Profile">The project's <c>$(NetPrintsProfile)</c>.</param>
/// <param name="Graphs">Graph documents to (re)generate, in request order.</param>
/// <param name="Extensions">Full paths of the project's <c>NetPrintsExtension</c> folders, in project
/// order.</param>
public sealed record GenerateRequest(string ProjectPath, string? RootNamespace, string Profile,
    IReadOnlyList<GraphJob> Graphs, IReadOnlyList<string> Extensions);

/// <summary>
/// Reads a <see cref="GenerateRequest"/> from the line-oriented request file the SDK's
/// <c>NetPrintsGenerate</c> target writes (project-system.md §2): one <c>key=value</c> line per entry,
/// UTF-8, no escaping (so paths are never split on anything but the documented separators).
/// </summary>
public static class GenerateRequestFile
{
    /// <summary>
    /// Parses the request file at <paramref name="path"/>.
    /// </summary>
    /// <param name="path">Path of the request file to read.</param>
    /// <returns>The parsed request.</returns>
    /// <exception cref="FormatException">A line is not of the form <c>key=value</c>, has an
    /// unrecognized key, a <c>graph</c> line has no <c>|</c> separating its input and output paths, or
    /// the request has no <c>project</c> or <c>profile</c> line.</exception>
    public static GenerateRequest Parse(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        string? project = null;
        string? rootNamespace = null;
        string? profile = null;
        var extensions = new List<string>();
        var graphs = new List<GraphJob>();

        foreach (string line in File.ReadLines(path))
        {
            int separator = line.IndexOf('=');
            if (separator < 0)
            {
                throw new FormatException($"Request line '{line}' is not of the form 'key=value'.");
            }

            string key = line[..separator];
            string value = line[(separator + 1)..];

            switch (key)
            {
                case "project":
                    project = value;
                    break;
                case "rootNamespace":
                    rootNamespace = value;
                    break;
                case "profile":
                    profile = value;
                    break;
                case "extension":
                    extensions.Add(value);
                    break;
                case "graph":
                    int pipe = value.IndexOf('|');
                    if (pipe < 0)
                    {
                        throw new FormatException($"Graph line '{line}' is missing '|' between its input and output paths.");
                    }

                    graphs.Add(new GraphJob(value[..pipe], value[(pipe + 1)..]));
                    break;
                default:
                    throw new FormatException($"Request line '{line}' has an unknown key '{key}'.");
            }
        }

        if (project is null)
        {
            throw new FormatException($"Request file '{path}' is missing a 'project' line.");
        }

        if (profile is null)
        {
            throw new FormatException($"Request file '{path}' is missing a 'profile' line.");
        }

        return new GenerateRequest(project, rootNamespace, profile, graphs, extensions);
    }
}
