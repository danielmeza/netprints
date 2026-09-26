using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Reactive.Testing;
using NetPrints.Serialization;
using NetPrints.Serialization.Stores;
using Xunit;

namespace NetPrints.Tests.Serialization.Stores
{
    /// <summary>
    /// <see cref="FileSystemDocumentStore"/>: the shared store tests (DF-T14) plus its own
    /// <see cref="IDocumentStore.WriteAsync"/> atomicity (DF-T12) and <see cref="IDocumentStore.Changes"/>
    /// behavior (DF-T13).
    /// </summary>
    public sealed class FileSystemDocumentStoreTests : DocumentStoreTestBase
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "netprints-filestore-" + Guid.NewGuid().ToString("N"));

        protected override IDocumentStore CreateStore() => new FileSystemDocumentStore(root, Scheduler.Default, NullLogger<FileSystemDocumentStore>.Instance);

        public override void Dispose()
        {
            base.Dispose();
            try
            { Directory.Delete(root, recursive: true); }
            catch (IOException) { }
        }

        private static ValueTask WriteText(Stream stream, string text, System.Threading.CancellationToken cancellationToken) =>
            new(stream.WriteAsync(Encoding.UTF8.GetBytes(text), cancellationToken).AsTask());

        private static string NewRoot() => Path.Combine(Path.GetTempPath(), "netprints-filestore-" + Guid.NewGuid().ToString("N"));

        // DF-T12: an exception from `write` leaves the old file intact and no temp file behind.
        [Fact]
        public async Task WriteAsyncExceptionLeavesOldFileIntactAndNoTempFile()
        {
            string directory = NewRoot();
            using var store = new FileSystemDocumentStore(directory, Scheduler.Default, NullLogger<FileSystemDocumentStore>.Instance);
            var id = new DocumentId("a.txt");

            await store.WriteAsync(id, (s, ct) => WriteText(s, "original", ct), TestContext.Current.CancellationToken);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.WriteAsync(id, (s, ct) => throw new InvalidOperationException("boom"), TestContext.Current.CancellationToken).AsTask());

            Assert.Equal("original", await File.ReadAllTextAsync(store.GetFullPath(id), TestContext.Current.CancellationToken));
            Assert.DoesNotContain(Directory.GetFiles(directory), f => f.Contains(".tmp-", StringComparison.Ordinal));

            Directory.Delete(directory, recursive: true);
        }

        // DF-T12: cancellation (an OperationCanceledException from `write`) behaves the same way.
        [Fact]
        public async Task WriteAsyncCancellationLeavesOldFileIntactAndNoTempFile()
        {
            string directory = NewRoot();
            using var store = new FileSystemDocumentStore(directory, Scheduler.Default, NullLogger<FileSystemDocumentStore>.Instance);
            var id = new DocumentId("a.txt");

            await store.WriteAsync(id, (s, ct) => WriteText(s, "original", ct), TestContext.Current.CancellationToken);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                store.WriteAsync(id, (s, ct) => throw new OperationCanceledException(), TestContext.Current.CancellationToken).AsTask());

            Assert.Equal("original", await File.ReadAllTextAsync(store.GetFullPath(id), TestContext.Current.CancellationToken));
            Assert.DoesNotContain(Directory.GetFiles(directory), f => f.Contains(".tmp-", StringComparison.Ordinal));

            Directory.Delete(directory, recursive: true);
        }

        // Repeatedly advances `scheduler`'s virtual clock in small steps while also letting real time
        // pass, so a debounced callback scheduled at an unpredictable *real* moment (when the OS's file
        // system notification actually arrives) still eventually fires; stops early once `done` is true.
        private static async Task PumpAsync(TestScheduler scheduler, Func<bool> done)
        {
            for (int i = 0; i < 150 && !done(); i++)
            {
                await Task.Delay(20, TestContext.Current.CancellationToken);
                scheduler.AdvanceBy(TimeSpan.FromMilliseconds(20).Ticks);
            }
        }

        // DF-T13: an external edit raises exactly one Changed (virtual time); a write through the store
        // itself raises none; Dispose completes the observable.
        [Fact]
        public async Task ExternalEditRaisesOneChangedOwnWriteRaisesNoneAndDisposeCompletes()
        {
            string directory = NewRoot();
            var scheduler = new TestScheduler();
            var store = new FileSystemDocumentStore(directory, scheduler, NullLogger<FileSystemDocumentStore>.Instance);
            var id = new DocumentId("a.txt");

            await store.WriteAsync(id, (s, ct) => WriteText(s, "seed", ct), TestContext.Current.CancellationToken);
            // Let the seed write's own file system event arrive and be consumed by the suppression
            // check before subscribing, so it cannot leak into the assertions below.
            await Task.Delay(500, TestContext.Current.CancellationToken);

            var received = new List<DocumentChange>();
            bool completed = false;
            using IDisposable subscription = store.Changes.Subscribe(received.Add, () => completed = true);

            // Own write: WriteAsync's resulting file system event is suppressed.
            await store.WriteAsync(id, (s, ct) => WriteText(s, "own", ct), TestContext.Current.CancellationToken);
            await PumpAsync(scheduler, () => false);
            Assert.Empty(received);

            // External edit: bypasses the store, so nothing suppresses it.
            await File.WriteAllTextAsync(store.GetFullPath(id), "external", TestContext.Current.CancellationToken);
            await PumpAsync(scheduler, () => received.Count > 0);

            DocumentChange change = Assert.Single(received);
            Assert.Equal(id, change.Id);
            Assert.Equal(DocumentChangeKind.Changed, change.Kind);

            store.Dispose();
            Assert.True(completed);

            Directory.Delete(directory, recursive: true);
        }
    }
}
