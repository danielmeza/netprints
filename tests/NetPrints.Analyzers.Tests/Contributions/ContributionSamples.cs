namespace NetPrints.Analyzers.Tests.Contributions;

/// <summary>Samples that build contribution descriptors, against stand-ins for the extension API types the analyzers read by name.</summary>
internal static class ContributionSamples
{
    private const string Types =
        """
        namespace NetPrints.Extensibility.Editor
        {
            public enum CommandScope { Global = 0, Graph = 1 }

            [System.Flags]
            public enum CommandModifiers { None = 0, Ctrl = 1, Alt = 2, Shift = 4, Meta = 8 }

            public sealed class CommandKey
            {
                public static readonly CommandKey S = new();
                public static readonly CommandKey F5 = new();
                public static readonly CommandKey Delete = new();
            }

            public sealed record CommandGesture(CommandModifiers Modifiers, CommandKey Key)
            {
                public static CommandGesture Parse(string text, System.IFormatProvider? provider = null) => new(CommandModifiers.None, CommandKey.S);

                public static bool TryParse(string? text, out CommandGesture? gesture)
                {
                    gesture = null;
                    return false;
                }
            }

            public sealed record MenuPlacement(string Path, string Group, int Order, string? Submenu = null);

            public sealed record CommandDescriptor(
                string Id,
                string Label,
                object Handler,
                string? IconId = null,
                System.Collections.Generic.IReadOnlyList<CommandGesture>? DefaultGestures = null,
                CommandScope Scope = CommandScope.Global,
                MenuPlacement? Menu = null,
                int? CommandBarOrder = null);

            public sealed record PanelDescriptor(string Id, string Title, string? IconId = null);
        }
        """;

    private const string Icons =
        """
        namespace NetPrints.Editor.Icons
        {
            public static class IconIds
            {
                public const string Save = "netprints.icon.save";
            }
        }
        """;

    /// <summary>Wraps statements into a method of a class that sees the extension API stand-ins and the built-in icon ids.</summary>
    /// <param name="body">The statements.</param>
    /// <param name="withIcons">Whether the compilation declares the built-in icon ids.</param>
    /// <returns>The source.</returns>
    public static string Use(string body, bool withIcons = true) =>
        Types + "\n" + (withIcons ? Icons + "\n" : string.Empty)
        + "namespace Use\n{\n    using NetPrints.Extensibility.Editor;\n\n    class C\n    {\n        const string Prefix = \"netprints.command.\";\n\n        void M(object handler, string text)\n        {\n"
        + body + "\n        }\n    }\n}\n";
}
