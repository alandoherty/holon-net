using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
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
    class LoginRequestMsg
    {
        [ProtoMember(1)]
        public string Username { get; set; }

        [ProtoMember(2)]
        public string Password { get; set; }
    }

    [RpcContract]
    interface ITest001
    {
        [RpcOperation(NoReply = true)]
        Task Login(LoginRequestMsg login);
    }

    class Test001 : ITest001
    {
        static int si = 0;
        int i = Interlocked.Increment(ref si);

        public async Task Login(LoginRequestMsg login) {
            Console.WriteLine($"Worker Waiting {i} - Username: {login.Username} Password: {login.Password}");
            await Task.Delay(15000);
        }
    }

    class Program
    {
        static void Main(string[] args) => AsyncMain(args).Wait();

        private static bool go = true;

        public static async void ReadLoop() {
            while (true) {
                string line = await Task.Run(() => Console.ReadLine());

                if (line == "s") {
                    Console.WriteLine("STOPPED!");
                    go = false;
                } else
                    go = true;
            }
        }

        static async Task AsyncMain(string[] args) {
            // attach node
            Node node = await Node.CreateFromEnvironmentAsync();

            Service service = await node.AttachAsync("auth:login", ServiceType.Balanced, ServiceExecution.Parallel, RpcBehaviour.BindOne<ITest001>(new Test001()));
            Service service2 = await node.AttachAsync("auth:login", ServiceType.Balanced, ServiceExecution.Parallel, RpcBehaviour.BindOne<ITest001>(new Test001()));

            ITest001 test = node.Proxy<ITest001>("auth:login");

            ReadLoop();

            while (true) {
                if (go) {
                    await test.Login(new LoginRequestMsg() { Username = "alan", Password = "bacon" });
                }
                await Task.Delay(100).ConfigureAwait(false);
            }

            await Task.Delay(50000);
        }
    }
}
