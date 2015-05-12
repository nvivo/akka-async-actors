using System;
using System.Threading.Tasks;

namespace Akka.Actor
{
    public class AsyncReceiveActor : ReceiveActor
    {
        public void ReceiveAsync<T>(Func<T, Task> handler)
        {
            Receive<T>(message =>
            {
                AsyncHelper.ReceiveAsync((ActorCell)Context, handler, message);
            });
        }
    }
}
