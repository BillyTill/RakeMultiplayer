using System;
using System.IO;
using UnityEngine;

namespace RakeMultiplayer.Networking
{
    /// <summary>
    /// Packet to signal clients to start the game (load scene)
    /// </summary>
    public class StartGamePacket : NetworkPacket
    {
        public override PacketType Type => PacketType.StartGame;

        public string SceneName;

        public StartGamePacket() { }

        public StartGamePacket(string sceneName)
        {
            SceneName = sceneName;
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(SceneName ?? "");
        }

        public override void Read(BinaryReader reader)
        {
            // ReadString trusts the 7-bit length prefix: a peer could ask the 32-bit host
            // to allocate ~2 GB before a single byte is read. Every other packet uses the
            // capped reader.
            SceneName = PacketSerializer.ReadSafeString(reader);
        }
    }
}