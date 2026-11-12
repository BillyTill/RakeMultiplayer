using System;
using System.IO;
using System.Collections.Generic;

namespace RakeMultiplayer.Networking
{
    /// <summary>
    /// Static serializer/deserializer for network packets
    /// </summary>
    public static class PacketSerializer
    {
        private const int MAX_PACKET_SIZE = 65536; // 64KB
        private const int MAX_STRING_LENGTH = 256;

        /// <summary>
        /// Deserializes byte array to NetworkPacket
        /// </summary>
        public static NetworkPacket Deserialize(byte[] data)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("Empty packet data");

            if (data.Length > MAX_PACKET_SIZE)
                throw new ArgumentException($"Packet too large: {data.Length} bytes (max {MAX_PACKET_SIZE})");

            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                var type = (PacketType)reader.ReadByte();
                NetworkPacket packet = type switch
                {
                    PacketType.PlayerState => new PlayerStatePacket(),
                    PacketType.RakeState => new RakeStatePacket(),
                    PacketType.PlayerConnected => new PlayerConnectedPacket(),
                    PacketType.PlayerDisconnected => new PlayerDisconnectedPacket(),
                    PacketType.DamageRequest => new DamageRequestPacket(),
                    PacketType.DamageApplied => new DamageAppliedPacket(),
                    PacketType.FireVisuals => new FireVisualsPacket(),
                    PacketType.StartGame => new StartGamePacket(),
                    PacketType.ItemPickup => new ItemPickupPacket(),
                    PacketType.ItemDestroyed => new ItemDestroyedPacket(),
                    PacketType.ItemPickupConfirm => new ItemPickupConfirmPacket(),
                    PacketType.PhaseChange => new PhaseChangePacket(),
                    PacketType.SessionState => new SessionStatePacket(),
                    PacketType.CameraSync => new CameraSyncPacket(),
                    PacketType.QuestTrigger => new QuestTriggerPacket(),
                    PacketType.PigState => new PigStatePacket(),
                    PacketType.MapMarker => new MapMarkerPacket(),
                    PacketType.TrapState => new TrapStatePacket(),
                    PacketType.CameraSpawn => new CameraSpawnPacket(),
                    PacketType.CameraDestroy => new CameraDestroyPacket(),
                    PacketType.CameraSetupRequest => new CameraSetupRequestPacket(),
                    PacketType.PlayerTeleport => new PlayerTeleportPacket(),
                    PacketType.LevelChange => new LevelChangePacket(),
                    PacketType.ItemState => new ItemStatePacket(),
                    PacketType.PlayerDamaged => new PlayerDamagedPacket(),
                    PacketType.PlayerRagdoll => new PlayerRagdollPacket(),
                    PacketType.PlayerRagdollRequest => new PlayerRagdollRequestPacket(),
                    PacketType.DoorAction => new DoorActionPacket(),
                    PacketType.LobbyRole => new LobbyRolePacket(),
                    PacketType.RakeGrab => new RakeGrabPacket(),
                    _ => throw new InvalidOperationException($"Unknown packet type: {type} (byte value: {(byte)type})")
                };
                packet.Read(reader);
                return packet;
            }
        }

        /// <summary>
        /// Serializes packet to byte array
        /// </summary>
        public static byte[] Serialize(NetworkPacket packet)
        {
            if (packet == null)
                throw new ArgumentNullException(nameof(packet));
            return packet.Serialize();
        }

        // TODO: String serialization currently uses BinaryReader.ReadString() without limits.
        // For production, implement length-prefixed strings:
        // Write: writer.Write((ushort)str.Length); writer.Write(Encoding.UTF8.GetBytes(str));
        // Read: int len = reader.ReadUInt16(); if (len > MAX_STRING_LENGTH) throw...; return Encoding.UTF8.GetString(reader.ReadBytes(len));
    }
}
