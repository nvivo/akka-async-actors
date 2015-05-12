using Akka.Actor;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Akka.Contrib.AsyncActors.Samples
{
    public class Program
    {
        public static void Main()
        {
            var system = ActorSystem.Create("AsyncSample");
            var untyped = system.ActorOf<SampleAsyncUntypedActor>();
            var receive = system.ActorOf<SampleAsyncReceiveActor>();
            var fsm = system.ActorOf<SampleAsyncFSM>();

            WaitPrompt(0, "Press ENTER to call AsyncUntypedActor");

            for (var i = 0; i < 10; i++)
                untyped.Tell(i);

            WaitPrompt(1500, "Press ENTER to call AsyncReceiveActor");

            for (var i = 0; i < 10; i++)
                receive.Tell(i);

            WaitPrompt(1500, "Press ENTER to call AsyncFSM");

            for (var i = 0; i < 10; i++)
                fsm.Tell(i);
            
            WaitPrompt(3000, "Press ENTER to exit");
        }

        static void WaitPrompt(int delay, string msg)
        {
            Thread.Sleep(delay);
            Console.WriteLine();
            Console.WriteLine(msg);
            Console.ReadLine();
        }
    }
}
