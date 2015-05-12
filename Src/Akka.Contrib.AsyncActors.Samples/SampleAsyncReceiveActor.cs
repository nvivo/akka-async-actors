using Akka.Actor;
using Akka.Event;
using System.Threading.Tasks;

namespace Akka.Contrib.AsyncActors.Samples
{
    public class SampleAsyncReceiveActor : AsyncReceiveActor
    {
        int? _state;
        ILoggingAdapter Log = Context.GetLogger();

        public SampleAsyncReceiveActor()
        {
            ReceiveAsync<int>(async i =>
            {
                Log.Info("Incoming  {0}", i);
                
                if (_state == null)
                    _state = await GetInitialState();

                _state++;

                Log.Info("Processed {0}, State {1}", i, _state);
            });
        }

        private async Task<int> GetInitialState()
        {
            Log.Info("Awaiting to load data from external source");

            await Task.Delay(500);
            return 42;
        }
    }
}
