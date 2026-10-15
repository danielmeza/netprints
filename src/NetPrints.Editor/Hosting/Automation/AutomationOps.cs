namespace NetPrints.Editor.Hosting.Automation;

/// <summary>The operation names of the read-only automation protocol, shared by the agent and its clients.</summary>
public static class AutomationOps
{
    /// <summary>The editor's status (<see cref="AutomationStatus"/>).</summary>
    public const string Status = "status";

    /// <summary>The elements matching an <see cref="AutomationQuery"/>.</summary>
    public const string Find = "find";

    /// <summary>A text dump of the element tree.</summary>
    public const string Dump = "dump";

    /// <summary>The state of the last program the editor launched.</summary>
    public const string RunState = "runState";

    /// <summary>Every window and every control with an automation id, hidden ones included.</summary>
    public const string Tree = "tree";

    /// <summary>Returns once everything queued before the request, including layout, has run.</summary>
    public const string Settle = "settle";
}
