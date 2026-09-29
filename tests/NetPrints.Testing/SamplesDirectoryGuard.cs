using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace NetPrints.Testing
{
    /// <summary>
    /// Assembly-wide regression guard (implementation-notes.md "samples/ pollution guard"): AGENTS.md
    /// says the suite must never leave changes under the real, checked-in <c>samples/</c> tree, since
    /// every legitimate test works on a temp copy (<c>SampleProjectFactory.CreateHelloWorld</c>,
    /// <c>NetPrints.Editor.Tests.TestPaths.CopyHelloWorldSample</c>, <c>NetPrints.Editor.UITests</c>'
    /// <c>SampleCopy</c> and <c>NetPrints.Desktop.E2ETests</c>' <c>X11SmokeTests</c>). Each consuming
    /// test assembly registers this with its own <c>[assembly: AssemblyFixture(typeof(SamplesDirectoryGuardFixture))]</c>,
    /// which xUnit v3 runs once around every test in that assembly regardless of order or
    /// parallelization: the constructor hashes every file under <c>samples/</c>, and <see cref="Dispose"/>
    /// hashes it again and fails loudly if a file changed, was added or was removed, instead of letting
    /// a test that writes into the real directory (or runs a real <c>dotnet build</c> against it)
    /// silently corrupt a checked-in fixture. A deliberate regeneration
    /// (<see cref="UpdateSnapshotsVariable"/> <c>=1</c>) skips the check.
    /// </summary>
    public sealed class SamplesDirectoryGuardFixture : IDisposable
    {
        /// <summary>The environment variable that, set to <c>1</c>, means a deliberate golden-fixture regeneration.</summary>
        public const string UpdateSnapshotsVariable = "NETPRINTS_UPDATE_SNAPSHOTS";

        private readonly Dictionary<string, string> before;

        public SamplesDirectoryGuardFixture()
        {
            before = Snapshot();
        }

        /// <exception cref="InvalidOperationException">
        /// The suite added, removed or changed a file under <c>samples/</c>.
        /// </exception>
        public void Dispose()
        {
            if (Environment.GetEnvironmentVariable(UpdateSnapshotsVariable) == "1")
            {
                return;
            }

            Dictionary<string, string> after = Snapshot();
            List<string> changes = new List<string>();

            foreach (KeyValuePair<string, string> entry in before)
            {
                if (!after.TryGetValue(entry.Key, out string? hash))
                {
                    changes.Add($"removed: {entry.Key}");
                }
                else if (!string.Equals(hash, entry.Value, StringComparison.Ordinal))
                {
                    changes.Add($"changed: {entry.Key}");
                }
            }

            foreach (string path in after.Keys)
            {
                if (!before.ContainsKey(path))
                {
                    changes.Add($"added: {path}");
                }
            }

            if (changes.Count > 0)
            {
                throw new InvalidOperationException(
                    "The test suite modified the checked-in samples/ directory (AGENTS.md \"Never leave " +
                    "changes under samples/\"); make the offending test work on a temp copy instead: " +
                    string.Join(", ", changes.OrderBy(change => change, StringComparer.Ordinal)));
            }
        }

        private static Dictionary<string, string> Snapshot()
        {
            string samplesDirectory = Path.Combine(FindRepositoryRoot(), "samples");
            var snapshot = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (string path in Directory.EnumerateFiles(samplesDirectory, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(samplesDirectory, path);
                using FileStream stream = File.OpenRead(path);
                snapshot[relativePath] = Convert.ToHexString(SHA256.HashData(stream));
            }

            return snapshot;
        }

        /// <summary>Walks up from <see cref="AppContext.BaseDirectory"/> to find the checked-out repository root.</summary>
        /// <returns>The repository root directory (the one containing <c>NetPrints.slnx</c>).</returns>
        /// <exception cref="InvalidOperationException">No <c>NetPrints.slnx</c> was found above the running tests' output directory.</exception>
        private static string FindRepositoryRoot()
        {
            for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "NetPrints.slnx")))
                {
                    return dir.FullName;
                }
            }

            throw new InvalidOperationException($"Could not find the repository root (NetPrints.slnx) above '{AppContext.BaseDirectory}'.");
        }
    }
}
