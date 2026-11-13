using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace RakeMultiplayer
{
    /// <summary>
    /// Bare-bones Steamworks bindings (app 480 / Spacewar) for the 32-bit game.
    ///
    /// The managed Steamworks.NET package targets .NET 4.0, which this Unity 5 / net35
    /// assembly cannot reference, so we bind the flat exports of the shipped
    /// `CSteamworks.dll` shim directly. Only the lobby + P2P + user subset the mod needs
    /// is declared here.
    /// </summary>
    internal static class SteamNative
    {
        private const string Dll = "CSteamworks";

        public const int CallbackLobbyEnter = 504;
        public const int CallbackLobbyChatUpdate = 506;
        public const int CallbackLobbyMatchList = 510;
        public const int CallbackLobbyCreated = 513;
        public const int CallbackP2PSessionRequest = 1202;
        public const int CallbackP2PSessionConnectFail = 1203;

        public const int LobbyTypePublic = 2;
        public const int SendReliable = 2;

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct LobbyCreated
        {
            public int Result;
            public ulong LobbyId;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct LobbyEnter
        {
            public ulong LobbyId;
            public uint Permissions;
            [MarshalAs(UnmanagedType.I1)] public bool Locked;
            public uint Response;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct LobbyMatchList
        {
            public uint Count;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct LobbyChatUpdate
        {
            public ulong LobbyId;
            public ulong UserChanged;
            public ulong MakingChange;
            public uint StateChange;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct P2PSessionRequest
        {
            public ulong Remote;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct P2PSessionConnectFail
        {
            public ulong Remote;
            public byte Error;
        }

        // ------------------------------------------------------------------ api

        [DllImport(Dll, EntryPoint = "Init", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool SteamAPI_Init();

        [DllImport(Dll, EntryPoint = "Shutdown", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SteamAPI_Shutdown();

        [DllImport(Dll, EntryPoint = "RunCallbacks", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SteamAPI_RunCallbacks();

        [DllImport(Dll, EntryPoint = "IsSteamRunning", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool SteamAPI_IsSteamRunning();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool ISteamUtils_IsAPICallCompleted(ulong apiCall,
            [MarshalAs(UnmanagedType.I1)] out bool failed);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool ISteamUtils_GetAPICallResult(ulong apiCall, IntPtr result, int size,
            int expectedCallback, [MarshalAs(UnmanagedType.I1)] out bool failed);

        [DllImport(Dll, EntryPoint = "RegisterCallback", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SteamAPI_RegisterCallback(IntPtr callback, int id);

        [DllImport(Dll, EntryPoint = "RegisterCallResult", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SteamAPI_RegisterCallResult(IntPtr callback, ulong apiCall);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong ISteamUser_GetSteamID();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr ISteamFriends_GetPersonaName();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong ISteamMatchmaking_CreateLobby(int lobbyType, int maxMembers);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong ISteamMatchmaking_JoinLobby(ulong lobbyId);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern void ISteamMatchmaking_LeaveLobby(ulong lobbyId);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong ISteamMatchmaking_RequestLobbyList();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong ISteamMatchmaking_GetLobbyByIndex(int index);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ISteamMatchmaking_GetNumLobbyMembers(ulong lobbyId);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong ISteamMatchmaking_GetLobbyMemberByIndex(ulong lobbyId, int index);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong ISteamMatchmaking_GetLobbyOwner(ulong lobbyId);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr ISteamMatchmaking_GetLobbyData(ulong lobbyId, IntPtr key);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool ISteamMatchmaking_SetLobbyData(ulong lobbyId, IntPtr key, IntPtr value);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern void ISteamMatchmaking_AddRequestLobbyListStringFilter(IntPtr key, IntPtr value, int comparison);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern void ISteamMatchmaking_AddRequestLobbyListResultCountFilter(int maxResults);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern void ISteamMatchmaking_AddRequestLobbyListDistanceFilter(int distanceFilter);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr ISteamMatchmaking_GetLobbyMemberData(ulong lobbyId, ulong member, IntPtr key);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern void ISteamMatchmaking_SetLobbyMemberData(ulong lobbyId, IntPtr key, IntPtr value);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool ISteamMatchmaking_SetLobbyJoinable(ulong lobbyId,
            [MarshalAs(UnmanagedType.I1)] bool joinable);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool ISteamNetworking_SendP2PPacket(ulong remote, byte[] data, uint size,
            int sendType, int channel);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool ISteamNetworking_IsP2PPacketAvailable(out uint size, int channel);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool ISteamNetworking_ReadP2PPacket(byte[] dest, uint destSize,
            out uint size, out ulong remote, int channel);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool ISteamNetworking_AcceptP2PSessionWithUser(ulong remote);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool ISteamNetworking_CloseP2PSessionWithUser(ulong remote);

        // ------------------------------------------------------------- helpers

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void CallbackDelegate(IntPtr data);

        // Delegates must stay alive for as long as Steam can call them.
        private static readonly List<CallbackDelegate> KeepAlive = new List<CallbackDelegate>();

        public static bool Init() { return SteamAPI_Init(); }
        public static void Shutdown() { SteamAPI_Shutdown(); }
        public static void RunCallbacks() { SteamAPI_RunCallbacks(); }
        public static bool IsSteamRunning() { return SteamAPI_IsSteamRunning(); }

        public static ulong GetSteamId() { return ISteamUser_GetSteamID(); }

        public static string PersonaName()
        {
            try { return ReadUtf8(ISteamFriends_GetPersonaName()); }
            catch { return "Player"; }
        }

        public static void RegisterCallback<T>(int id, Action<T> handler) where T : struct
        {
            CallbackDelegate callback = delegate(IntPtr data)
            {
                try { handler((T)Marshal.PtrToStructure(data, typeof(T))); }
                catch (Exception ex) { RakeMultiplayerPlugin.Log?.LogError("[Steam] callback " + id + ": " + ex.Message); }
            };

            KeepAlive.Add(callback);
            SteamAPI_RegisterCallback(Marshal.GetFunctionPointerForDelegate(callback), id);
        }

        public static void RegisterCallResult<T>(ulong apiCall, int id, Action<T> handler) where T : struct
        {
            CallbackDelegate callback = delegate(IntPtr data)
            {
                try { handler((T)Marshal.PtrToStructure(data, typeof(T))); }
                catch (Exception ex) { RakeMultiplayerPlugin.Log?.LogError("[Steam] call result " + id + ": " + ex.Message); }
            };

            KeepAlive.Add(callback);
            SteamAPI_RegisterCallResult(Marshal.GetFunctionPointerForDelegate(callback), apiCall);
        }

        /// <summary>Poll a SteamAPICall_t result without native callback objects.</summary>
        public static bool TryGetCallResult<T>(ulong apiCall, int callbackId, out T result) where T : struct
        {
            result = default(T);
            if (apiCall == 0) return false;

            bool failed;
            if (!ISteamUtils_IsAPICallCompleted(apiCall, out failed)) return false;

            if (failed)
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[Steam] API call " + apiCall + " failed");
                return false;
            }

            int size = Marshal.SizeOf(typeof(T));
            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                bool ok = ISteamUtils_GetAPICallResult(apiCall, buffer, size, callbackId, out failed);
                if (!ok || failed) return false;
                result = (T)Marshal.PtrToStructure(buffer, typeof(T));
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        // ------------------------------------------------------------- lobby api

        public static ulong CreateLobby(int maxMembers)
        {
            return ISteamMatchmaking_CreateLobby(LobbyTypePublic, maxMembers);
        }

        public static ulong JoinLobby(ulong lobbyId) { return ISteamMatchmaking_JoinLobby(lobbyId); }
        public static void LeaveLobby(ulong lobbyId) { ISteamMatchmaking_LeaveLobby(lobbyId); }
        public static ulong RequestLobbyList() { return ISteamMatchmaking_RequestLobbyList(); }
        public static void LimitLobbyResults(int max) { ISteamMatchmaking_AddRequestLobbyListResultCountFilter(max); }
        /// <summary>0=Close, 1=Default, 2=Far, 3=Worldwide.</summary>
        public static void SetLobbyDistanceFilter(int filter) { ISteamMatchmaking_AddRequestLobbyListDistanceFilter(filter); }
        public static ulong GetLobbyByIndex(int index) { return ISteamMatchmaking_GetLobbyByIndex(index); }
        public static int GetNumLobbyMembers(ulong lobbyId) { return ISteamMatchmaking_GetNumLobbyMembers(lobbyId); }
        public static ulong GetLobbyMemberByIndex(ulong lobbyId, int index) { return ISteamMatchmaking_GetLobbyMemberByIndex(lobbyId, index); }
        public static ulong GetLobbyOwner(ulong lobbyId) { return ISteamMatchmaking_GetLobbyOwner(lobbyId); }
        public static bool SetLobbyJoinable(ulong lobbyId, bool joinable) { return ISteamMatchmaking_SetLobbyJoinable(lobbyId, joinable); }

        public static void AddGameFilter(string key, string value)
        {
            IntPtr k = ToUtf8(key);
            IntPtr v = ToUtf8(value);
            try { ISteamMatchmaking_AddRequestLobbyListStringFilter(k, v, 0); }
            finally { Free(k); Free(v); }
        }

        public static string GetLobbyData(ulong lobbyId, string key)
        {
            IntPtr k = ToUtf8(key);
            try { return ReadUtf8(ISteamMatchmaking_GetLobbyData(lobbyId, k)); }
            finally { Free(k); }
        }

        public static void SetLobbyData(ulong lobbyId, string key, string value)
        {
            IntPtr k = ToUtf8(key);
            IntPtr v = ToUtf8(value ?? string.Empty);
            try { ISteamMatchmaking_SetLobbyData(lobbyId, k, v); }
            finally { Free(k); Free(v); }
        }

        public static string GetLobbyMemberData(ulong lobbyId, ulong member, string key)
        {
            IntPtr k = ToUtf8(key);
            try { return ReadUtf8(ISteamMatchmaking_GetLobbyMemberData(lobbyId, member, k)); }
            finally { Free(k); }
        }

        public static void SetLobbyMemberData(ulong lobbyId, string key, string value)
        {
            IntPtr k = ToUtf8(key);
            IntPtr v = ToUtf8(value ?? string.Empty);
            try { ISteamMatchmaking_SetLobbyMemberData(lobbyId, k, v); }
            finally { Free(k); Free(v); }
        }

        // ------------------------------------------------------------- p2p api

        public static bool SendP2P(ulong remote, byte[] data)
        {
            return ISteamNetworking_SendP2PPacket(remote, data, (uint)data.Length, SendReliable, 0);
        }

        public static bool IsPacketAvailable(out uint size, int channel)
        {
            return ISteamNetworking_IsP2PPacketAvailable(out size, channel);
        }

        public static bool ReadPacket(byte[] buffer, out uint size, out ulong remote, int channel)
        {
            return ISteamNetworking_ReadP2PPacket(buffer, (uint)buffer.Length, out size, out remote, channel);
        }

        public static void AcceptSession(ulong remote) { ISteamNetworking_AcceptP2PSessionWithUser(remote); }
        public static void CloseSession(ulong remote) { ISteamNetworking_CloseP2PSessionWithUser(remote); }

        // ------------------------------------------------------------- utf8

        private static IntPtr ToUtf8(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
            IntPtr ptr = Marshal.AllocHGlobal(bytes.Length + 1);
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            Marshal.WriteByte(ptr, bytes.Length, 0);
            return ptr;
        }

        private static void Free(IntPtr ptr)
        {
            if (ptr != IntPtr.Zero) Marshal.FreeHGlobal(ptr);
        }

        private static string ReadUtf8(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero) return string.Empty;

            int length = 0;
            while (Marshal.ReadByte(ptr, length) != 0) length++;

            if (length == 0) return string.Empty;

            byte[] bytes = new byte[length];
            Marshal.Copy(ptr, bytes, 0, length);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
