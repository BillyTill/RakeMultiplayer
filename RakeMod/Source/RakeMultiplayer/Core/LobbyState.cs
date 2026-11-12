namespace RakeMultiplayer
{
    /// <summary>
    /// Lobby role selection, shared by the menu, the network layer and the scene loader.
    /// Exactly one Rake per session and it may belong to any player: 0 = AI (nobody),
    /// 1 = host, N = client with that player id.
    /// </summary>
    internal static class LobbyState
    {
        public static int RakeOwnerId;

        /// <summary>Compatibility helper: the host owns the Rake slot.</summary>
        public static bool HostIsRake
        {
            get { return RakeOwnerId == 1; }
            set { RakeOwnerId = value ? 1 : 0; }
        }

        public static int LocalPlayerId
        {
            get
            {
                var spawner = PlayerSpawner.Instance;
                if (spawner != null && spawner.LocalPlayer != null && spawner.LocalPlayer.PlayerId > 0)
                {
                    return spawner.LocalPlayer.PlayerId;
                }
                return RakeMultiplayerPlugin.isServer ? 1 : 0;
            }
        }

        /// <summary>This machine should drive the Rake.</summary>
        public static bool LocalIsRakeOwner
        {
            get
            {
                int local = LocalPlayerId;
                return RakeOwnerId > 0 && local > 0 && RakeOwnerId == local;
            }
        }

        /// <summary>Some other player drives the Rake (the AI is off).</summary>
        public static bool RemoteIsRakeOwner
        {
            get
            {
                int local = LocalPlayerId;
                return RakeOwnerId > 0 && (local <= 0 || RakeOwnerId != local);
            }
        }
    }
}
