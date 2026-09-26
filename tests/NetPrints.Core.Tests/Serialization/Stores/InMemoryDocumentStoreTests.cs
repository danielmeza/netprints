using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NetPrints.Serialization;
using NetPrints.Serialization.Stores;
using Xunit;

namespace NetPrints.Tests.Serialization.Stores
{
    /// <summary>
    /// <see cref="InMemoryDocumentStore"/>: the shared store tests (DF-T14) plus its own
    /// <see cref="IDocumentStore.Changes"/> behavior (DF-T13): <see cref="InMemoryDocumentStore.Set"/>
    /// raises a change synchronously, <see cref="IDocumentStore.WriteAsync"/> (the "own write" path)
    /// raises none, and disposing completes the observable.
    /// </summary>
    public sealed class InMemoryDocumentStoreTests : DocumentStoreTestBase
    {
        protected override IDocumentStore CreateStore() => new InMemoryDocumentStore();

        [Fact]
        public void SetRaisesCreatedThenChangedSynchronously()
        {
            using var store = new InMemoryDocumentStore();
            var id = new DocumentId("a.txt");
            var received = new List<DocumentChange>();
            using var subscription = store.Changes.Subscribe(received.Add);

            store.Set(id, Encoding.UTF8.GetBytes("first"));
            store.Set(id, Encoding.UTF8.GetBytes("second"));

            Assert.Equal(
                [new DocumentChange(id, DocumentChangeKind.Created), new DocumentChange(id, DocumentChangeKind.Changed)],
                received);
            Assert.Equal("second", Encoding.UTF8.GetString(store.Get(id)));
        }

        [Fact]
        public async Task WriteAsyncRaisesNoChangeAndDisposeCompletesTheObservable()
        {
            var store = new InMemoryDocumentStore();
            var id = new DocumentId("a.txt");
            var received = new List<DocumentChange>();
            bool completed = false;
            store.Changes.Subscribe(received.Add, () => completed = true);

            await store.WriteAsync(id, (s, ct) => new ValueTask(s.WriteAsync(Encoding.UTF8.GetBytes("own"), ct).AsTask()),
                TestContext.Current.CancellationToken);

            Assert.Empty(received);

            store.Dispose();
            Assert.True(completed);
        }
    }
}
