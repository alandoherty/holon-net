﻿using System;
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

    class EventTest
    {
        public int Wow { get; set; }
    }

    class Program
    {
        static void Main(string[] args) => AsyncMain(args).Wait();

        private static bool go = true;

        public static async void ReadLoop(Node node) {
            // subscribe
            EventSubscription subscription = await node.SubscribeAsync("user:alan.*");

            while (true) {
                Event e = await subscription.ReceiveAsync();
            }
        }

        static async Task AsyncMain(string[] args) {
            // attach node
            Node node = await Node.CreateFromEnvironmentAsync();

            ReadLoop(node);

            await Task.Delay(5000);

            await node.EmitAsync("user:alan.name_changed", new EventTest() {
                Wow = 39
            });

            await Task.Delay(50000);
        }
    }
}
