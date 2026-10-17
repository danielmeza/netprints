namespace NetPrints.Editor.NodeSearch;

/// <summary>The category names the search groups its rows under, in the text the list shows.</summary>
internal static class SearchCategories
{
    /// <summary>Node kinds of the editor, and the entries of an event graph.</summary>
    public const string NetPrints = "NetPrints";

    /// <summary>Methods of the class and its base types.</summary>
    public const string ThisMethods = "This Methods";

    /// <summary>Variables of the class and its base types.</summary>
    public const string ThisVariables = "This Variables";

    /// <summary>Static methods.</summary>
    public const string StaticMethods = "Static Methods";

    /// <summary>Static variables.</summary>
    public const string StaticVariables = "Static Variables";

    /// <summary>The local variables of the open method or constructor.</summary>
    public const string MethodVariables = "Method Variables";

    /// <summary>Variables of the type of the dragged pin.</summary>
    public const string PinVariables = "Pin Variables";

    /// <summary>Methods of the type of the dragged pin.</summary>
    public const string PinMethods = "Pin Methods";

    /// <summary>Types.</summary>
    public const string Types = "Types";

    /// <summary>Static methods of the type a type pin carries.</summary>
    public const string PinStaticMethods = "Pin Static Methods";

    /// <summary>Generic types.</summary>
    public const string GenericTypes = "Generic Types";

    /// <summary>Static methods with generic arguments.</summary>
    public const string GenericStaticMethods = "Generic Static Methods";
}
