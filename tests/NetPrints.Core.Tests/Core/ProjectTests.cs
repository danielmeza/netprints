using System;
using NetPrints.Core;
using Xunit;

namespace NetPrintsUnitTests
{
    /// <summary>
    /// Covers the guard exception added when replacing the null-forgiving operator on
    /// <c>Path.GetDirectoryName(...)!</c> (T011, AGENTS.md "Nullable reference types"): a project
    /// path with no directory component (for example the filesystem root) now throws a clear
    /// <see cref="InvalidOperationException"/> instead of a <see cref="NullReferenceException"/>
    /// once the path is combined with a relative file name.
    /// </summary>
    public class ProjectTests
    {
        [Fact]
        public void SaveClassInProjectDirectoryThrowsForPathWithNoDirectory()
        {
            var project = Project.CreateNew("P", "N");
            project.Path = "/"; // Path.GetDirectoryName("/") is null: a root has no parent directory.
            var cls = new ClassGraph { Name = "C", Namespace = "N", Project = project };

            var ex = Assert.Throws<InvalidOperationException>(() => project.SaveClassInProjectDirectory(cls));
            Assert.Contains("has no directory", ex.Message);
        }
    }
}
