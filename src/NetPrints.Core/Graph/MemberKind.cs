namespace NetPrints.Graph
{
    /// <summary>
    /// The kinds of class member a node can refer to. Closed on purpose: C# member kinds are a stable language set.
    /// </summary>
    public enum MemberKind
    {
        /// <summary>A method.</summary>
        Method,

        /// <summary>A member variable.</summary>
        Variable,

        /// <summary>A custom event entry, which generates a method.</summary>
        Event,
    }
}
