using System;
using System.IO;
using UnityEngine;

namespace RakeMultiplayer.Networking
{
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
        }

        public override void Read(BinaryReader reader)
        {
            Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            Rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            State = reader.ReadByte();
            Health = reader.ReadSingle();
        }
    }

    /// <summary>
    /// New player connected notification
    /// Server -> All
    /// </summary>
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

        public override void Write(BinaryWriter writer)
        {
            writer.Write(ShooterId);
            writer.Write(TargetId);
            writer.Write(Amount);
            writer.Write(DamageType ?? string.Empty);
        }

        public override void Read(BinaryReader reader)
        {
            ShooterId = reader.ReadInt32();
            TargetId = reader.ReadInt32();
            Amount = reader.ReadInt32();
            DamageType = reader.ReadString();
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

        public override void Write(BinaryWriter writer)
        {
            writer.Write(ItemNetworkId);
            writer.Write(PickerId);
        }

        public override void Read(BinaryReader reader)
        {
            ItemNetworkId = reader.ReadInt32();
            PickerId = reader.ReadInt32();
        }
    }
}