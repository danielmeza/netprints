using Microsoft.Extensions.Options;

namespace NetPrints.Extensibility.Settings;

/// <summary>
/// Validates <see cref="NetPrintsSettings"/> through the DataAnnotations on its members (generated).
/// </summary>
[OptionsValidator]
internal sealed partial class NetPrintsSettingsValidator : IValidateOptions<NetPrintsSettings>;
