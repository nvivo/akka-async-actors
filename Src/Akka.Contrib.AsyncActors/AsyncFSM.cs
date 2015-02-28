using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;

namespace Akka.Actor
{
    public class AsyncFSM<TState, TData> : FSM<TState, TData>, WithUnboundedStash
    {
        public IStash Stash { get; set; }
        bool _awaiting;
        
        public void WhenAsync(TState stateName, Func<Event<TData>, Task<State<TState, TData>>> func)
        {
            When(stateName, e =>
            {
                if (_awaiting)
                {
                    // during FSM await, we keep the same state until the task is complete
                    // only messages from itself should be allowed, anything else is stashed to be handled when await finishes
                    if (Self.Equals(Sender))
                    {
                        // FSM returns GoTo/Stay states instead of completion events
                        // once it's received from the inner task, return it to FSM
                        if (e.FsmEvent is State<TState, TData>)
                        {
                            _awaiting = false;
                            Stash.UnstashAll();
                            return (State<TState, TData>)e.FsmEvent;
                        }

                        if (e.FsmEvent is ExceptionDispatchInfo)
                        {
                            _awaiting = false;
                            Stash.UnstashAll();
                            ((ExceptionDispatchInfo)e.FsmEvent).Throw();
                        }
                    }

                    if (e.FsmEvent != null)
                        Stash.Stash();

                    return Stay();
                }

                var task = func(e);

                // handle null returns as "Stay", per FSM convention
                if (task == null)
                    return Stay();

                // handle exceptions as if it was synchronous
                if (task.IsFaulted)
                    ExceptionDispatchInfo.Capture(task.Exception.InnerException).Throw();

                // if task is completed, shortcut to return directly whatever it returned
                if (task.IsCompleted)
                    return task.Result;

                // otherwise the task is still running,
                // set the flag and stash any new messages until the task is complete
                var self = Self;
                _awaiting = true;

                task.ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        self.Tell(ExceptionDispatchInfo.Capture(t.Exception.InnerException), self);
                    else
                        self.Tell(t.Result, self);
                });

                return Stay();
            });
        }
    }
}
