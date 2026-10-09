using CommunityToolkit.Mvvm.Messaging;
using NetPrints.Core;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.UndoRedo;

namespace NetPrints.Editor.ClassEditor;

/// <summary>
/// The narrow set of services a class editor's child view models depend on, instead of a direct
/// reference to the owning <see cref="Shell.ClassContext"/> (FR-038): host services, the class editor's
/// undo/redo history and its scoped messenger.
/// </summary>
/// <param name="Context">Host services shared across the editor.</param>
/// <param name="UndoRedo">Undo/redo history of the class editor.</param>
/// <param name="Messenger">Messenger scoped to the class editor.</param>
/// <param name="ProjectClasses">The classes whose graphs may call the class editor's members; their calls follow a signature edit.</param>
public sealed record ClassEditorServices(EditorContext Context, UndoRedoStack UndoRedo, IMessenger Messenger, Func<IReadOnlyList<ClassGraph>> ProjectClasses);
