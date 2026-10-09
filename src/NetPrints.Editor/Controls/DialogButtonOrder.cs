using Avalonia.Controls;

namespace NetPrints.Editor.Controls;

/// <summary>
/// The order of a dialog's footer buttons on a platform: Windows puts the default button first and the cancel button
/// last, macOS and Linux put the cancel button first and the default button last. Each order is a member with its own
/// ranking; <see cref="DialogShell.ButtonOrder"/> takes the order as a value so a test can pick either one.
/// </summary>
public abstract class DialogButtonOrder
{
    /// <summary>Default button first, cancel button last (Windows).</summary>
    public static readonly DialogButtonOrder DefaultFirst = new DefaultFirstOrder();

    /// <summary>Cancel button first, default button last (macOS and Linux).</summary>
    public static readonly DialogButtonOrder CancelFirst = new CancelFirstOrder();

    private const int First = 0;
    private const int Middle = 1;
    private const int Last = 2;

    private DialogButtonOrder()
    {
    }

    /// <summary>Gets the order a platform uses.</summary>
    /// <param name="isWindows">Whether the platform is Windows.</param>
    /// <returns><see cref="DefaultFirst"/> on Windows, <see cref="CancelFirst"/> elsewhere.</returns>
    public static DialogButtonOrder For(bool isWindows) => isWindows ? DefaultFirst : CancelFirst;

    /// <summary>Gets the order of the platform the editor runs on.</summary>
    /// <returns>The order for the current operating system.</returns>
    public static DialogButtonOrder ForCurrentPlatform() => For(OperatingSystem.IsWindows());

    /// <summary>Puts the footer actions in this order; actions of the same rank keep the order they were declared in.</summary>
    /// <param name="actions">The footer actions as declared.</param>
    /// <returns>The actions in display order, left to right.</returns>
    public IReadOnlyList<Control> Arrange(IEnumerable<Control> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        return [.. actions.OrderBy(Rank)];
    }

    private protected abstract int Rank(Control action);

    private sealed class DefaultFirstOrder : DialogButtonOrder
    {
        private protected override int Rank(Control action) => action switch
        {
            Button { IsDefault: true } => First,
            Button { IsCancel: true } => Last,
            _ => Middle,
        };
    }

    private sealed class CancelFirstOrder : DialogButtonOrder
    {
        private protected override int Rank(Control action) => action switch
        {
            Button { IsCancel: true } => First,
            Button { IsDefault: true } => Last,
            _ => Middle,
        };
    }
}
