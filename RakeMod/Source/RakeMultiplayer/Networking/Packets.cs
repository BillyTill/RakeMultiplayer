using System;
using System.IO;
using UnityEngine;

namespace RakeMultiplayer.Networking
{
    /// <summary>
    /// Day/Night phase enumeration
    /// </summary>
    public enum Phase : byte
    {
        Day = 0,
        Night = 1
    }

    /// <summary>
    /// Player state synchronization packet
    /// Client -> Server (local player), Server -> All (remote players)
    /// </summary>
    public class PlayerStatePacket : NetworkPacket
    {
        public override PacketType Type => PacketType.PlayerState;

        public int PlayerId;
        public Vector3 Position;
        public Quaternion Rotation;
        public byte AnimState;

        public override void Write(BinaryWriter writer)
        {
            writer.Write(PlayerId);
            writer.Write(Position.x);
            writer.Write(Position.y);
            writer.Write(Position.z);
            writer.Write(Rotation.x);
            writer.Write(Rotation.y);
            writer.Write(Rotation.z);
            writer.Write(Rotation.w);
            writer.Write(AnimState);
        }

        public override void Read(BinaryReader reader)
        {
            PlayerId = reader.ReadInt32();
            Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            Rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            AnimState = reader.ReadByte();
        }
    }

    /// <summary>
    /// Rake AI state synchronization packet
    /// Server -> All (server-authoritative)
    /// </summary>
    public class RakeStatePacket : NetworkPacket
    {
        public override PacketType Type => PacketType.RakeState;

        public Vector3 Position;
        public Quaternion Rotation;
        public byte State;
        public float Health;

        // Animator snapshot so clients reproduce exactly what the host shows
        // (crouch/sneak while hunting animals, animal kill, camera break...).
        public byte AnimFlags;
        public byte WalkType;
        public float AnimSpeed;

        public override void Write(BinaryWriter writer)
        {
            writer.Write(Position.x);
            writer.Write(Position.y);
            writer.Write(Position.z);
            writer.Write(Rotation.x);
            writer.Write(Rotation.y);
            writer.Write(Rotation.z);
            writer.Write(Rotation.w);
            writer.Write(State);
            writer.Write(Health);
            writer.Write(AnimFlags);
            writer.Write(WalkType);
            writer.Write(AnimSpeed);
        }

        public override void Read(BinaryReader reader)
        {
            Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            Rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            State = reader.ReadByte();
            Health = reader.ReadSingle();
            AnimFlags = reader.ReadByte();
            WalkType = reader.ReadByte();
            AnimSpeed = reader.ReadSingle();
        }
    }

    /// <summary>
    /// New player connected notification
    /// Server -> All
    /// </summary>
    /// <summary>
    /// A Rake hit on one specific player.
    /// Server -> the victim client.
    /// </summary>
    public class PlayerDamagedPacket : NetworkPacket
    {
        public override PacketType Type => PacketType.PlayerDamaged;

        public int PlayerId;
        public int Amount;
        public string DamageType;
        public Vector3 SourcePosition;

        public override void Write(BinaryWriter writer)
        {
            writer.Write(PlayerId);
            writer.Write(Amount);
            writer.Write(DamageType ?? string.Empty);
            writer.Write(SourcePosition.x);
            writer.Write(SourcePosition.y);
            writer.Write(SourcePosition.z);
        }

        public override void Read(BinaryReader reader)
        {
            PlayerId = reader.ReadInt32();
            Amount = reader.ReadInt32();
            DamageType = reader.ReadString();
            SourcePosition = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }
    }


    /// <summary>
    /// Show the hit-reaction ragdoll for a player. Works for any damage source.
    /// Server -> all.
    /// </summary>
    public class PlayerRagdollPacket : NetworkPacket
    {
        public override PacketType Type => PacketType.PlayerRagdoll;

        public int PlayerId;
        public Vector3 Direction;

        public override void Write(BinaryWriter writer)
        {
            writer.Write(PlayerId);
            writer.Write(Direction.x);
            writer.Write(Direction.y);
            writer.Write(Direction.z);
        }

