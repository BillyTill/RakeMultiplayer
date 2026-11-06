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
        /// <summary>
        /// Deserializes byte array to NetworkPacket
        /// </summary>
        public static NetworkPacket Deserialize(byte[] data)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("Empty packet data");

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
    }
}