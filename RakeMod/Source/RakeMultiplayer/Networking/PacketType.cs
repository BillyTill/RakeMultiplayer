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
        ItemPickupConfirm = 11,
        FireRequest = 12,
        Flashlight = 13,
        CameraSync = 14,
        QuestTrigger = 15,
        PlayerInput = 16,
        PlayerInputAck = 17,
        PlayerInputServer = 18,
        WeaponState = 19,
        WeaponSwitch = 20,
        WeaponSwitchAck = 21,
        ReconnectRequest = 22,
        ReconnectConfirm = 23,
        PhaseChange = 24,
        SessionState = 25,
        PigState = 26,
        MapMarker = 27,
        TrapState = 28,
        CameraSpawn = 29,
        CameraDestroy = 30,
        CameraSetupRequest = 31,
        PlayerTeleport = 32,
        LevelChange = 33,
        ItemState = 34,
        PlayerDamaged = 35,
        PlayerRagdoll = 36,
        PlayerRagdollRequest = 37,
        DoorAction = 38,
        LobbyRole = 39,
        RakeGrab = 40,
        RakeOwner = 41,
        RakeRoleRequest = 42,
        RakeAttackRequest = 43,
        RakeSound = 44,
        RakeDied = 45,
        TestArena = 46,
    }
}