        public override void Read(BinaryReader reader)
        {
            PlayerId = reader.ReadInt32();
            Direction = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }
    }


    /// <summary>
    /// Client -> server: the sending player just got hit, show the ragdoll to everyone.
    /// The server re-broadcasts it as a PlayerRagdollPacket.
    /// </summary>
    public class PlayerRagdollRequestPacket : NetworkPacket
    {
        public override PacketType Type => PacketType.PlayerRagdollRequest;

        public int PlayerId;
        public Vector3 Direction;

        public override void Write(BinaryWriter writer)
        {
            writer.Write(PlayerId);
            writer.Write(Direction.x);
            writer.Write(Direction.y);
            writer.Write(Direction.z);
        }

        public override void Read(BinaryReader reader)
        {
            PlayerId = reader.ReadInt32();
            Direction = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }
    }

    /// <summary>
    /// A door was used. Client -> server, then server -> all.
    /// </summary>
    public class DoorActionPacket : NetworkPacket
    {
        public override PacketType Type => PacketType.DoorAction;

        public int DoorId;
        public int SenderId;

        public override void Write(BinaryWriter writer)
        {
            writer.Write(DoorId);
            writer.Write(SenderId);
        }

        public override void Read(BinaryReader reader)
        {
            DoorId = reader.ReadInt32();
            SenderId = reader.ReadInt32();
        }
    }


    /// <summary>
    /// Lobby role state. Server -> all.
    /// </summary>
    public class LobbyRolePacket : NetworkPacket
    {
        public override PacketType Type => PacketType.LobbyRole;

        public bool HostIsRake;

        public override void Write(BinaryWriter writer)
        {
            writer.Write(HostIsRake);
        }

        public override void Read(BinaryReader reader)
        {
            HostIsRake = reader.ReadBoolean();
        }
    }


    /// <summary>
    /// The Rake grabbed a player: freeze them and turn their camera to the Rake.
    /// Server -> all.
    /// </summary>
    public class RakeGrabPacket : NetworkPacket
    {
        public override PacketType Type => PacketType.RakeGrab;

        public int PlayerId;
        public Vector3 RakePosition;
        public Vector3 FocusPosition;

        public override void Write(BinaryWriter writer)
        {
            writer.Write(PlayerId);
            writer.Write(RakePosition.x);
            writer.Write(RakePosition.y);
            writer.Write(RakePosition.z);
            writer.Write(FocusPosition.x);
            writer.Write(FocusPosition.y);
            writer.Write(FocusPosition.z);
        }

        public override void Read(BinaryReader reader)
        {
            PlayerId = reader.ReadInt32();
            RakePosition = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            FocusPosition = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }
    }



    /// <summary>Server -> all: which peer drives the Rake (0 = AI, 1 = host, N = client id).</summary>
    public class RakeOwnerPacket : NetworkPacket
    {
        public override PacketType Type => PacketType.RakeOwner;
        public int OwnerId;

        public override void Write(BinaryWriter writer) { writer.Write(OwnerId); }
        public override void Read(BinaryReader reader) { OwnerId = reader.ReadInt32(); }
    }

    /// <summary>Client -> server: take or release the Rake slot.</summary>
    public class RakeRoleRequestPacket : NetworkPacket
    {
        public override PacketType Type => PacketType.RakeRoleRequest;
        public bool TakeRole;

        public override void Write(BinaryWriter writer) { writer.Write(TakeRole); }
        public override void Read(BinaryReader reader) { TakeRole = reader.ReadBoolean(); }
    }

    /// <summary>A client-owned Rake attacks: the server validates it and applies the effect.</summary>
    public class RakeAttackRequestPacket : NetworkPacket
    {
        public override PacketType Type => PacketType.RakeAttackRequest;

        public byte Kind; // 0 player, 1 animal, 2 camera
        public int TargetPlayerId;
        public int Amount;
        public string DamageType;
        public Vector3 RakePosition;
        public Vector3 FocusPosition;
        public Vector3 TargetPosition;

