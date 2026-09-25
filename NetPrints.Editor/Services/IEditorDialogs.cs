using NetPrints.Core;
using NetPrints.Editor.ViewModels;

namespace NetPrints.Editor.Services;

/// <summary>
/// Modal dialogs used by view models.
/// </summary>
public interface IEditorDialogs
{
    /// <summary>Shows an error with a copy-friendly message.</summary>
    Task ShowErrorAsync(string title, string message);

    /// <summary>Lets the user choose a type; returns null when cancelled.</summary>
    Task<TypeSpecifier?> SelectTypeAsync(IEnumerable<TypeSpecifier> types, TypeSpecifier initial);

    /// <summary>Lets the user choose a method (the first one is preselected); returns null when cancelled.</summary>
    Task<MethodSpecifier?> SelectMethodAsync(IEnumerable<MethodSpecifier> methods);

    /// <summary>Shows the references dialog of a project until it is closed.</summary>
    Task ShowReferencesAsync(ReferenceListVM references);
}
