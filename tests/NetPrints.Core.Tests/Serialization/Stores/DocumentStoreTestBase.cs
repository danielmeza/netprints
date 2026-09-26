using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetPrints.Serialization;
using NetPrints.Serialization.Stores;
using Xunit;

namespace NetPrints.Tests.Serialization.Stores
{
    /// <summary>
    /// Shared <see cref="IDocumentStore"/> behavior, run against every implementation
    /// (<see cref="InMemoryDocumentStoreTests"/>, <see cref="FileSystemDocumentStoreTests"/>; DF-T14):
    /// write/read round trips, a missing document, overwriting, and listing by prefix.
    /// </summary>
    public abstract class DocumentStoreTestBase : IDisposable
    {
        private readonly IDocumentStore store;

        protected DocumentStoreTestBase()
        {
            store = CreateStore();
        }

        /// <summary>Creates a fresh, empty store for one test.</summary>
        protected abstract IDocumentStore CreateStore();

        /// <inheritdoc/>
        public virtual void Dispose() => store.Dispose();

        private static ValueTask WriteText(Stream stream, string text, CancellationToken cancellationToken)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            return new ValueTask(stream.WriteAsync(bytes, cancellationToken).AsTask());
        }

        private static async Task<string> ReadTextAsync(IDocumentStore store, DocumentId id, CancellationToken cancellationToken)
        {
            using Stream stream = await store.OpenReadAsync(id, cancellationToken);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return await reader.ReadToEndAsync(cancellationToken);
        }

        private static async Task<List<DocumentId>> ListAsync(IDocumentStore store, string prefix, CancellationToken cancellationToken)
        {
            var ids = new List<DocumentId>();
            await foreach (DocumentId id in store.ListAsync(prefix, cancellationToken))
            {
                ids.Add(id);
            }

            return ids;
        }

        [Fact]
        public async Task WriteThenReadRoundTrips()
        {
            var id = new DocumentId("a/b.txt");
            await store.WriteAsync(id, (s, ct) => WriteText(s, "hello", ct), TestContext.Current.CancellationToken);

            Assert.True(await store.ExistsAsync(id, TestContext.Current.CancellationToken));
            Assert.Equal("hello", await ReadTextAsync(store, id, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task ExistsAsyncIsFalseForAMissingDocument()
        {
            Assert.False(await store.ExistsAsync(new DocumentId("missing.txt"), TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task OpenReadAsyncThrowsForAMissingDocument()
        {
            await Assert.ThrowsAsync<DocumentNotFoundException>(() =>
                store.OpenReadAsync(new DocumentId("missing.txt"), TestContext.Current.CancellationToken).AsTask());
        }

        [Fact]
        public async Task WriteAsyncOverwritesExistingContent()
        {
            var id = new DocumentId("c.txt");
            await store.WriteAsync(id, (s, ct) => WriteText(s, "first", ct), TestContext.Current.CancellationToken);
            await store.WriteAsync(id, (s, ct) => WriteText(s, "second", ct), TestContext.Current.CancellationToken);

            Assert.Equal("second", await ReadTextAsync(store, id, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task WriteAsyncCreatesMissingContainingDirectories()
        {
            var id = new DocumentId("deeply/nested/directory/d.txt");
            await store.WriteAsync(id, (s, ct) => WriteText(s, "nested", ct), TestContext.Current.CancellationToken);

            Assert.Equal("nested", await ReadTextAsync(store, id, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task ListAsyncReturnsOrdinalSortedIdsMatchingThePrefix()
        {
            await store.WriteAsync(new DocumentId("b/2.txt"), (s, ct) => WriteText(s, "2", ct), TestContext.Current.CancellationToken);
            await store.WriteAsync(new DocumentId("b/1.txt"), (s, ct) => WriteText(s, "1", ct), TestContext.Current.CancellationToken);
            await store.WriteAsync(new DocumentId("a/1.txt"), (s, ct) => WriteText(s, "1", ct), TestContext.Current.CancellationToken);

            List<DocumentId> matching = await ListAsync(store, "b/", TestContext.Current.CancellationToken);
            Assert.Equal([new DocumentId("b/1.txt"), new DocumentId("b/2.txt")], matching);

            List<DocumentId> all = await ListAsync(store, "", TestContext.Current.CancellationToken);
            Assert.Equal([new DocumentId("a/1.txt"), new DocumentId("b/1.txt"), new DocumentId("b/2.txt")], all);
        }
    }
}
