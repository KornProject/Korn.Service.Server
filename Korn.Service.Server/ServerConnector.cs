using Korn.Pipes;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Korn.Service
{
    public class ServerConnector : IDisposable
    {
        public ServerConnector(Server server)
        {
            Server = server;

            ConnectPipe = new OutputPipe(server.Configuration.ConnectConfiguration)
            {
                Received = OnConnectionReceived
            };
        }

        public readonly Server Server;
        public readonly OutputPipe ConnectPipe;
        public readonly List<ClientConnection> Clients = new List<ClientConnection>();

        public Action<ClientConnection> ClientConnected;
        public Action<ClientConnection, byte[]> ReceivedPacketBytes;

        const int ConnectPacketSize = 8;
        void OnConnectionReceived(byte[] bytes)
        {
            if (bytes.Length != ConnectPacketSize)
                throw new Exception("connection packet to server has wrong size");

            ConnectPipe.Disconnect();

            var id = BitConverter.ToUInt64(bytes, 0);
            var connectionId = new ConnectionID(Server.Configuration, id);
            var connection = new ClientConnection(Server, connectionId);

            OnClientConnected(connection);
        }

        void OnClientConnected(ClientConnection client)
        {
            Clients.Add(client);
            client.Received += bytes => ReceivedPacketBytes?.Invoke(client, bytes);
            ClientConnected?.Invoke(client);
        }

        public void Dispose()
        {
            ConnectPipe.Dispose();
            foreach (var client in Clients)
                client.Dispose();
        }
    }
}