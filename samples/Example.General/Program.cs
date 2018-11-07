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
            //Console.WriteLine($"Worker ({_uuid}) - Username: {login.Username} Password: {login.Password}");
            
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

        public static async void ReadLoop(int[] ctr, Node node, Guid[] uuids) {
            Random rand = new Random();

            while (true) {
                try {
                    int i = rand.Next(0, uuids.Length);
                    Guid uuid = uuids[i];
                    ITest001 proxy = node.Proxy<ITest001>($"auth:{uuid}");

                    string s = await proxy.Login(new LoginRequestMsg() {
                        Password = "wow",
                        Username = "alan"
                    }).ConfigureAwait(false);

                    Interlocked.Increment(ref ctr[0]);

                    //Console.WriteLine($"String: {s}");
                } catch(Exception ex) {
                    Console.WriteLine(ex.ToString());
                }
            }
        }

        class EventTest
        {
            public string Potato { get; set; }
        }

        class EventObserver : IObserver<Event>
        { 
            public void OnCompleted() {
            }

            public void OnError(Exception error) {
            }

            public void OnNext(Event value) {
                Console.WriteLine(value.Data.Length);
            }
        }

        static async Task AsyncMain(string[] args) {
            // attach node
            TestNode = await Node.CreateFromEnvironmentAsync(new NodeConfiguration() {
                ThrowUnhandledExceptions = true
            });

            // attach services
            Guid[] uuids = new Guid[500];
            List<Task> tasks = new List<Task>();

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            for (int i = 0; i < uuids.Length; i++) {
                uuids[i] = Guid.NewGuid();

                tasks.Add(TestNode.AttachAsync($"auth:{uuids[i]}", RpcBehaviour.Bind<ITest001>(new Test001(uuids[i]))));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);

            Console.WriteLine($"Attached {uuids.Length} services in {stopwatch.ElapsedMilliseconds}ms");
            
            int[] ctr = new int[] { 0 };
            int pavg = 0;

            for (int i = 0; i < 32; i++)
                ReadLoop(ctr, TestNode, uuids);

            while(true) {
                Console.WriteLine($"Logging in at {ctr[0]}/s avg ({pavg}/s), ({Process.GetCurrentProcess().Threads.Count} threads)");

                pavg += ctr[0];
                pavg = pavg / 2;
                ctr[0] = 0;

                await Task.Delay(1000).ConfigureAwait(false);
            }

            await Task.Delay(50000);
        }
    }
}
