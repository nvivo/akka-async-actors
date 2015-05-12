using System;
using System.Threading.Tasks;

namespace Akka.Actor
{
    public class AsyncFSM<TState, TData> : FSM<TState, TData>
    {
        public void WhenAsync(TState stateName, Func<Event<TData>, Task<State<TState, TData>>> func)
        {
            When(stateName, e =>
            {
                var receiveTask = AsyncHelper.ReceiveAsync((ActorCell) Context, func, e);
                var originalTask = receiveTask as Task<State<TState, TData>>;

                if (originalTask != null && originalTask.IsCompleted)
                    return originalTask.Result;
                else
                    return Stay();
            });
        }
    }
}
