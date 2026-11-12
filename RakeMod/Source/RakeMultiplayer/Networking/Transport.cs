using System;
using Telepathy;

namespace RakeMultiplayer.Networking
{
    public enum NetEventType
    {
        Connected = 0,
        Data = 1,
        Disconnected = 2
    }

    public struct NetMessage
    {
        public int ConnectionId;
        public NetEventType EventType;
        public byte[] Data;

        public NetMessage(NetEventType type, int connectionId, byte[] data)
        {
            EventType = type;
            ConnectionId = connectionId;
            Data = data;
        }
    }

    /// <summary>Transport-agnostic server side (LAN TCP or Steam P2P).</summary>
    public interface INetServer
    {
        bool Active { get; }
        bool Start(int port);
        void Stop();
        void Send(int connectionId, byte[] data);
        bool GetNextMessage(out NetMessage message);
    }

    /// <summary>Transport-agnostic client side (LAN TCP or Steam P2P).</summary>
    public interface INetClient
    {
        bool Connected { get; }
        void Connect(string address, int port);
        void Disconnect();
        void Send(byte[] data);
        bool GetNextMessage(out NetMessage message);
    }

    public static class TransportFactory
    {
        public static INetServer CreateServer()
        {
            if (RakeMultiplayerPlugin.UseSteamP2P.Value) return new SteamServerTransport();
            return new LanServerTransport();
        }

        public static INetClient CreateClient()
        {
            if (RakeMultiplayerPlugin.UseSteamP2P.Value) return new SteamClientTransport();
            return new LanClientTransport();
        }
    }

    /// <summary>Existing Telepathy TCP transport, wrapped in the shared interface.</summary>
    public sealed class LanServerTransport : INetServer
    {
        private readonly Server _server = new Server();

        public LanServerTransport()
        {
            _server.NoDelay = true;
        }

        public bool Active { get { return _server.Active; } }

        public bool Start(int port) { return _server.Start(port); }
        public void Stop() { _server.Stop(); }
        public void Send(int connectionId, byte[] data) { _server.Send(connectionId, data); }

        public bool GetNextMessage(out NetMessage message)
        {
            Message msg;
            if (_server.GetNextMessage(out msg))
            {
                message = new NetMessage(ToNetEvent(msg.eventType), msg.connectionId, msg.data);
                return true;
            }

            message = new NetMessage();
            return false;
        }

        private static NetEventType ToNetEvent(EventType type)
        {
            switch (type)
            {
                case EventType.Connected: return NetEventType.Connected;
                case EventType.Disconnected: return NetEventType.Disconnected;
                default: return NetEventType.Data;
            }
        }
    }

    /// <summary>Existing Telepathy TCP transport, wrapped in the shared interface.</summary>
    public sealed class LanClientTransport : INetClient
    {
        private readonly Client _client = new Client();

        public LanClientTransport()
        {
            _client.NoDelay = true;
        }

        public bool Connected { get { return _client.Connected; } }

        public void Connect(string address, int port) { _client.Connect(address, port); }
        public void Disconnect() { _client.Disconnect(); }
        public void Send(byte[] data) { _client.Send(data); }

        public bool GetNextMessage(out NetMessage message)
        {
            Message msg;
            if (_client.GetNextMessage(out msg))
            {
                message = new NetMessage(ToNetEvent(msg.eventType), msg.connectionId, msg.data);
                return true;
            }

            message = new NetMessage();
            return false;
        }

        private static NetEventType ToNetEvent(EventType type)
        {
            switch (type)
            {
                case EventType.Connected: return NetEventType.Connected;
                case EventType.Disconnected: return NetEventType.Disconnected;
                default: return NetEventType.Data;
            }
        }
    }
}
