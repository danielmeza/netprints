namespace EventGraphs
{
    public class GameEvents : EventGraphs.EventBase
    {
        // OnStart Entry
        public void OnStart()
        {
            // Variables
            // Console.WriteLine
            System.Console.WriteLine("OnStart!");
        }

        // OnTick Entry
        public void OnTick(System.Single vardeltaTime)
        {
            // Variables
            // Console.WriteLine
            System.Console.WriteLine(vardeltaTime);
        }

        // OnReset Entry
        public override void OnReset()
        {
        // Variables
        }
    }
}