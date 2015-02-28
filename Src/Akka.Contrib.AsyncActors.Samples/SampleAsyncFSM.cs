using Akka.Actor;
using Akka.Event;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    public class SampleAsyncFSM : AsyncFSM<int, object>
    {
        const int IDLE = 0;
        const int PROCESSING = 1;
        
        LoggingAdapter Log = Context.GetLogger();
        int _state = 0;

        class ProcessingResult
        {
            public int Request;
            public int State;
        }

        public SampleAsyncFSM()
        {
            StartWith(IDLE, null);

            When(IDLE, e =>
            {
                if (e.FsmEvent is int)
                {
                    Log.Info("Incoming  " + e.FsmEvent);
                    DoSomeBackgroundWork((int)e.FsmEvent);
                    return GoTo(PROCESSING);
                }
                
                return null;
            });

            WhenAsync(PROCESSING, async e =>
            {
                if (e.FsmEvent is ProcessingResult)
                {
                    var result = (ProcessingResult)e.FsmEvent;

                    await ProcessResult(result.State);

                    Log.Info("Processed {0}, State {1}", result.Request, result.State);

                    Stash.UnstashAll();
                    return GoTo(IDLE);
                }

                Stash.Stash();
                return Stay();
            });
        }

        private void DoSomeBackgroundWork(int request)
        {
            _state = _state + 2;

            // here we are sure a background task will be started, 
            // we send a message to ourselves with the result once its finished
            Task.Delay(50)
                .ContinueWith(_ => {
                    
                    return new ProcessingResult { Request = request, State = _state };
                }).PipeTo(Self);
        }

        private async Task ProcessResult(int result)
        {
            // here, only in some cases this will cause a task
            // to start, most cases will return immediately
            // instead of creating an immaginary message, we use
            // async/await and let it run syncrhonously if needed
            if (result % 5 == 0)
            {
                Log.Info("Need some external work on this one, awaiting...");
                await Task.Delay(10);
            }
        }
    }
}
