using NetPrints.Editor.Hosting;

namespace NetPrints.Editor.Settings;

/// <summary>What the generated fields need from the host.</summary>
/// <param name="Picker">Picks a file or folder for a path field, or <see langword="null"/> when the host has no picker.</param>
/// <param name="Choices">Lists the choices of the provider a field names with <c>x-netprints-choices</c>; <see langword="null"/> for a provider that is unknown.</param>
/// <param name="ChoiceLabels">Gives the texts of a provider's values, or <see langword="null"/> to show the values.</param>
internal sealed record SettingsFieldServices(IFilePickerService? Picker, Func<string, IReadOnlyList<string>?> Choices, Func<string, Func<string, string>?>? ChoiceLabels = null);
