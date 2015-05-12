using Akka.Dispatch;
using Akka.Dispatch.SysMsg;
using System;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace Akka.Actor
{
    internal static class AsyncHelper
    {
        public static Task ReceiveAsync<T>(ActorCell context, Func<T, Task> handler, T message)
        {
            var task = handler(message);

            // if task is null, treat as synchronous execution
            if (task == null)
                return task;

            if (task.IsFaulted)
                ExceptionDispatchInfo.Capture(task.Exception.InnerException).Throw();

            // if task is completed, return synchronously
            if (task.IsCompleted)
                return task;

            // otherwise the task is still running,
            // suspend the mailbox and until the task is complete
            context.Mailbox.Suspend(MailboxSuspendStatus.AwaitingTask);

            return task.ContinueWith(t =>
            {
                try
                {
                    if (t.IsFaulted)
                    {
                        var state = new AmbientState { Self = context.Self, Sender = context.Sender };
                        var edi = ExceptionDispatchInfo.Capture(t.Exception.InnerException);
                        context.Self.Tell(new CompleteTask(state, () => edi.Throw()));
                    }
                }
                finally
                {
                    context.Mailbox.Resume(MailboxSuspendStatus.AwaitingTask);
                }
            },
            TaskContinuationOptions.ExecuteSynchronously);
        }
    }
}
