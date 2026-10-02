using CommunityToolkit.Mvvm.ComponentModel;

namespace NetPrints.Editor.Shell;

/// <summary>A document the shell can open in a tab: its stable id, its title and whether its file has unsaved edits.</summary>
public abstract partial class DocumentViewModel(DocumentId id, string title) : ObservableObject, IDisposable
{
    /// <summary>Gets the stable id the shell finds the document by.</summary>
    public DocumentId Id { get; } = id;

    /// <summary>Gets the tab title, with a trailing <c>*</c> while <see cref="IsUnsaved"/>.</summary>
    [ObservableProperty]
    public partial string Title { get; protected set; } = title;

    /// <summary>Gets a value indicating whether the file behind the document has unsaved edits.</summary>
    [ObservableProperty]
    public partial bool IsUnsaved { get; protected set; }

    /// <summary>Releases what the document follows; the shell calls it once the document is closed.</summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases what the document follows.</summary>
    /// <param name="disposing">True when called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
    }
}