        public override void Write(BinaryWriter writer)
        {
            writer.Write(Kind);
            writer.Write(TargetPlayerId);
            writer.Write(Amount);
            writer.Write(DamageType ?? string.Empty);
            writer.Write(RakePosition.x); writer.Write(RakePosition.y); writer.Write(RakePosition.z);
            writer.Write(FocusPosition.x); writer.Write(FocusPosition.y); writer.Write(FocusPosition.z);
            writer.Write(TargetPosition.x); writer.Write(TargetPosition.y); writer.Write(TargetPosition.z);
        }

        public override void Read(BinaryReader reader)
        {
            Kind = reader.ReadByte();
            TargetPlayerId = reader.ReadInt32();
            Amount = reader.ReadInt32();
            DamageType = reader.ReadString();
            RakePosition = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            FocusPosition = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            TargetPosition = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }
    }

    /// <summary>All peers: play a Rake attack sound at a position (0 roar, 1 kick).</summary>
    public class RakeSoundPacket : NetworkPacket
    {
        public override PacketType Type => PacketType.RakeSound;
        public byte Sound;
        public Vector3 Position;

        public override void Write(BinaryWriter writer)
        {
            writer.Write(Sound);
            writer.Write(Position.x); writer.Write(Position.y); writer.Write(Position.z);
        }

        public override void Read(BinaryReader reader)
        {
            Sound = reader.ReadByte();
            Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }
    }

    /// <summary>Server -> all: the Rake died here, spawn the corpse on every peer.</summary>
    public class RakeDiedPacket : NetworkPacket
    {
        public override PacketType Type => PacketType.RakeDied;
        public Vector3 Position;
        public Quaternion Rotation;

        public override void Write(BinaryWriter writer)
        {
            writer.Write(Position.x); writer.Write(Position.y); writer.Write(Position.z);
            writer.Write(Rotation.x); writer.Write(Rotation.y); writer.Write(Rotation.z); writer.Write(Rotation.w);
        }

        public override void Read(BinaryReader reader)
        {
            Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            Rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }
    }
    public class PlayerConnectedPacket : NetworkPacket
    {
        public override PacketType Type => PacketType.PlayerConnected;

        public int PlayerId;
        public Vector3 SpawnPosition;
        public bool IsYou; // true only for the packet sent to the player who owns this ID

        public override void Write(BinaryWriter writer)
        {
            writer.Write(PlayerId);
            writer.Write(SpawnPosition.x);
            writer.Write(SpawnPosition.y);
            writer.Write(SpawnPosition.z);
            writer.Write(IsYou);
        }

        public override void Read(BinaryReader reader)
        {
            PlayerId = reader.ReadInt32();
            SpawnPosition = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            IsYou = reader.ReadBoolean();
        }
    }

    /// <summary>
    /// Player disconnected notification
    /// Server -> All
    /// </summary>
    public class PlayerDisconnectedPacket : NetworkPacket
    {
        public override PacketType Type => PacketType.PlayerDisconnected;

        public int PlayerId;

        public override void Write(BinaryWriter writer)
        {
            writer.Write(PlayerId);
        }

        public override void Read(BinaryReader reader)
        {
            PlayerId = reader.ReadInt32();
        }
    }

    /// <summary>
    /// Damage request from client to server
    /// Client -> Server
    /// </summary>
    public class DamageRequestPacket : NetworkPacket
    {
        public override PacketType Type => PacketType.DamageRequest;

        public int ShooterId;
        public int TargetId;
        public int Amount;
        public string DamageType;
        public Vector3 ShooterPosition; // Position of shooter for AI reaction (danger state)
        public Vector3 Direction;        // Aim direction, used to validate client hits

        public override void Write(BinaryWriter writer)
        {
            writer.Write(ShooterId);
            writer.Write(TargetId);
            writer.Write(Amount);
            writer.Write(DamageType ?? string.Empty);
            writer.Write(ShooterPosition.x);
            writer.Write(ShooterPosition.y);
            writer.Write(ShooterPosition.z);
            writer.Write(Direction.x);
            writer.Write(Direction.y);
            writer.Write(Direction.z);
        }

        public override void Read(BinaryReader reader)
        {
            ShooterId = reader.ReadInt32();
            TargetId = reader.ReadInt32();
            Amount = reader.ReadInt32();
            DamageType = reader.ReadString();
            ShooterPosition = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            Direction = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }
    }

