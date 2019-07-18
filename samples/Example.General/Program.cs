using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.Lambda;
using Holon;
using Holon.Events;
using Holon.Metrics;
using Holon.Remoting;
using Holon.Remoting.Serializers;
using Holon.Security;
using Holon.Services;
using Holon.Transports;
using Holon.Transports.Amqp;
using Holon.Transports.Lambda;
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
        static async Task Main(string[] args) {
            // build node
            NodeBuilder nodeBuilder = new NodeBuilder()
                .AddVirtual("virtual")
                .AddAmqp(new Uri("amqp://localhost"), "amqp")
                .AddLambda(new AmazonLambdaClient(RegionEndpoint.EUWest1), "lambda")
                .RouteAll("lambda");

            Node node = nodeBuilder.Build();

            await node.SendAsync(new Message() {
                Address = new ServiceAddress("lambda:holontest"),
                Body = new byte[0]
            });

            // attach
            Service service = await node.AttachAsync("auth:login", ServiceType.Balanced, RpcBehaviour.Bind<ITest001>(new Test001(Guid.NewGuid())));

            // detaches the service
            await node.DetachAsync(service).ConfigureAwait(false);

            await Task.Delay(50000);
        }
    }
}
