﻿using System;
using System.IO;
using System.Threading;
using CherryMPShared;
using Lidgren.Network;
using ProtoBuf;

namespace CherryMP.Misc
{
    public static class Program
    {
        public static string Location { get { return AppDomain.CurrentDomain.BaseDirectory; } }

        public static void Main(string[] args)
        {
            Console.WriteLine("Starting...");

            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

            var _config = new NetPeerConfiguration("GTAVOnlineRaces");
            _config.Port = new Random().Next(1000, 9999);

            var _client = new NetClient(_config);
            _client.Start();

            var msg = _client.CreateMessage();
            msg.Write("Player");
            _client.Connect("127.0.0.1", 4499, msg);
            _client.RegisterReceivedCallback(ProcessMessages, SynchronizationContext.Current);

            while (true)
            {
            }
        }

        public static void ProcessMessages(object sender)
        {
            Console.WriteLine("Received message.");

            var peer = (NetPeer)sender;
            var msg = peer.ReadMessage();

            var type = (PacketType)msg.ReadByte();


            Console.WriteLine("Data is " + type);

            switch (type)
            {
                case PacketType.ChatData:
                    {
                        var len = msg.ReadInt32();
                        var data = DeserializeBinary<ChatData>(msg.ReadBytes(len)) as ChatData;
                        if (data != null) Console.WriteLine("Chat: " + data.Message);
                    }
                    break;
            }
        }

        public static object DeserializeBinary<T>(byte[] data)
        {
            object output;
            using (var stream = new MemoryStream(data))
            {
                try
                {
                    output = Serializer.Deserialize<T>(stream);
                }
                catch (ProtoException e)
                {
                    Console.WriteLine("ERROR: " + e.Message);
                    return null;
                }
            }
            return output;
        }

        public static byte[] SerializeBinary(object data)
        {
            using (var stream = new MemoryStream())
            {
                stream.SetLength(0);
                Serializer.Serialize(stream, data);
                return stream.ToArray();
            }
        }
    }
}