    /// <summary>
    /// Damage applied confirmation from server
    /// Server -> All
    /// </summary>
    public class DamageAppliedPacket : NetworkPacket
    {
        public override PacketType Type => PacketType.DamageApplied;

        public int TargetId;
        public float NewHealth;

        public override void Write(BinaryWriter writer)
        {
            writer.Write(TargetId);
            writer.Write(NewHealth);
        }

        public override void Read(BinaryReader reader)
        {
            TargetId = reader.ReadInt32();
            NewHealth = reader.ReadSingle();
        }
    }

    /// <summary>
    /// Weapon fire visual effects
    /// Server -> All
    /// </summary>
    public class FireVisualsPacket : NetworkPacket
    {
        public override PacketType Type => PacketType.FireVisuals;

        public int ShooterId;
        public byte WeaponType;
        public Vector3 Origin;
        public Vector3 Direction;

        public override void Write(BinaryWriter writer)
        {
            writer.Write(ShooterId);
            writer.Write(WeaponType);
            writer.Write(Origin.x);
            writer.Write(Origin.y);
            writer.Write(Origin.z);
            writer.Write(Direction.x);
            writer.Write(Direction.y);
            writer.Write(Direction.z);
        }

        public override void Read(BinaryReader reader)
        {
            ShooterId = reader.ReadInt32();
            WeaponType = reader.ReadByte();
            Origin = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            Direction = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }
    }

    /// <summary>
    /// Item pickup request from client to server
    /// Client -> Server (when player interacts with pickup)
    /// </summary>
    public class ItemPickupPacket : NetworkPacket
    {
        public override PacketType Type => PacketType.ItemPickup;

        public int PlayerId;
        public int ItemNetworkId; // Network ID of the pickup item
        public string ItemType;   // Type identifier (e.g., "Medkit", "Ammo", "Key")

        public override void Write(BinaryWriter writer)
        {
            writer.Write(PlayerId);
            writer.Write(ItemNetworkId);
            writer.Write(ItemType ?? string.Empty);
        }

        public override void Read(BinaryReader reader)
        {
            PlayerId = reader.ReadInt32();
            ItemNetworkId = reader.ReadInt32();
            ItemType = reader.ReadString();
        }
    }

    /// <summary>
    /// Item destroyed confirmation from server
    /// Server -> All (broadcast when item is picked up/destroyed)
    /// </summary>
    public class ItemDestroyedPacket : NetworkPacket
    {
        public override PacketType Type => PacketType.ItemDestroyed;

        public int ItemNetworkId;
        public int PickerId; // Who picked it up (for effects)
        public string ItemType; // Type name, so the picker can apply the effect without the object

        public override void Write(BinaryWriter writer)
        {
            writer.Write(ItemNetworkId);
            writer.Write(PickerId);
            writer.Write(ItemType ?? string.Empty);
        }

        public override void Read(BinaryReader reader)
        {
            ItemNetworkId = reader.ReadInt32();
            PickerId = reader.ReadInt32();
            ItemType = reader.ReadString();
        }
    }

    /// <summary>
    /// Item pickup confirmation from server (accept/reject)
    /// Server -> Requesting client.
    /// Successful pickups are confirmed via the ItemDestroyedPacket broadcast;
    /// this packet is sent when the server rejects a pickup request.
    /// </summary>
    public class ItemPickupConfirmPacket : NetworkPacket
    {
        public override PacketType Type => PacketType.ItemPickupConfirm;

        public int PlayerId;
        public int ItemNetworkId;
        public bool Success;   // true = authorized, false = rejected
        public string Reason;  // e.g. "CameraLimit", "NotFound", "Unauthorized"

        public override void Write(BinaryWriter writer)
        {
            writer.Write(PlayerId);
            writer.Write(ItemNetworkId);
            writer.Write(Success);
            writer.Write(Reason ?? string.Empty);
        }

        public override void Read(BinaryReader reader)
        {
            PlayerId = reader.ReadInt32();
            ItemNetworkId = reader.ReadInt32();
            Success = reader.ReadBoolean();
            Reason = reader.ReadString();
        }
    }

