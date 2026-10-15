namespace NetPrints.Editor.Events;

/// <summary>The text of a refused edit: the message of the <see cref="ArgumentException"/> without the parameter name .NET appends.</summary>
internal static class RefusalMessage
{
    internal static string Of(ArgumentException refused)
    {
        string suffix = $" (Parameter '{refused.ParamName}')";
        return refused.ParamName is not null && refused.Message.EndsWith(suffix, StringComparison.Ordinal) ? refused.Message[..^suffix.Length] : refused.Message;
    }
}
