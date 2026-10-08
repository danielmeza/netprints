using NetPrints.Core;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Tests.Shell;

/// <summary>An <see cref="IProjectActions"/> that records every call, for command handler tests.</summary>
public sealed class FakeProjectActions : IProjectActions
{
    /// <summary>Gets the calls made, in order, as <c>Method</c> or <c>Method:argument</c>.</summary>
    public List<string> Calls { get; } = [];

    /// <summary>Gets or sets what <see cref="ConfirmUnloadAsync"/> answers.</summary>
    public bool AllowUnload { get; set; } = true;

    /// <summary>Gets the item of the last <see cref="RenameItem"/> or <see cref="DeleteItem"/> call.</summary>
    public object? LastItem { get; private set; }

    /// <summary>Gets the class of the last member or class-settings call.</summary>
    public ClassGraph? LastClass { get; private set; }

    /// <inheritdoc/>
    public Task<bool> ConfirmUnloadAsync(CancellationToken cancellationToken)
    {
        Calls.Add("ConfirmUnload");
        return Task.FromResult(AllowUnload);
    }

    /// <inheritdoc/>
    public Task OpenProjectAsync(string? path, CancellationToken cancellationToken)
    {
        Calls.Add($"OpenProject:{path}");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task OpenDroppedAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        Calls.Add($"OpenDropped:{string.Join(";", paths)}");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task NewProjectAsync(CancellationToken cancellationToken)
    {
        Calls.Add("NewProject");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task OpenSampleAsync(string sampleName, CancellationToken cancellationToken)
    {
        Calls.Add($"OpenSample:{sampleName}");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task CloseProjectAsync(CancellationToken cancellationToken)
    {
        Calls.Add("CloseProject");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ExitAsync(CancellationToken cancellationToken)
    {
        Calls.Add("Exit");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void ShowProjectSettings() => Calls.Add("ShowProjectSettings");

    /// <inheritdoc/>
    public Task ShowReferencesAsync(CancellationToken cancellationToken)
    {
        Calls.Add("ShowReferences");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ShowKeyboardShortcutsAsync(CancellationToken cancellationToken)
    {
        Calls.Add("ShowKeyboardShortcuts");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ShowCommandPaletteAsync(CancellationToken cancellationToken)
    {
        Calls.Add("ShowCommandPalette");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ShowAboutAsync(CancellationToken cancellationToken)
    {
        Calls.Add("ShowAbout");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task NewClassAsync(CancellationToken cancellationToken)
    {
        Calls.Add("NewClass");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task AddExistingClassAsync(CancellationToken cancellationToken)
    {
        Calls.Add("AddExistingClass");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void ShowClassSettings(ClassGraph cls) => Record("ShowClassSettings", cls);

    /// <inheritdoc/>
    public void AddMethod(ClassGraph cls) => Record("AddMethod", cls);

    /// <inheritdoc/>
    public void AddConstructor(ClassGraph cls) => Record("AddConstructor", cls);

    /// <inheritdoc/>
    public void AddVariable(ClassGraph cls) => Record("AddVariable", cls);

    /// <inheritdoc/>
    public void AddEventGraph(ClassGraph cls) => Record("AddEventGraph", cls);

    /// <inheritdoc/>
    public Task OverrideMethodAsync(ClassGraph cls, CancellationToken cancellationToken)
    {
        Record("OverrideMethod", cls);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void RenameItem(object item)
    {
        LastItem = item;
        Calls.Add("RenameItem");
    }

    /// <inheritdoc/>
    public Task DeleteItemAsync(object item, CancellationToken cancellationToken)
    {
        LastItem = item;
        Calls.Add("DeleteItem");
        return Task.CompletedTask;
    }

    private void Record(string call, ClassGraph cls)
    {
        LastClass = cls;
        Calls.Add(call);
    }
}