    /// <summary>
    /// Camera setup/pickup request and result
    /// Client -> Server (request), Server -> Requester (result)
    /// </summary>
    public class CameraSetupRequestPacket : NetworkPacket
    {
        public override PacketType Type => PacketType.CameraSetupRequest;

        public uint CameraId;
        public int PlayerId;
        public bool IsSetup;    // true = set up (deploy), false = pick up (un-deploy)
        public bool IsResult;   // false = request, true = server response
        public bool Approved;   // valid when IsResult
        public string Reason;   // rejection reason, e.g. "CameraLimit"

        public override void Write(BinaryWriter writer)
        {
            writer.Write(CameraId);
            writer.Write(PlayerId);
            writer.Write(IsSetup);
            writer.Write(IsResult);
            writer.Write(Approved);
            writer.Write(Reason ?? string.Empty);
        }

        public override void Read(BinaryReader reader)
        {
            CameraId = reader.ReadUInt32();
            PlayerId = reader.ReadInt32();
            IsSetup = reader.ReadBoolean();
            IsResult = reader.ReadBoolean();
            Approved = reader.ReadBoolean();
            Reason = reader.ReadString();
        }
    }

    /// <summary>
    /// Server-issued teleport (safehouse respawn, Task 4.6)
    /// Server -> specific client (or broadcast)
    /// </summary>
    public class PlayerTeleportPacket : NetworkPacket
    {
        public override PacketType Type => PacketType.PlayerTeleport;

        public int PlayerId;
        public Vector3 Position;
        public Quaternion Rotation;

        public override void Write(BinaryWriter writer)
        {
            writer.Write(PlayerId);
            writer.Write(Position.x);
            writer.Write(Position.y);
            writer.Write(Position.z);
            writer.Write(Rotation.x);
            writer.Write(Rotation.y);
            writer.Write(Rotation.z);
            writer.Write(Rotation.w);
        }

        public override void Read(BinaryReader reader)
        {
            PlayerId = reader.ReadInt32();
            Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            Rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }
    }

    /// <summary>
    /// Level change request/notification (Skip day, night transition)
    /// Client -> Server (request), Server -> All (notification)
    /// </summary>
    public class LevelChangePacket : NetworkPacket
    {
        public override PacketType Type => PacketType.LevelChange;

        public int LevelIndex;

        public override void Write(BinaryWriter writer)
        {
            writer.Write(LevelIndex);
        }

        public override void Read(BinaryReader reader)
        {
            LevelIndex = reader.ReadInt32();
        }
    }

    /// <summary>
    /// Stackable item state (e.g. box of cameras): remaining count.
    /// Server -> All
    /// </summary>
    public class ItemStatePacket : NetworkPacket
    {
        public override PacketType Type => PacketType.ItemState;

        public int ItemNetworkId;
        public int RemainingCount;
        public bool IsActive;
        public int PickerId;   // who took one (0 = state only)
        public string ItemType; // type name for client-side effect replay

        public override void Write(BinaryWriter writer)
        {
            writer.Write(ItemNetworkId);
            writer.Write(RemainingCount);
            writer.Write(IsActive);
            writer.Write(PickerId);
            writer.Write(ItemType ?? string.Empty);
        }

        public override void Read(BinaryReader reader)
        {
            ItemNetworkId = reader.ReadInt32();
            RemainingCount = reader.ReadInt32();
            IsActive = reader.ReadBoolean();
            PickerId = reader.ReadInt32();
            ItemType = reader.ReadString();
        }
    }

    /// <summary>
    /// Day/Night phase change notification
    /// Server -> All (broadcast on phase transition)
    /// </summary>
    public class PhaseChangePacket : NetworkPacket
    {
        public override PacketType Type => PacketType.PhaseChange;

        public Phase Phase;
        public float RemainingTime;
        public int NightNumber;

        public override void Write(BinaryWriter writer)
        {
            writer.Write((byte)Phase);
            writer.Write(RemainingTime);
            writer.Write(NightNumber);
        }

