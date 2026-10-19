using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Controls;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// An extension's data template whose failure is contained (FR-034, research R27): when building the view throws, the
/// <see cref="ContributionErrorView"/> names the extension in its place and the failure is reported. The wrapper has the
/// capabilities of the template it wraps and no others; <see cref="Wrap"/> picks the class that fits.
/// </summary>
internal class ContainedDataTemplate : IDataTemplate
{
    private readonly Action<Exception>? failed;

    private ContainedDataTemplate(IDataTemplate inner, string extensionName, Action<Exception>? failed)
    {
        Inner = inner;
        ExtensionName = extensionName;
        this.failed = failed;
    }

    /// <summary>Gets the extension's template.</summary>
    public IDataTemplate Inner { get; }

    /// <summary>Gets the display name of the extension.</summary>
    public string ExtensionName { get; }

    /// <summary>Wraps <paramref name="inner"/> in the contained template that has the same capabilities.</summary>
    /// <param name="inner">The extension's template.</param>
    /// <param name="extensionName">The display name of the extension.</param>
    /// <param name="failed">Reports a failure; may be <see langword="null"/>.</param>
    /// <returns>The contained template.</returns>
    public static ContainedDataTemplate Wrap(IDataTemplate inner, string extensionName, Action<Exception>? failed)
    {
        ArgumentNullException.ThrowIfNull(inner);
        bool recycling = inner is IRecyclingDataTemplate;
        bool typed = inner is ITypedDataTemplate;
        return inner is ITreeDataTemplate
            ? (recycling, typed) switch
            {
                (true, true) => new TreeRecyclingTyped(inner, extensionName, failed),
                (true, false) => new TreeRecycling(inner, extensionName, failed),
                (false, true) => new TreeTyped(inner, extensionName, failed),
                _ => new Tree(inner, extensionName, failed),
            }
            : (recycling, typed) switch
            {
                (true, true) => new RecyclingTyped(inner, extensionName, failed),
                (true, false) => new Recycling(inner, extensionName, failed),
                (false, true) => new Typed(inner, extensionName, failed),
                _ => new ContainedDataTemplate(inner, extensionName, failed),
            };
    }

    /// <inheritdoc/>
    public bool Match(object? data) => Inner.Match(data);

    /// <inheritdoc/>
    public Control? Build(object? param) => Contain(() => Inner.Build(param), param);

    private protected Control? Contain(Func<Control?> build, object? param)
    {
        Control? view;
        try
        {
            view = build();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Placeholder(ex, param);
        }

        return view is null ? null : new ContainedView(view, ex => Placeholder(ex, param));
    }

    private protected Control? Recycle(IRecyclingDataTemplate inner, object? data, Control? existing)
    {
        if (existing is not ContainedView { HasFailed: false } held)
        {
            return Contain(() => inner.Build(data, null), data);
        }

        Control? view;
        try
        {
            view = inner.Build(data, held.Child);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Placeholder(ex, data);
        }

        return ReferenceEquals(view, held.Child) ? held : view is null ? null : new ContainedView(view, ex => Placeholder(ex, data));
    }

    private ContributionErrorView Placeholder(Exception ex, object? param)
    {
        failed?.Invoke(ex);
        return new ContributionErrorView
        {
            DataContext = new ContributionErrorViewModel(ExtensionName, "the view of " + (param?.GetType().Name ?? "nothing"), ex.Message),
        };
    }

    private class Recycling(IDataTemplate inner, string extensionName, Action<Exception>? failed) : ContainedDataTemplate(inner, extensionName, failed), IRecyclingDataTemplate
    {
        public Control? Build(object? data, Control? existing) =>
            Recycle((IRecyclingDataTemplate)Inner, data, existing);
    }

    private sealed class Typed(IDataTemplate inner, string extensionName, Action<Exception>? failed) : ContainedDataTemplate(inner, extensionName, failed), ITypedDataTemplate
    {
        public Type? DataType => ((ITypedDataTemplate)Inner).DataType;
    }

    private sealed class RecyclingTyped(IDataTemplate inner, string extensionName, Action<Exception>? failed) : Recycling(inner, extensionName, failed), ITypedDataTemplate
    {
        public Type? DataType => ((ITypedDataTemplate)Inner).DataType;
    }

    private class Tree(IDataTemplate inner, string extensionName, Action<Exception>? failed) : ContainedDataTemplate(inner, extensionName, failed), ITreeDataTemplate
    {
        public IDisposable BindChildren(AvaloniaObject target, AvaloniaProperty targetProperty, object item) =>
            ((ITreeDataTemplate)Inner).BindChildren(target, targetProperty, item);
    }

    private sealed class TreeTyped(IDataTemplate inner, string extensionName, Action<Exception>? failed) : Tree(inner, extensionName, failed), ITypedDataTemplate
    {
        public Type? DataType => ((ITypedDataTemplate)Inner).DataType;
    }

    private class TreeRecycling(IDataTemplate inner, string extensionName, Action<Exception>? failed) : Tree(inner, extensionName, failed), IRecyclingDataTemplate
    {
        public Control? Build(object? data, Control? existing) =>
            Recycle((IRecyclingDataTemplate)Inner, data, existing);
    }

    private sealed class TreeRecyclingTyped(IDataTemplate inner, string extensionName, Action<Exception>? failed) : TreeRecycling(inner, extensionName, failed), ITypedDataTemplate
    {
        public Type? DataType => ((ITypedDataTemplate)Inner).DataType;
    }
}
