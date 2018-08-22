using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Holon;
using Holon.Events;
using Holon.Introspection;
using Holon.Metrics;
using Holon.Remoting;
using Holon.Remoting.Serializers;
using Holon.Services;
using ProtoBuf;

namespace Example.General
{
    [ProtoContract]
    class MySerializedData
    {
        [ProtoMember(1)]
        public string Name { get; set; } = "potato";

        [ProtoMember(2)]
        public int wow = 432;
    }

    [RpcContract]
    interface ITest001
    {
        [RpcOperation]
        Task Test(string wow);
    }

    class Test001 : ITest001
    {
        public async Task Test(string wow) {
            Console.WriteLine("Test");
        }
    }

    class Program
    {
        static void Main(string[] args) => AsyncMain(args).Wait();

        static async Task AsyncMain(string[] args) {
            // create context
            DistributedContext ctx = await DistributedContext.CreateAsync("amqp://localhost");

            // attach node
            Node node = await ctx.AttachAsync();

            Service service = await node.AttachAsync("test:test", ServiceType.Balanced, ServiceExecution.Parallel, RpcBehaviour.BindOne<ITest001>(new Test001()));

            ITest001 test = node.Proxy<ITest001>("test:test");
            await test.Test();

            await Task.Delay(50000);
        }
    }
}
