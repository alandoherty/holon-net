using Holon.Events;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Holon.Tests
{
    public class Events
    {
        [Fact]
        public async Task EmitAndSubscribe()
        {
            // connect
            //await Node.CreateAsync("amqp://localhost");

            // event completion
            TaskCompletionSource<Event> tcs = new TaskCompletionSource<Event>();
        }
    }
}
