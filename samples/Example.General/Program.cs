using System;
using System.Collections.Generic;
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

    [ProtoContract]
    class NameChangedEventData
    {
        [ProtoMember(1)]
        public string OldName { get; set; }

        [ProtoMember(2)]
        public string NewName { get; set; }
    }

    class Test001 : ITest001
    {
        static int si = 0;
        int i = Interlocked.Increment(ref si);

        public async Task<string> Login(LoginRequestMsg login) {
            Console.WriteLine($"Worker Waiting {i} - Username: {login.Username} Password: {login.Password}");

            await Program.TestNode.EmitAsync("user:bacon.name_change", new NameChangedEventData() {
                OldName = "Bacon",
                NewName = "Ham"
            });
            return "landlocked";
        }
    }

    class Program
    {
        public static Node TestNode { get; set; }

        static void Main(string[] args) => AsyncMain(args).Wait();

        public static async void ReadLoop(Node node) {
            ITest001 proxy = node.SecureProxy<ITest001>("auth:test", new SecureChannelConfiguration() {
                ValidateAuthority = false,
                ValidateAddress = false
            });

            while (true) {
                try {
                    string s = await proxy.Login(new LoginRequestMsg() {
                        Password = "wow",
                        Username = "alan"
                    });

                    Console.WriteLine($"String: {s}");
                } catch(Exception ex) {
                    Console.WriteLine(ex.ToString());
                }

                await Task.Delay(3000);
            }
        }

        class Observer : IObserver<Event>
        {
            public void OnCompleted() {
            }

            public void OnError(Exception error) {
            }

            public void OnNext(Event value) {
                NameChangedEventData changeData = value.Deserialize<NameChangedEventData>();

                Console.WriteLine("Name changed: " + changeData.OldName + " -> " + changeData.NewName);
            }
        }

        static async Task AsyncMain(string[] args) {
            // attach node
            TestNode = await Node.CreateFromEnvironmentAsync(new NodeConfiguration() {
                ThrowUnhandledExceptions = true
            });
            
            await TestNode.AttachAsync("auth:test", new ServiceConfiguration() {
                Filters = new IServiceFilter[] { new SecureFilter(new X509Certificate2("public_privatekey.pfx"), "bacon") }
            }, RpcBehaviour.Bind<ITest001>(new Test001()));

            EventSubscription subscription = await TestNode.SubscribeAsync("user:bacon.*");

            subscription.AsObservable().Subscribe(new Observer());

            ReadLoop(TestNode);

            await Task.Delay(50000);
        }
    }
}
