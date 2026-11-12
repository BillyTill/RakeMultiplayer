namespace RakeMultiplayer
{
    /// <summary>
    /// Lobby role selection, shared by the menu, the network layer and the scene loader.
    /// Only one Rake per session: the host can take it.
    /// </summary>
    internal static class LobbyState
    {
        public static bool HostIsRake;
    }
}