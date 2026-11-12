using System;
using System.Collections.Generic;
using UnityEngine;

namespace RakeMultiplayer.Networking
{
    /// <summary>
    /// Steam P2P server: the host maps SteamIDs to the same integer connection ids the
    /// rest of the mod uses (player id = connection id + 1) and exchanges the existing
    /// packet bytes over reliable Steam P2P packets.
    /// </summary>
    public sealed class SteamServerTransport : INetServer
    {
        public static SteamServerTransport Current { get; private set; }

        private readonly Dictionary<int, ulong> _steamByConn = new Dictionary<int, ulong>();
        private readonly Dictionary<ulong, int> _connBySteam = new Dictionary<ulong, int>();
        private readonly Queue<NetMessage> _queue = new Queue<NetMessage>();
        private readonly byte[] _buffer = new byte[65536];

        private bool _active;
        private int _nextConn;

        public bool Active { get { return _active; } }

        public bool Start(int port)
        {
            _active = true;
            _nextConn = 0;
            Current = this;
            _steamByConn.Clear();
            _connBySteam.Clear();
            _queue.Clear();

            RakeMultiplayerPlugin.Log?.LogInfo("[Steam] P2P server ready (lobby " +
                                               SteamLobbyManager.CurrentLobby + ")");
            return true;
        }

        public void Stop()
        {
            foreach (var kv in _steamByConn)
            {
                try { SteamNative.CloseSession(kv.Value); } catch { }
            }

            _steamByConn.Clear();
            _connBySteam.Clear();
            _queue.Clear();
            _active = false;

            if (Current == this) Current = null;
        }

        public void Send(int connectionId, byte[] data)
        {
            ulong steamId;
            if (!_steamByConn.TryGetValue(connectionId, out steamId)) return;

            if (!SteamNative.SendP2P(steamId, data))
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[Steam] SendP2P failed for C" +
                                                      connectionId + " (" + data.Length + "b)");
            }
        }

        public bool GetNextMessage(out NetMessage message)
        {
            Pump();

            if (_queue.Count > 0)
            {
                message = _queue.Dequeue();
                return true;
            }

            message = new NetMessage();
            return false;
        }

        /// <summary>Called from the P2PSessionRequest callback.</summary>
        public void OnSessionRequest(ulong steamId)
        {
            if (!_active) return;

            if (!SteamLobbyManager.IsLobbyMember(steamId))
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[Steam] rejected P2P session from non-member " + steamId);
                return;
            }

            SteamNative.AcceptSession(steamId);

            if (_connBySteam.ContainsKey(steamId)) return;

            int connId = _nextConn++;
            _connBySteam[steamId] = connId;
            _steamByConn[connId] = steamId;

            RakeMultiplayerPlugin.Log?.LogInfo("[Steam] P2P peer connected: " + steamId + " -> C" + connId);
            _queue.Enqueue(new NetMessage(NetEventType.Connected, connId, null));
        }

        public void OnPeerLeft(ulong steamId)
        {
            int connId;
            if (!_connBySteam.TryGetValue(steamId, out connId)) return;

            _connBySteam.Remove(steamId);
            _steamByConn.Remove(connId);

            try { SteamNative.CloseSession(steamId); } catch { }
            RakeMultiplayerPlugin.Log?.LogInfo("[Steam] P2P peer left: " + steamId + " (C" + connId + ")");
            _queue.Enqueue(new NetMessage(NetEventType.Disconnected, connId, null));
        }

        private void Pump()
        {
            if (!_active) return;

            for (int channel = 0; channel < 4; channel++)
            {
                uint available;
                while (SteamNative.IsPacketAvailable(out available, channel))
                {
                    uint size;
                    ulong remote;
                    if (!SteamNative.ReadPacket(_buffer, out size, out remote, channel)) break;

                    int connId;
                    if (!_connBySteam.TryGetValue(remote, out connId))
                    {
                        OnSessionRequest(remote);
                        if (!_connBySteam.TryGetValue(remote, out connId)) continue;
                    }

                    byte[] data = new byte[size];
                    Array.Copy(_buffer, data, (int)size);
                    _queue.Enqueue(new NetMessage(NetEventType.Data, connId, data));
                }
            }
        }
    }

    /// <summary>
    /// Steam P2P client: talks to the lobby owner's SteamID. The "address" passed by the
    /// network manager is that SteamID as a decimal string.
    /// </summary>
    public sealed class SteamClientTransport : INetClient
    {
        public static SteamClientTransport Current { get; private set; }

        private readonly Queue<NetMessage> _queue = new Queue<NetMessage>();
        private readonly byte[] _buffer = new byte[65536];

        private ulong _owner;
        private bool _connected;
        private bool _connectedQueued;

        public bool Connected { get { return _connected; } }

        public void Connect(string address, int port)
        {
            ulong owner;
            if (!ulong.TryParse(address, out owner) || owner == 0)
            {
                RakeMultiplayerPlugin.Log?.LogError("[Steam] invalid lobby owner id: " + address);
                return;
            }

            _owner = owner;
            _connected = true;
            _connectedQueued = false;
            _queue.Clear();
            Current = this;

            RakeMultiplayerPlugin.Log?.LogInfo("[Steam] P2P client targeting " + owner);
            if (!_connectedQueued)
            {
                _connectedQueued = true;
                _queue.Enqueue(new NetMessage(NetEventType.Connected, 0, null));
            }
        }

        public void Disconnect()
        {
            if (_owner != 0)
            {
                try { SteamNative.CloseSession(_owner); } catch { }
            }

            _owner = 0;
            _connected = false;
            _queue.Clear();
            if (Current == this) Current = null;
        }

        public void Send(byte[] data)
        {
            if (!_connected || _owner == 0) return;
            if (!SteamNative.SendP2P(_owner, data))
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[Steam] SendP2P to host failed (" + data.Length + "b)");
            }
        }

        public bool GetNextMessage(out NetMessage message)
        {
            Pump();

            if (_queue.Count > 0)
            {
                message = _queue.Dequeue();
                return true;
            }

            message = new NetMessage();
            return false;
        }

        public void OnConnectFail(ulong remote)
        {
            if (remote != _owner || !_connected) return;

            _connected = false;
            _queue.Enqueue(new NetMessage(NetEventType.Disconnected, 0, null));
            RakeMultiplayerPlugin.Log?.LogWarning("[Steam] P2P session to host failed");
        }

        private void Pump()
        {
            for (int channel = 0; channel < 4; channel++)
            {
                uint available;
                while (SteamNative.IsPacketAvailable(out available, channel))
                {
                    uint size;
                    ulong remote;
                    if (!SteamNative.ReadPacket(_buffer, out size, out remote, channel)) break;
                    if (_owner != 0 && remote != _owner) continue;

                    byte[] data = new byte[size];
                    Array.Copy(_buffer, data, (int)size);
                    _queue.Enqueue(new NetMessage(NetEventType.Data, 0, data));
                }
            }
        }
    }
}
