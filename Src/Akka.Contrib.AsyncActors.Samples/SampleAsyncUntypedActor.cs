using Akka.Actor;
using Akka.Event;
using System.Threading.Tasks;

namespace Akka.Contrib.AsyncActors.Samples
{
    /// <summary>
    /// This is a sample implementation to show what can be done with an async actor.
    /// This implementation uses an async call only on the first request, and keeps any other messages stashed.
    /// Subsequent messages will be handled syncrhonously and no tasks are created.
    /// </summary>
    public class SampleAsyncUntypedActor : AsyncUntypedActor
    {
        private int? _state;
        ILoggingAdapter Log = Context.GetLogger();

        protected override async Task OnReceiveAsync(object request)
        {
            Log.Info("Incoming  {0}", request);

            // need to keep actorRefs for now, as they will be gone after await
            var self = Self;
            var sender = Sender;

            // this could be reading a file, database query, API call
            if (_state == null)
                _state = await GetInitialState();

            // modify state after await, no messages where processed in between
            _state++;

            Log.Info("Processed {0}, State {1}", request, _state);
        }

        private async Task<int> GetInitialState()
        {
            Log.Info("Awaiting to load data from external source");

            await Task.Delay(500);
            return 42;
        }
    }
}
