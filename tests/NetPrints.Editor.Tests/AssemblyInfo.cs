using Xunit;

// Tests share the preloaded reflection host and write to temp folders; keep them sequential.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
