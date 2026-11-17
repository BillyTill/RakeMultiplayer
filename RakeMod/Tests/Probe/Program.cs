using System;
using System.IO;
using System.Text;
using System.Threading;
using Telepathy;

namespace RakeMultiplayer.ProtocolProbe
{
    /// <summary>
    /// Connects to a running host exactly like a client would and checks that a build with a
    /// different protocol version is refused with a readable reason instead of half-joining
    /// the session.
    ///
    /// Usage: RakeMultiplayer.ProtocolProbe.exe [port] [version]
    ///   version = -1  sends a handshake from a build that predates versioning
    ///
    /// Exit codes: 0 = rejected as expected, 1 = accepted (bad), 2 = could not connect.
    /// </summary>
    internal static class Program
    {
        private const byte ClientReadyType = 49;
        private const byte ProtocolRejectType = 51;

        private static int Main(string[] args)
        {
            int port = 7777;
            int version = 0;

            if (args.Length > 0) int.TryParse(args[0], out port);
            if (args.Length > 1) int.TryParse(args[1], out version);

            var client = new Client { NoDelay = true };
            client.Connect("127.0.0.1", port);

            if (!WaitFor(delegate { return client.Connected; }, 5000))
            {
                Console.WriteLine("FAIL: no host on 127.0.0.1:" + port);
                return 2;
            }

            client.Send(BuildClientReady("ProtocolProbe", version));

            int hostVersion = -1;
            string message = null;
            bool disconnected = false;

            WaitFor(delegate
            {
                Message msg;
                while (client.GetNextMessage(out msg))
                {
                    if (msg.eventType == EventType.Disconnected) disconnected = true;
                    if (msg.eventType != EventType.Data || msg.data == null || msg.data.Length < 2) continue;
                    if (msg.data[0] != ProtocolRejectType) continue;

                    using (var reader = new BinaryReader(new MemoryStream(msg.data)))
                    {
                        reader.ReadByte();
                        hostVersion = reader.ReadInt32();
                        reader.ReadInt32();          // client version, echoed back
                        message = ReadString(reader);
                    }
                    return true;
                }

                return false;
            }, 5000);

            if (hostVersion < 0 && !disconnected && !client.Connected) disconnected = true;


            if (hostVersion < 0 && disconnected)
            {
                // The host closed the session but the reject packet did not reach us (it can
                // race with the disconnect). Still a refusal, just a less informative one.
                Console.WriteLine("PASS: the host closed the connection for an incompatible build");
                return 0;
            }

            if (hostVersion < 0)
            {
                Console.WriteLine("FAIL: the host neither rejected nor closed the handshake");
                return 1;
            }

            Console.WriteLine("PASS: host protocol " + hostVersion + " refused this build: " + message);
            return 0;
        }

        private static byte[] BuildClientReady(string name, int version)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(ClientReadyType);
                WriteString(writer, name);

                // -1 simulates a build that wrote only the name.
                if (version >= 0) writer.Write(version);

                return stream.ToArray();
            }
        }

        /// <summary>Same 7-bit length prefix as PacketSerializer.ReadSafeString.</summary>
        private static void WriteString(BinaryWriter writer, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            int length = bytes.Length;

            do
            {
                byte b = (byte)(length & 0x7F);
                length >>= 7;
                if (length != 0) b |= 0x80;
                writer.Write(b);
            }
            while (length != 0);

            writer.Write(bytes);
        }

        private static string ReadString(BinaryReader reader)
        {
            int length = 0;
            int shift = 0;
            byte b;

            do
            {
                if (reader.BaseStream.Position >= reader.BaseStream.Length) return string.Empty;
                b = reader.ReadByte();
                length |= (b & 0x7F) << shift;
                shift += 7;
            }
            while ((b & 0x80) != 0 && shift <= 28);

            if (length <= 0 || length > reader.BaseStream.Length - reader.BaseStream.Position)
                return string.Empty;

            return Encoding.UTF8.GetString(reader.ReadBytes(length));
        }

        private static bool WaitFor(Func<bool> condition, int timeoutMs)
        {
            int deadline = Environment.TickCount + timeoutMs;
            while (Environment.TickCount < deadline)
            {
                if (condition()) return true;
                Thread.Sleep(20);
            }
            return condition();
        }
    }
}
