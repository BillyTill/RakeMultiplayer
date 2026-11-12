using UnityEngine;

namespace RakeMultiplayer
{
    /// <summary>
    /// Applies <see cref="LobbyState.RakeOwnerId"/> to the local Rake instance.
    ///
    /// The Rake someone plays is always the same scene object on every peer; only the
    /// authority differs:
    ///  - the owning peer runs <see cref="RakePlayerController"/> and, on a client, sends
    ///    its body state to the server (NetworkedRakeAI.SetLocalOwner);
    ///  - the server mirrors a remote owner's snapshots and never runs the AI
    ///    (NetworkedRakeAI.SetRemoteOwner);
    ///  - everyone else is a normal client that interpolates the server's snapshots.
    ///
    /// Safe to call when there is no Rake in the scene (menu/lobby) - it does nothing.
    /// </summary>
    public static class RakeControlSync
    {
        public static void Apply(string reason)
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return;

            var rake = RakeHighlight.FindRake();
            if (rake == null) return;

            var ai = rake.GetComponent<NetworkedRakeAI>();
            bool localOwns = LobbyState.LocalIsRakeOwner;
            bool remoteOwns = LobbyState.RemoteIsRakeOwner;

            if (localOwns)
            {
                if (ai != null)
                {
                    ai.SetRemoteOwner(false);
                    if (!RakeMultiplayerPlugin.isServer) ai.SetLocalOwner(true);
                }

                if (!RakePlayerController.IsActive) RakePlayerController.SetEnabled(true);
            }
            else
            {
                if (RakePlayerController.IsActive) RakePlayerController.SetEnabled(false);

                if (ai != null)
                {
                    ai.SetLocalOwner(false);

                    if (RakeMultiplayerPlugin.isServer)
                    {
                        ai.SetRemoteOwner(remoteOwns);
                        ai.SetPlayerControlled(remoteOwns);
                    }
                    else
                    {
                        ai.SetRemoteOwner(false);
                    }
                }
            }

            RakeMultiplayerPlugin.Log?.LogInfo("[RakeControl] " + reason +
                                               ": owner=P" + LobbyState.RakeOwnerId +
                                               " local=P" + LobbyState.LocalPlayerId +
                                               " server=" + RakeMultiplayerPlugin.isServer +
                                               " controlling=" + RakePlayerController.IsActive);

            if (RakeMultiplayerPlugin.isServer && RakeMultiplayerPlugin.isConnected)
            {
                RakeNetworkManager.Instance?.BroadcastRakeHealth();
            }
        }
    }
}
