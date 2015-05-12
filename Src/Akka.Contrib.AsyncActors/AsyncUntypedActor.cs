using System.Threading.Tasks;

namespace Akka.Actor
{
    public abstract class AsyncUntypedActor : UntypedActor
    {
        protected sealed override void OnReceive(object message)
        {
            AsyncHelper.ReceiveAsync((ActorCell)Context, OnReceiveAsync, message);
        }

        protected abstract Task OnReceiveAsync(object message);
    }
}
