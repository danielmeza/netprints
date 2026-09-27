// Hand-written base type for the "EventGraphs" golden fixture (T079): GameEvents.netpc.json declares
// EventGraphs.EventBase as its super type so its OnReset event entry is a genuine override of a real
// virtual method. Documentation only: excluded from compilation like every other Fixtures/**/*.cs file
// (NetPrints.Core.Tests.csproj), never built or run.
namespace EventGraphs
{
    public class EventBase
    {
        public virtual void OnReset()
        {
        }
    }
}
