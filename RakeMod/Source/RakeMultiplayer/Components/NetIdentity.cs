using System;
using System.Collections.Generic;
using UnityEngine;

namespace RakeMultiplayer
{
    /// <summary>
    /// Simple network identity for tracking networked objects
    /// Lightweight replacement for Netcode's NetworkObject
    /// </summary>
    public class NetIdentity : MonoBehaviour
    {
        public static uint NextNetId = 1;
        public static readonly System.Collections.Generic.Dictionary<uint, NetIdentity> Registry = new System.Collections.Generic.Dictionary<uint, NetIdentity>();

        [Header("Network Identity")]
        public uint NetId = 0;
        public ushort PrefabId = 0;
        public uint OwnerClientId = 0;
        public bool IsPlayer = false;
        public bool IsServer = false;

        // Ownership checks (simple Telepathy pattern)
        public bool IsOwner => RakeMultiplayerPlugin.isConnected && 
            (OwnerClientId == GetLocalClientId() || (RakeMultiplayerPlugin.isServer && OwnerClientId == 0));
        
        public bool IsLocalPlayer => IsPlayer && IsOwner;

        private bool _registered = false;

        private void Awake()
        {
            if (NetId == 0)
            {
                NetId = NextNetId++;
            }
            
            if (NetId != 0 && !_registered)
            {
                Register();
            }
        }

        private void Start()
        {
            if (!_registered && NetId != 0)
            {
                Register();
            }
        }

        private void OnDestroy()
        {
            Unregister();
        }

        public void Register()
        {
            if (!_registered && NetId != 0)
            {
                Registry[NetId] = this;
                _registered = true;
                RakeMultiplayerPlugin.Log?.LogInfo($"[NetIdentity] Registered: {gameObject.name} NetId={NetId} PrefabId={PrefabId} Owner={OwnerClientId}");
            }
        }

        public void Unregister()
        {
            if (_registered && NetId != 0)
            {
                Registry.Remove(NetId);
                _registered = false;
            }
        }

        public static NetIdentity Find(uint netId)
        {
            Registry.TryGetValue(netId, out var identity);
            return identity;
        }

        public static uint GetLocalClientId()
        {
            if (RakeNetworkManager.Instance == null) return 0;
            
            if (RakeMultiplayerPlugin.isServer)
            {
                return 0;
            }
            else
            {
                var localPlayer = PlayerSpawner.Instance?.LocalPlayer;
                return (uint)(localPlayer?.PlayerId ?? 0);
            }
        }
    }
}