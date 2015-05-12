using Akka.Actor;
using Akka.Event;
using System.Threading.Tasks;

namespace Akka.Contrib.AsyncActors.Samples
{
    /// <summary>
    /// This FSM mixes PipeTo with async/await. 
    /// 
    /// PipeTo is used to start the background the processing where you
    /// know for sure the code is going to run in another thread.
    /// 
    /// Async/await is used to handle cases where your processing
    /// may or may not run asynchronously or when you need to call
    /// multiple methods that returns tasks and async/await is just
    /// simpler than breaking into multiple messages.
    /// </summary>
    public class SampleAsyncFSM : AsyncFSM<int, object>, IWithUnboundedStash
    {
        const int IDLE = 0;
        const int PROCESSING = 1;

        ILoggingAdapter Log = Context.GetLogger();
        public IStash Stash { get; set; }

        public SampleAsyncFSM()
        {
            StartWith(IDLE, null);

            When(IDLE, e =>
            {
                if (e.FsmEvent is int)
                {
                    var i = (int)e.FsmEvent;
                    
                    Log.Info("Received:" + i);
                    var self = Self;

                    Self.Tell(new ProcessingResult(i));
                    Self.Tell(new ProcessingResult(i));
                    Self.Tell(new ProcessingResult(i, true));

                    return GoTo(PROCESSING);
                }
                
                return null;
            });

            WhenAsync(PROCESSING, async e =>
            {
                var result = e.FsmEvent as ProcessingResult;

                if (result != null)
                {
                    Log.Info("Processed: {0}, {1}", result.Value, result.IsLastValue ? "continuing synchronously" : "awaiting...");

                    if (!result.IsLastValue)
                    {
                        await Task.Delay(100);
                        return Stay();
                    }
                    else
                    {
                        Stash.UnstashAll();
                        return GoTo(IDLE);
                    }
                }

                Stash.Stash();
                return Stay();
            });
        }

        class ProcessingResult
        {
            public ProcessingResult(int value, bool isLastValue = false)
            {
                Value = value;
                IsLastValue = isLastValue;
            }

            public int Value;
            public bool IsLastValue;
        }
    }
}
