using System;

namespace RakeMultiplayer.Networking
{
    /// <summary>
    /// Network packet types - first byte of every packet
    /// </summary>
    public enum PacketType : byte
    {
        PlayerState = 1,
        RakeState = 2,
        PlayerConnected = 3,
        PlayerDisconnected = 4,
        DamageRequest = 5,
        DamageApplied = 6,
        FireVisuals = 7,
        StartGame = 8,
        ItemPickup = 9,
        ItemDestroyed = 10,
    }
}