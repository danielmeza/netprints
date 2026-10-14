using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NetPrints.Tests.Samples;
using Xunit;

namespace NetPrints.Tests.Core
{
    public partial class SourceHygieneTests
    {
        // Documented examples that show a synthetic user's path (ADR-0033).
        private static readonly string[] MachinePathAllowlist =
        [
            "docs/guide/catalogs.md",
            "specs/005-editor-shell/contracts/state-files.md",
        ];

        // Built from parts so this file does not match itself.
        private static readonly string[] MachinePathMarkers =
        [
            "/" + "home/",
            "/" + "Users/",
            "/" + "mnt/",
            "C:" + "\\Users\\",
            "C:" + "/Users/",
            "scratch" + "pad",
        ];

        /// <summary>ADR-0033: no tracked file names an absolute home, mount or scratch path of a machine.</summary>
        [Fact]
        public void NoTrackedFileNamesAMachinePath()
        {
            string root = SampleProjectFactory.FindRepositoryRoot();
            var offenders = new List<string>();

            foreach (string relative in TrackedFiles(root).Where(path => !MachinePathAllowlist.Contains(path, StringComparer.Ordinal)))
            {
                string full = Path.Combine(root, relative);
                if (!File.Exists(full))
                {
                    continue;
                }

                byte[] bytes = File.ReadAllBytes(full);
                if (Array.IndexOf(bytes, (byte)0) >= 0)
                {
                    continue;
                }

                string text = System.Text.Encoding.UTF8.GetString(bytes);
                string? marker = MachinePathMarkers.FirstOrDefault(m => text.Contains(m, StringComparison.Ordinal));
                if (marker is not null)
                {
                    offenders.Add($"{relative}: {marker}");
                }
            }

            Assert.Empty(offenders);
        }

        private static string[] TrackedFiles(string root)
        {
            var info = new ProcessStartInfo("git", ["-C", root, "ls-files", "-z"])
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            using Process process = Process.Start(info) ?? throw new InvalidOperationException("git did not start.");
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            Assert.Equal(0, process.ExitCode);
            return output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
