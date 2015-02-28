using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;

namespace Akka.Actor
{
    public abstract class AsyncUntypedActor : UntypedActor, WithUnboundedStash
    {
        public IStash Stash { get; set; }
        bool _awaiting;
        readonly object AwaitComplete = new object();

        protected sealed override void OnReceive(object message)
        {
            // if awaiting, only complete event or exception should be handled
            // anything else should be stashed
            if (_awaiting)
            {
                if (message == AwaitComplete)
                {
                    _awaiting = false;
                    Stash.UnstashAll();
                    return;
                }

                if (message is ExceptionDispatchInfo && Self.Equals(Sender))
                {
                    _awaiting = false;
                    Stash.UnstashAll();
                    ((ExceptionDispatchInfo)message).Throw();
                }

                Stash.Stash();
                return;
            }

            var task = OnReceiveAsync(message);

            // if task is null, treat as synchronous execution
            if (task == null)
                return;

            if (task.IsFaulted)
                ExceptionDispatchInfo.Capture(task.Exception.InnerException).Throw();

            // if task is completed, return synchronously
            if (task.IsCompleted)
                return;

            // otherwise the task is still running,
            // set the flag and stash any new messages until the task is complete
            var self = Self;
            _awaiting = true;

            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                    self.Tell(ExceptionDispatchInfo.Capture(t.Exception.InnerException), self);
                else
                    self.Tell(AwaitComplete, ActorRef.NoSender);
            });
        }

        protected abstract Task OnReceiveAsync(object message);
    }
}
