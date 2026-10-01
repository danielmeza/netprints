namespace AnnotatedFixture
{
    /// <summary>Builds greetings.</summary>
    [NetPrints.Annotations.NetPrintsType(DisplayName = "Greeter", Category = "Sample")]
    public sealed class Greeter
    {
        /// <summary>Builds a greeting.</summary>
        /// <param name="name">Who to greet.</param>
        /// <returns>The greeting.</returns>
        [NetPrints.Annotations.NetPrintsNode(DisplayName = "Greet", Category = "Sample")]
        public static string Greet(string name) => "Hello, " + name + "!";

        /// <summary>Counts the greetings made so far.</summary>
        /// <returns>The count.</returns>
        [NetPrints.Annotations.NetPrintsNode]
        public int Count() => 0;

        /// <summary>Not part of the catalog.</summary>
        [NetPrints.Annotations.NetPrintsIgnore]
        public string Undeclared() => "not annotated";
    }

    /// <summary>A public type without annotations: the catalog leaves it out.</summary>
    public sealed class Unlisted
    {
        /// <summary>Not part of the catalog.</summary>
        public int Value() => 1;
    }
}
