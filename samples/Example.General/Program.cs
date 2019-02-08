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

    class EventObserver : IObserver<Event>
    {
        public void OnCompleted() {
        }

        public void OnError(Exception error) {
        }

        public void OnNext(Event value) {

        }
    }

    class Program
    {
        public static Node TestNode { get; set; }

        static void Main(string[] args) => AsyncMain(args).Wait();
       

        static async Task AsyncMain(string[] args) {
            // attach node
            TestNode = await Node.CreateFromEnvironmentAsync(new NodeConfiguration() {
                ThrowUnhandledExceptions = true
            });

            // attach
            Service service = null;
            
            try {
                service = await TestNode.AttachAsync("auth:login", ServiceType.Balanced, RpcBehaviour.Bind<ITest001>(new Test001(Guid.NewGuid())));
            } catch(Exception) {
                Console.WriteLine();
            }

            // detaches the service
            await TestNode.DetachAsync(service).ConfigureAwait(false);

            await Task.Delay(50000);
        }
    }
}
