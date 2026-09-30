namespace AnnotatedSample
{
    /// <summary>Builds greetings.</summary>
    [NetPrints.Annotations.NetPrintsType(DisplayName = "Greeter", Category = "Sample")]
    public sealed class Greeter
    {
        /// <summary>Builds a greeting.</summary>
        /// <param name="name">Who to greet.</param>
        /// <returns>The greeting.</returns>
        [NetPrints.Annotations.NetPrintsNode(DisplayName = "Greet", Category = "Sample", Keywords = new[] { "hello", "welcome" })]
        public static string Greet(string name)
        {
            return "Hello, " + name + "!";
        }

        /// <summary>Counts the greetings made so far.</summary>
        /// <returns>The count.</returns>
        [NetPrints.Annotations.NetPrintsNode]
        public int Count()
        {
            return 0;
        }

        /// <summary>Not part of the catalog.</summary>
        [NetPrints.Annotations.NetPrintsIgnore]
        public string Secret { get; set; }
    }
}
