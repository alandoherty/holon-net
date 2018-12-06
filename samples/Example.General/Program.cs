using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Holon;
using Holon.Events;
using Holon.Metrics;
using Holon.Remoting;
using Holon.Remoting.Serializers;
using Holon.Security;
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
        [RpcOperation(NoReply = false)]
        Task<string> Login(LoginRequestMsg login);
    }

    class Test001 : ITest001
    {
        private Guid _uuid;

        public async Task<string> Login(LoginRequestMsg login) {
            Console.WriteLine($"Worker ({_uuid}) - Username: {login.Username} Password: {login.Password}");
            RpcContext context = RpcContext.Current;

            return "Wow";
        }

        public Test001(Guid uuid) {
            _uuid = uuid;
        }
    }

    class Program
    {
        public static Node TestNode { get; set; }

        static void Main(string[] args) => AsyncMain(args).Wait();

        class EventTest
        {
            public string Potato { get; set; }
        }
        static async Task AsyncMain(string[] args) {
            // attach node
            TestNode = await Node.CreateFromEnvironmentAsync(new NodeConfiguration() {
                ThrowUnhandledExceptions = true
            });

            TestNode.TraceBegin += (o, e) => Console.WriteLine($"Begin trace {e.TraceId} for {e.Envelope.ID} at {DateTime.UtcNow}");
            TestNode.TraceEnd += (o, e) => Console.WriteLine($"End trace {e.TraceId} for {e.Envelope.ID} at {DateTime.UtcNow}");

            // attach
            await TestNode.AttachAsync("auth:login", RpcBehaviour.Bind<ITest001>(new Test001(Guid.NewGuid())));

            ITest001 proxy = TestNode.Proxy<ITest001>("auth:login", new ProxyConfiguration() {
                TraceId = Guid.NewGuid().ToString()
            });

            await proxy.Login(new LoginRequestMsg() {
                Password = "password",
                Username = "username"
            });

            await Task.Delay(50000);
        }
    }
}