        public override void Read(BinaryReader reader)
        {
            Phase = (Phase)reader.ReadByte();
            RemainingTime = reader.ReadSingle();
            NightNumber = reader.ReadInt32();
        }
    }

    /// <summary>
    /// Session state enumeration
    /// </summary>
    public enum SessionState : byte
    {
        Waiting = 0,    // Waiting for players / in lobby
        Active = 1,     // Session in progress (3-night cycle)
        GameOver = 2,   // Session ended
        Victory = 3     // Victory achieved
    }

    /// <summary>
    /// Victory type enumeration
    /// </summary>
    public enum VictoryType : byte
    {
        None = 0,
        HuntersWin = 1,  // All 3 nights survived / Rake defeated
        RakeWin = 2,     // All hunters eliminated
        Timeout = 3      // Session time expired
    }

    /// <summary>
    /// Session state synchronization packet
    /// Server -> All (broadcast on session state changes)
    /// </summary>
    public class SessionStatePacket : NetworkPacket
    {
        public override PacketType Type => PacketType.SessionState;

        public int CurrentNight;
        public SessionState State;
        public float RakeHealth;
        public VictoryType VictoryType;

        public override void Write(BinaryWriter writer)
        {
            writer.Write(CurrentNight);
            writer.Write((byte)State);
            writer.Write(RakeHealth);
            writer.Write((byte)VictoryType);
        }

        public override void Read(BinaryReader reader)
        {
            CurrentNight = reader.ReadInt32();
            State = (SessionState)reader.ReadByte();
            RakeHealth = reader.ReadSingle();
            VictoryType = (VictoryType)reader.ReadByte();
        }
    }

    /// <summary>
    /// Camera synchronization packet
    /// Server -> All (full camera state sync)
    /// </summary>
    public class CameraSyncPacket : NetworkPacket
    {
        public override PacketType Type => PacketType.CameraSync;

        public uint CameraId;
        public Vector3 Position;
        public Quaternion Rotation;
        public bool IsActive;
        public bool IsBroken;
        public float BatteryLevel;
        public int PlacedByPlayerId;

        public override void Write(BinaryWriter writer)
        {
            writer.Write(CameraId);
            writer.Write(Position.x);
            writer.Write(Position.y);
            writer.Write(Position.z);
            writer.Write(Rotation.x);
            writer.Write(Rotation.y);
            writer.Write(Rotation.z);
            writer.Write(Rotation.w);
            writer.Write(IsActive);
            writer.Write(IsBroken);
            writer.Write(BatteryLevel);
            writer.Write(PlacedByPlayerId);
        }

        public override void Read(BinaryReader reader)
        {
            CameraId = reader.ReadUInt32();
            Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            Rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            IsActive = reader.ReadBoolean();
            IsBroken = reader.ReadBoolean();
            BatteryLevel = reader.ReadSingle();
            PlacedByPlayerId = reader.ReadInt32();
        }
    }

    /// <summary>
    /// Camera spawn notification
    /// Server -> All (new camera placed)
    /// </summary>
    public class CameraSpawnPacket : NetworkPacket
    {
        public override PacketType Type => PacketType.CameraSpawn;

        public uint CameraId;
        public Vector3 Position;
        public Quaternion Rotation;
        public int PlacedByPlayerId;

        public override void Write(BinaryWriter writer)
        {
            writer.Write(CameraId);
            writer.Write(Position.x);
            writer.Write(Position.y);
            writer.Write(Position.z);
            writer.Write(Rotation.x);
            writer.Write(Rotation.y);
            writer.Write(Rotation.z);
            writer.Write(Rotation.w);
            writer.Write(PlacedByPlayerId);
        }

        public override void Read(BinaryReader reader)
        {
            CameraId = reader.ReadUInt32();
            Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            Rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            PlacedByPlayerId = reader.ReadInt32();
        }
    }

    /// <summary>
    /// Camera destroy notification
    /// Server -> All (camera removed/destroyed)
    /// </summary>
    public class CameraDestroyPacket : NetworkPacket
    {
        public override PacketType Type => PacketType.CameraDestroy;

        public uint CameraId;

