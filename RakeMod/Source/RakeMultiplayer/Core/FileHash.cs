using System;
using System.IO;
using System.Security.Cryptography;

namespace RakeMultiplayer
{
    /// <summary>
    /// SHA-256 in lowercase hex. Pure managed, so it can be unit tested outside the game.
    /// </summary>
    public static class FileHash
    {
        public static string OfBytes(byte[] data)
        {
            if (data == null) return string.Empty;

            using (var sha = new SHA256Managed())
            {
                return ToHex(sha.ComputeHash(data));
            }
        }

        public static string OfFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return string.Empty;
                using (var stream = File.OpenRead(path))
                using (var sha = new SHA256Managed())
                {
                    return ToHex(sha.ComputeHash(stream));
                }
            }
            catch
            {
                // Locked, unreadable or gone: the caller treats an empty hash as a mismatch.
                return string.Empty;
            }
        }

        private static string ToHex(byte[] hash)
        {
            var chars = new char[hash.Length * 2];
            const string digits = "0123456789abcdef";

            for (int i = 0; i < hash.Length; i++)
            {
                chars[i * 2] = digits[hash[i] >> 4];
                chars[i * 2 + 1] = digits[hash[i] & 0x0F];
            }

            return new string(chars);
        }
    }
}
