using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Korn.Service
{
    public class Server
    {
        public Server(ServerConfiguration configuration)
        {
            Configuration = configuration;
            InitializeRegisteredPackets();

            Connector = new ServerConnector(this)
            {
                ClientConnected = OnClientConnected,
                ReceivedPacketBytes = OnReceivedPacketBytes
            };
        }

        public readonly ServerConfiguration Configuration;
        public readonly ServerConnector Connector;

        public Action<ClientConnection> Connected;
        public Action<ClientConnection, ClientPacket> Received;
                
        void OnClientConnected(ClientConnection client) => Connected?.Invoke(client);

        void OnReceivedPacketBytes(ClientConnection client, byte[] bytes)
        {
            PacketSerializer.DeserializeClientPacket(Configuration, bytes, out var packet, out var id);
            OnPacketReceived(client, packet, id);
        }

        void OnPacketReceived(ClientConnection connection, ClientPacket packet, uint packetId)
        {
            Received?.Invoke(connection, packet);

            if (packet is ClientCallbackPacket callbackPacket)
            {
                HandleCallback(connection, callbackPacket);
            }
            else
            {
                var handlers = registeredPackets[packetId];
                if (handlers != null)
                    foreach (var handler in handlers)
                        handler.Invoke(connection, packet);
            }
        }

        List<RegisteredPacketDelegate>[] registeredPackets;
        void InitializeRegisteredPackets() => registeredPackets = new List<RegisteredPacketDelegate>[Configuration.ClientPackets.Count];

        public void UnregisterAll() => InitializeRegisteredPackets();

        public Server Register<TClientPaclet>(Action<ClientConnection, TClientPaclet> handler) where TClientPaclet : ClientPacket
            => Register<TClientPaclet>(RegisteredPacketDelegate.Create(handler));

        public Server Register<TClientPaclet>(Action<TClientPaclet> handler) where TClientPaclet : ClientPacket
            => Register<TClientPaclet>(RegisteredPacketDelegate.Create(handler));

        public Server Register<TClientPaclet>(RegisteredPacketDelegate packetDelegate) where TClientPaclet : ClientPacket
        {
            var type = typeof(TClientPaclet);
            var id = Configuration.GetClientPacketID(type);
            if (registeredPackets[id] == null)
                registeredPackets[id] = new List<RegisteredPacketDelegate>();

            registeredPackets[id].Add(packetDelegate);

            return this;
        }

        static TimeSpan callbackCheckDelay = TimeSpan.FromMinutes(1);

        DateTime lastCallbacksCheck;
        public void RegisterCallback<TClientPacket>(
            ClientConnection connection, 
            ServerCallbackablePacket<TClientPacket> callbackablePacket, 
            Action<ClientConnection, TClientPacket> handler
        ) where TClientPacket : ClientCallbackPacket
        {
            connection.RegisterCallback(callbackablePacket, handler);
            EnsureCallbacksChecked();
        }

        public void RegisterCallback<TClientPacket>(
            ClientConnection connection, 
            ServerCallbackablePacket<TClientPacket> callbackablePacket, 
            Action<TClientPacket> handler
        ) where TClientPacket : ClientCallbackPacket
        {
            connection.RegisterCallback(callbackablePacket, handler);
            EnsureCallbacksChecked();
        }

        void EnsureCallbacksChecked()
        {
            var now = DateTime.Now;
            if (lastCallbacksCheck - now < callbackCheckDelay)
            {
                CheckCallbacks();
                lastCallbacksCheck = now;
            }
        }

        void CheckCallbacks()
        {
            var clients = Connector.Clients;

            for (var i = 0; i < clients.Count; i++)
            {
                var client = clients[i];
                client.CheckCallbacks();
            }
        }

        void HandleCallback(ClientConnection connection, ClientCallbackPacket callbackPacket) => connection.HandleCallback(callbackPacket);

        public class RegisteredPacketDelegate
        {
            RegisteredPacketDelegate() { }

            public Delegate ConnectionPacketHandler { get; private set; }
            public Delegate PacketHandler { get; private set; }

            public void Invoke(ClientConnection connection, ClientPacket packet)
            {
                if (ConnectionPacketHandler != null)
                    ConnectionPacketHandler.DynamicInvoke(connection, packet);

                if (PacketHandler != null)
                    PacketHandler.DynamicInvoke(packet);
            }

            public static RegisteredPacketDelegate Create<TClientPacket>(Action<ClientConnection, TClientPacket> handler) where TClientPacket : ClientPacket
                => new RegisteredPacketDelegate() { ConnectionPacketHandler = handler };

            public static RegisteredPacketDelegate Create<TClientPacket>(Action<TClientPacket> handler) where TClientPacket : ClientPacket
                => new RegisteredPacketDelegate() { PacketHandler = handler };
        }
    }    
}