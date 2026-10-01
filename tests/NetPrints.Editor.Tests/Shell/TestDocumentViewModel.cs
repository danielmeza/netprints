using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Tests.Shell;

/// <summary>A document with no editor behind it, whose title and unsaved flag a test can change.</summary>
public sealed class TestDocumentViewModel(DocumentId id, string title) : DocumentViewModel(id, title)
{
    public void Rename(string newTitle) => Title = newTitle;

    public void SetUnsaved(bool unsaved) => IsUnsaved = unsaved;
}
