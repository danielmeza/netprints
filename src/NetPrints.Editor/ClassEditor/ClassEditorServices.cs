using CommunityToolkit.Mvvm.Messaging;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.UndoRedo;

namespace NetPrints.Editor.ClassEditor;

/// <summary>
/// The narrow set of services a class editor's child view models depend on, instead of a direct
/// reference to the owning <see cref="ClassEditorVM"/> (FR-038): host services, the class editor's
/// undo/redo history and its scoped messenger.
/// </summary>
/// <param name="Context">Host services shared across the editor.</param>
/// <param name="UndoRedo">Undo/redo history of the class editor.</param>
/// <param name="Messenger">Messenger scoped to the class editor.</param>
public sealed record ClassEditorServices(EditorContext Context, UndoRedoStack UndoRedo, IMessenger Messenger);
