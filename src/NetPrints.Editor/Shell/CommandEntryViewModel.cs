using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetPrints.Editor.Contributions;

namespace NetPrints.Editor.Shell;

/// <summary>One command as a menu or command bar surface shows it, or a menu divider.</summary>
public sealed partial class CommandEntryViewModel : ObservableObject
{
    private readonly CommandDescriptor? descriptor;
    private readonly CommandInvoker? invoker;
    private readonly CommandScope scope;
    private readonly object? parameter;

    private CommandEntryViewModel()
    {
    }

    internal CommandEntryViewModel(CommandDescriptor descriptor, CommandInvoker invoker, string automationPrefix, CommandScope scope = CommandScope.Global, object? parameter = null)
    {
        this.scope = scope;
        this.parameter = parameter;
        this.descriptor = descriptor;
        this.invoker = invoker;
        Id = descriptor.Id;
        AutomationId = automationPrefix + descriptor.Id;
        IconId = descriptor.IconId;
        StaticLabel = descriptor.Label;
        FirstGesture = descriptor.DefaultGestures is { Count: > 0 } gestures ? gestures[0] : "";
        Refresh();
    }

    /// <summary>Gets the divider between two groups of a menu.</summary>
    public static CommandEntryViewModel Separator { get; } = new() { IsSeparator = true };

    /// <summary>Gets a value indicating whether this entry is a divider.</summary>
    public bool IsSeparator { get; private init; }

    /// <summary>Gets the command id, or an empty string for a divider.</summary>
    public string Id { get; } = "";

    /// <summary>Gets the automation id, derived from the command id.</summary>
    public string AutomationId { get; } = "";

    /// <summary>Gets the Icon id (see IconIds), or null.</summary>
    public string? IconId { get; }

    /// <summary>Gets the descriptor's own label, which a command bar button shows however the handler's label reads.</summary>
    public string StaticLabel { get; } = "";

    /// <summary>Gets the first shortcut as written, or an empty string.</summary>
    public string FirstGesture { get; } = "";

    /// <summary>Gets the label, the handler's dynamic one (such as <c>Undo Add node</c>) when it has one.</summary>
    [ObservableProperty]
    public partial string Label { get; private set; } = "";

    /// <summary>Gets the tooltip: <c>"&lt;Label&gt; (&lt;first shortcut&gt;)"</c>, or just the label without a shortcut.</summary>
    [ObservableProperty]
    public partial string Tooltip { get; private set; } = "";

    /// <summary>Gets a value indicating whether the command can run now.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunCommand))]
    public partial bool IsEnabled { get; private set; }

    /// <summary>Gets or sets the error count shown as a badge; zero shows none.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBadge), nameof(BadgeText), nameof(BadgeName))]
    public partial int BadgeCount { get; set; }

    /// <summary>Gets a value indicating whether the badge shows.</summary>
    public bool HasBadge => BadgeCount > 0;

    /// <summary>Gets the badge text, the error count.</summary>
    public string BadgeText => BadgeCount.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Gets the accessible name of the badge.</summary>
    public string BadgeName => BadgeCount == 1 ? "1 error" : $"{BadgeText} errors";

    /// <summary>Re-queries the handler for the label and the enabled state.</summary>
    public void Refresh()
    {
        if (descriptor is null || invoker is null)
        {
            return;
        }

        CommandContext context = invoker.CreateContext(scope, parameter);
        Label = descriptor.Handler.DynamicLabel(context) ?? descriptor.Label;
        Tooltip = FirstGesture.Length > 0 ? $"{Label} ({FirstGesture})" : Label;
        IsEnabled = descriptor.Handler.CanExecute(context);
    }

    [RelayCommand(CanExecute = nameof(IsEnabled))]
    private void Run()
    {
        if (descriptor is not null)
        {
            invoker?.TryRun(descriptor, scope, parameter);
        }
    }
}
