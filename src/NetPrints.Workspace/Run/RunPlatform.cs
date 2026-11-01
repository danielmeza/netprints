using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace NetPrints.Workspace.Run;

/// <summary>The operating system rules of resolving a run. Tests pass a member explicitly; production passes <see cref="Current"/>.</summary>
public abstract class RunPlatform
{
    private const string BundleExtension = ".app";
    private const string BundleExecutableKey = "CFBundleExecutable";

    private RunPlatform()
    {
    }

    /// <summary>Gets Windows.</summary>
    public static RunPlatform Windows { get; } = new WindowsPlatform();

    /// <summary>Gets macOS.</summary>
    public static RunPlatform MacOS { get; } = new MacOSPlatform();

    /// <summary>Gets Linux and the other Unix systems.</summary>
    public static RunPlatform Linux { get; } = new LinuxPlatform();

    /// <summary>Gets the platform this process runs on.</summary>
    public static RunPlatform Current =>
        OperatingSystem.IsWindows() ? Windows : OperatingSystem.IsMacOS() ? MacOS : Linux;

    /// <summary>Gets the platform's name.</summary>
    public abstract string Name { get; }

    /// <summary>Gets the character that separates the entries of <c>PATH</c>.</summary>
    public abstract char PathListSeparator { get; }

    /// <summary>Gets how environment variable names compare.</summary>
    public abstract StringComparer VariableNameComparer { get; }

    /// <summary>Gets a value indicating whether <paramref name="path"/> names a place, not a bare program name.</summary>
    /// <param name="path">The expanded executable path.</param>
    /// <returns><see langword="true"/> when it contains a directory separator of the platform.</returns>
    public abstract bool HasDirectorySeparator(string path);

    /// <summary>Gets the file names to try for a bare program name in each <c>PATH</c> entry, in order.</summary>
    /// <param name="bareName">The program name.</param>
    /// <returns>The candidate file names.</returns>
    public abstract IReadOnlyList<string> CandidateNames(string bareName);

    /// <summary>Gets a value indicating whether <paramref name="path"/> is an application bundle.</summary>
    /// <param name="path">The full path.</param>
    /// <returns><see langword="true"/> on macOS for an existing folder that ends in <c>.app</c>.</returns>
    public virtual bool IsBundle(string path) => false;

    /// <summary>Reads the program a bundle starts.</summary>
    /// <param name="bundlePath">The bundle folder.</param>
    /// <returns>The path of <c>Contents/MacOS/&lt;CFBundleExecutable&gt;</c>; <see langword="null"/> when the plist is missing, unreadable, not XML or names no usable executable.</returns>
    public virtual string? BundleExecutable(string bundlePath) => null;

    /// <summary>Gets a value indicating whether the platform refuses to start <paramref name="path"/> directly (NPR009).</summary>
    /// <param name="path">The resolved path.</param>
    /// <returns><see langword="true"/> on Windows for a <c>.cmd</c> or <c>.bat</c> file.</returns>
    public virtual bool RefusesTarget(string path) => false;

    private sealed class WindowsPlatform : RunPlatform
    {
        public override string Name => "Windows";

        public override char PathListSeparator => ';';

        public override StringComparer VariableNameComparer => StringComparer.OrdinalIgnoreCase;

        public override bool HasDirectorySeparator(string path) => path.Contains('\\', StringComparison.Ordinal) || path.Contains('/', StringComparison.Ordinal);

        public override IReadOnlyList<string> CandidateNames(string bareName) =>
            Path.HasExtension(bareName) ? [bareName, bareName + ".exe", bareName + ".com"] : [bareName + ".exe", bareName + ".com"];

        public override bool RefusesTarget(string path)
        {
            string extension = Path.GetExtension(path);
            return string.Equals(extension, ".cmd", StringComparison.OrdinalIgnoreCase) || string.Equals(extension, ".bat", StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class MacOSPlatform : RunPlatform
    {
        public override string Name => "MacOS";

        public override char PathListSeparator => ':';

        public override StringComparer VariableNameComparer => StringComparer.Ordinal;

        public override bool HasDirectorySeparator(string path) => path.Contains('/', StringComparison.Ordinal);

        public override IReadOnlyList<string> CandidateNames(string bareName) => [bareName];

        public override bool IsBundle(string path) =>
            path.EndsWith(BundleExtension, StringComparison.OrdinalIgnoreCase) && Directory.Exists(path);

        public override string? BundleExecutable(string bundlePath)
        {
            string plist = Path.Combine(bundlePath, "Contents", "Info.plist");
            string? value = ReadBundleExecutable(plist);
            if (string.IsNullOrEmpty(value) || !string.Equals(Path.GetFileName(value), value, StringComparison.Ordinal) || value is "." or "..")
            {
                return null;
            }

            return Path.Combine(bundlePath, "Contents", "MacOS", value);
        }

        private static string? ReadBundleExecutable(string plist)
        {
            try
            {
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
                using XmlReader reader = XmlReader.Create(plist, settings);
                XDocument document = XDocument.Load(reader);
                XElement? key = document.Descendants("key").FirstOrDefault(element => string.Equals(element.Value, BundleExecutableKey, StringComparison.Ordinal));
                XElement? value = key?.ElementsAfterSelf().FirstOrDefault();
                return value is not null && string.Equals(value.Name.LocalName, "string", StringComparison.Ordinal) ? value.Value : null;
            }
            catch (Exception exception) when (exception is XmlException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                return null;
            }
        }
    }

    private sealed class LinuxPlatform : RunPlatform
    {
        public override string Name => "Linux";

        public override char PathListSeparator => ':';

        public override StringComparer VariableNameComparer => StringComparer.Ordinal;

        public override bool HasDirectorySeparator(string path) => path.Contains('/', StringComparison.Ordinal);

        public override IReadOnlyList<string> CandidateNames(string bareName) => [bareName];
    }
}
