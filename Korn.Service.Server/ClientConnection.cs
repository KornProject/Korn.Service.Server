using System.Collections.Generic;
using Korn.Pipes;
using System;

namespace Korn.Service
{
    public class ClientConnection : Connection
    {
        static Random random = new Random();

        public ClientConnection(Server server, ConnectionID identifier) : base(identifier)
        {
            Server = server;
            ConnectionID = identifier;

            InputPipe = new InputPipe(identifier.InClientConfiguration)
            {
                Connected = OnPipeConnected,
                Disconnected = OnPipeDisconnected
            };

            OutputPipe = new OutputPipe(identifier.InServerConfiguration)
            {
                Received = bytes =>
                {
                    if (Received != null)
                        Received.Invoke(bytes);
                },
                Connected = OnPipeConnected,
                Disconnected = OnPipeDisconnected
            };
        }

        public readonly Server Server;
        public readonly ConnectionID ConnectionID;

        public Action<byte[]> Received;

        public void Send(ServerPacket packet)
        {
            var bytes = PacketSerializer.SerializeServerPacket(Server.Configuration, packet);
            Send(bytes);
        }

        public void Send<TClientPacket>(ServerCallbackablePacket<TClientPacket> packet, Action<TClientPacket> handler) where TClientPacket : ClientCallbackPacket
        {
            packet.CallbackID = random.Next();
            Server.RegisterCallback(this, packet, handler);
            Send(packet);
        }

        public void Send<TClientPacket>(ServerCallbackablePacket<TClientPacket> packet, Action<ClientConnection, TClientPacket> handler) where TClientPacket : ClientCallbackPacket
        {
            packet.CallbackID = random.Next();
            Server.RegisterCallback(this, packet, handler);
            Send(packet);
        }

        public void Callback(ClientCallbackablePacket clientPacket, ServerCallbackPacket serverPacket)
        {
            serverPacket.ApplyCallback(clientPacket);
            Send(serverPacket);
        }

        List<CallbackDelegate> callbacks = new List<CallbackDelegate>();
        public void RegisterCallback<TClientPacket>(ServerCallbackablePacket<TClientPacket> callbackablePacket, Action<ClientConnection, TClientPacket> handler) 
            where TClientPacket : ClientCallbackPacket => RegisterPacketCallback(CallbackDelegate.Create(callbackablePacket.CallbackID, handler));

        public void RegisterCallback<TClientPacket>(ServerCallbackablePacket<TClientPacket> callbackablePacket, Action<TClientPacket> handler)
            where TClientPacket : ClientCallbackPacket => RegisterPacketCallback(CallbackDelegate.Create(callbackablePacket.CallbackID, handler));

        void RegisterPacketCallback(CallbackDelegate callback)
        {
            lock (callbacks)
                callbacks.Add(callback);
        }

        static TimeSpan callbackLifetime = TimeSpan.FromMinutes(5);
        public void CheckCallbacks()
        {
            var now = DateTime.Now;
            var deadline = now - callbackLifetime;
            for (var i = 0; i < callbacks.Count; i++)
                if (callbacks[i].CreatedAt < deadline)
                    lock (callbacks)
                        callbacks.RemoveRange(i, callbacks.Count - i);
        }

        public void HandleCallback(ClientCallbackPacket callbackPacket)
        {
            var callbackId = callbackPacket.CallbackID;
            for (var i = 0; i < callbacks.Count; i++)
            {
                var callback = callbacks[i];
                if (callback.CallbackID == callbackId)
                {
                    lock (callbacks)
                        callbacks.RemoveAt(i);

                    callback.Invoke(this, callbackPacket);

                    break;
                }
            }
        }
    }

    public class CallbackDelegate
    {
        CallbackDelegate() { }

        public int CallbackID { get; private set; }
        public Delegate ConnectionPacketHandler { get; private set; }
        public Delegate PacketHandler { get; private set; }
        public DateTime CreatedAt { get; private set; } = DateTime.Now;

        public void Invoke(ClientConnection connection, ClientCallbackPacket packet)
        {
            if (ConnectionPacketHandler != null)
                ConnectionPacketHandler.DynamicInvoke(connection, packet);

            if (PacketHandler != null)
                PacketHandler.DynamicInvoke(packet);
        }

        public static CallbackDelegate Create<TClientPacket>(int callbackId, Action<ClientConnection, TClientPacket> handler) where TClientPacket : ClientCallbackPacket
        {
            var callback = new CallbackDelegate()
            {
                CallbackID = callbackId,
                ConnectionPacketHandler = handler
            };

            return callback;
        }

        public static CallbackDelegate Create<TClientPacket>(int callbackId, Action<TClientPacket> handler) where TClientPacket : ClientCallbackPacket
        {
            var callback = new CallbackDelegate()
            {
                CallbackID = callbackId,
                PacketHandler = handler
            };

            return callback;
        }
    }
}