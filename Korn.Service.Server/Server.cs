using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

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
        public Action<ClientConnection, ClientPacket, uint> Received;

        void OnClientConnected(ClientConnection client) => Connected?.Invoke(client);

        List<Delegate>[] registeredPackets;
        void InitializeRegisteredPackets() => registeredPackets = new List<Delegate>[Configuration.ClientPackets.Count];

        void OnReceivedPacketBytes(ClientConnection client, byte[] bytes)
        {
            PacketSerializer.DeserializeClientPacket(Configuration, bytes, out var packet, out var id);
            OnPacketReceived(client, packet, id);
        }

        void OnPacketReceived(ClientConnection client, ClientPacket packet, uint packetId)
        {
            Received?.Invoke(client, packet, packetId);

            var handlers = registeredPackets[packetId];
            if (handlers != null)
                foreach (var handler in handlers)
                    handler.DynamicInvoke(packet);
        }

        public void RegisterPacket<T>(Action<T> handler) where T : ClientPacket
        {
            var type = typeof(T);
            var id = Configuration.GetClientPacketID(type);
            if (registeredPackets[id] == null)
                registeredPackets[id] = new List<Delegate>();
            else registeredPackets[id] = new List<Delegate>();

            registeredPackets[id].Add(handler);
        }

        public void UnregisterAllPackets() => InitializeRegisteredPackets();
    }
}