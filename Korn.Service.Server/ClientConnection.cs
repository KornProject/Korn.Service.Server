using Korn.Pipes;
using System;

namespace Korn.Service
{
    public class ClientConnection : Connection
    {
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
    }
}