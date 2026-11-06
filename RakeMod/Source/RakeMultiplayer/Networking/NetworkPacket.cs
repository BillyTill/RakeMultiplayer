using System;
using System.IO;
using UnityEngine;

namespace RakeMultiplayer.Networking
{
    /// <summary>
    /// Base class for all network packets
    /// </summary>
    public abstract class NetworkPacket
    {
        public abstract PacketType Type { get; }
        public abstract void Write(BinaryWriter writer);
        public abstract void Read(BinaryReader reader);

        /// <summary>
        /// Serializes packet to byte array (includes PacketType as first byte)
        /// </summary>
        public byte[] Serialize()
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write((byte)Type);
                Write(writer);
                return ms.ToArray();
            }
        }
    }
}