        public override void Write(BinaryWriter writer)
        {
            writer.Write(CameraId);
        }

        public override void Read(BinaryReader reader)
        {
            CameraId = reader.ReadUInt32();
        }
    }

    /// <summary>
    /// Quest trigger packet (camera break, bait taken, trap triggered)
    /// Server -> All (Rake alert, etc.)
    /// </summary>
    public class QuestTriggerPacket : NetworkPacket
    {
        public override PacketType Type => PacketType.QuestTrigger;

        public byte TriggerType; // 0=CameraBreak, 1=BaitTaken, 2=TrapTriggered
        public Vector3 Position;
        public uint CameraId; // Optional: which camera/pig triggered

        public override void Write(BinaryWriter writer)
        {
            writer.Write(TriggerType);
            writer.Write(Position.x);
            writer.Write(Position.y);
            writer.Write(Position.z);
            writer.Write(CameraId);
        }

        public override void Read(BinaryReader reader)
        {
            TriggerType = reader.ReadByte();
            Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            CameraId = reader.ReadUInt32();
        }
    }

    /// <summary>
    /// Pig AI state synchronization packet
    /// Server -> All (pig position, state, camera attachment)
    /// </summary>
    public class PigStatePacket : NetworkPacket
    {
        public override PacketType Type => PacketType.PigState;

        public uint PigId;
        public Vector3 Position;
        public Quaternion Rotation;
        public byte State; // 0=Idle, 1=Patrol, 2=Escape, 3=CameraAttached
        public float Health;
        public uint CameraId; // Attached camera ID (0 if none)

        public override void Write(BinaryWriter writer)
        {
            writer.Write(PigId);
            writer.Write(Position.x);
            writer.Write(Position.y);
            writer.Write(Position.z);
            writer.Write(Rotation.x);
            writer.Write(Rotation.y);
            writer.Write(Rotation.z);
            writer.Write(Rotation.w);
            writer.Write(State);
            writer.Write(Health);
            writer.Write(CameraId);
        }

        public override void Read(BinaryReader reader)
        {
            PigId = reader.ReadUInt32();
            Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            Rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            State = reader.ReadByte();
            Health = reader.ReadSingle();
            CameraId = reader.ReadUInt32();
        }
    }

    /// <summary>
    /// Map marker packet (cameras, traps, bait, safehouse, cave)
    /// Server -> All (map UI sync)
    /// </summary>
    public class MapMarkerPacket : NetworkPacket
    {
        public override PacketType Type => PacketType.MapMarker;

        public byte MarkerType; // 0=Camera, 1=Trap, 2=Bait, 3=Safehouse, 4=Cave
        public Vector3 Position;
        public uint MarkerId;
        public bool IsActive;

        public override void Write(BinaryWriter writer)
        {
            writer.Write(MarkerType);
            writer.Write(Position.x);
            writer.Write(Position.y);
            writer.Write(Position.z);
            writer.Write(MarkerId);
            writer.Write(IsActive);
        }

        public override void Read(BinaryReader reader)
        {
            MarkerType = reader.ReadByte();
            Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            MarkerId = reader.ReadUInt32();
            IsActive = reader.ReadBoolean();
        }
    }

    /// <summary>
    /// Trap state synchronization packet
    /// Server -> All (trap armed/triggered state)
    /// </summary>
    public class TrapStatePacket : NetworkPacket
    {
        public override PacketType Type => PacketType.TrapState;

        public uint TrapId;
        public Vector3 Position;
        public bool IsArmed;
        public float TriggerRadius;
        public bool IsTriggered;

        public override void Write(BinaryWriter writer)
        {
            writer.Write(TrapId);
            writer.Write(Position.x);
            writer.Write(Position.y);
            writer.Write(Position.z);
            writer.Write(IsArmed);
            writer.Write(TriggerRadius);
            writer.Write(IsTriggered);
        }

        public override void Read(BinaryReader reader)
        {
            TrapId = reader.ReadUInt32();
            Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            IsArmed = reader.ReadBoolean();
            TriggerRadius = reader.ReadSingle();
            IsTriggered = reader.ReadBoolean();
        }
    }
}
