using System;

namespace RakeMultiplayer.Networking
{
    /// <summary>
    /// Wire compatibility between the host and its clients.
    ///
    /// Without this, a PC with an older mod build connects, receives packets it cannot
    /// parse and behaves unpredictably - the same class of "works on my PC" failure that the
    /// other machine's crash log showed. A mismatch is now an explicit refusal with a
    /// readable message instead of undefined behaviour.
    ///
    /// Bump <see cref="Version"/> whenever a packet layout changes incompatibly.
    /// </summary>
    public static class Protocol
    {
        // 2: PhaseSkipRequest, item replay and the strict client-side level rules. A build
        // below this connects fine but cannot join the phase logic, which showed up as
        // "the client skips the day locally and the host never moves".
        // 3: the client introduces itself with Handshake before the host sends any world
        // state, so a mismatched build is refused before it can load a level (I-NET-6).
        // 4: PlayerState carries the held weapon id; the host validates DamageRequest
        // against it instead of trusting the client's damage type (N3).
        public const int Version = 4;

        public static bool IsCompatible(int clientVersion)
        {
            return clientVersion == Version;
        }

        public static string MismatchMessage(int clientVersion)
        {
            return "Mod version mismatch: client protocol " + Describe(clientVersion) +
                   ", host protocol " + Version + ". Update the mod on every PC.";
        }

        private static string Describe(int version)
        {
            return version <= 0 ? "unknown (build without protocol version)" : version.ToString();
        }
    }
}
