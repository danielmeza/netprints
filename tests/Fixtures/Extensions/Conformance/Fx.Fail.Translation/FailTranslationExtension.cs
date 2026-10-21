using Fx.Kit;
using NetPrints.Extensibility;

namespace Fx.Fail.Translation;

/// <summary>An extension whose translator breaks out of the string literal it writes and emits a call to a method that does not exist.</summary>
public sealed class FailTranslationExtension : INetPrintsExtension
{
    private const string BrokenText = "ping\"); ThisMethodDoesNotExist(); System.Console.WriteLine(\"";

    /// <inheritdoc />
    public void Register(IExtensionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder
            .AddNodeLibrary(new FxNodeLibrary("fx.fail.translation", FxKit.Kind("fx.fail.translation/Ping", () => BrokenText)))
            .AddJsonTypeInfoResolver(FxKitJsonContext.Default);
    }
}
