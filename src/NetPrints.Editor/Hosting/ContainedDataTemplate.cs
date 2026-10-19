using Avalonia.Controls;
using Avalonia.Controls.Templates;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Controls;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// An extension's data template whose failure is contained (FR-034, research R27): when building the view throws, the
/// <see cref="ContributionErrorView"/> names the extension in its place and the failure is reported.
/// </summary>
/// <param name="inner">The extension's template.</param>
/// <param name="extensionName">The display name of the extension.</param>
/// <param name="failed">Reports a failure; may be <see langword="null"/>.</param>
internal sealed class ContainedDataTemplate(IDataTemplate inner, string extensionName, Action<Exception>? failed) : IDataTemplate
{
    /// <summary>Gets the extension's template.</summary>
    public IDataTemplate Inner { get; } = inner;

    /// <summary>Gets the display name of the extension.</summary>
    public string ExtensionName { get; } = extensionName;

    /// <inheritdoc/>
    public bool Match(object? data) => Inner.Match(data);

    /// <inheritdoc/>
    public Control? Build(object? param)
    {
        try
        {
            return Inner.Build(param);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            failed?.Invoke(ex);
            return new ContributionErrorView
            {
                DataContext = new ContributionErrorViewModel(ExtensionName, "the view of " + (param?.GetType().Name ?? "nothing"), ex.Message),
            };
        }
    }
}
