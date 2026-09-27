#nullable enable

namespace NetPrints.Translator
{
    /// <summary>
    /// C# modifier keywords shared by <see cref="ClassTranslator"/> and
    /// <see cref="ExecutionGraphTranslator"/>, so each keyword is spelled once.
    /// </summary>
    internal static class CSharpKeywords
    {
        /// <summary>The <c>partial</c> modifier.</summary>
        public const string Partial = "partial";

        /// <summary>The <c>static</c> modifier.</summary>
        public const string Static = "static";

        /// <summary>The <c>abstract</c> modifier.</summary>
        public const string Abstract = "abstract";

        /// <summary>The <c>sealed</c> modifier.</summary>
        public const string Sealed = "sealed";

        /// <summary>The <c>unsafe</c> modifier.</summary>
        public const string Unsafe = "unsafe";

        /// <summary>The <c>readonly</c> modifier.</summary>
        public const string ReadOnly = "readonly";

        /// <summary>The <c>new</c> modifier.</summary>
        public const string New = "new";

        /// <summary>The <c>override</c> modifier.</summary>
        public const string Override = "override";

        /// <summary>The <c>virtual</c> modifier.</summary>
        public const string Virtual = "virtual";
    }
}
