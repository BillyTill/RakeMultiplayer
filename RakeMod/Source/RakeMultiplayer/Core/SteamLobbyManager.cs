using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using RakeMultiplayer.Networking;

namespace RakeMultiplayer
{
    /// <summary>
    /// Steam lobby browser + lobby lifecycle on top of Spacewar (app 480).
    ///
    /// One Steam lobby = one match. The lobby carries the display name, host name,
    /// max players and an optional password hash. Gameplay itself runs over the
    /// existing packet protocol, tunnelled through Steam P2P (SteamServerTransport /
    /// SteamClientTransport) instead of Telepathy.
    /// </summary>
    public class SteamLobbyManager : MonoBehaviour
    {
        public static SteamLobbyManager Instance { get; private set; }

        public sealed class LobbyInfo
        {
            public ulong Id;
            public string Name = "Hunt";
            public string Host = "Player";
            public int Members;
            public int Max = 4;
            public bool HasPassword;
            public string PasswordHash = "";
        }

        public static readonly List<LobbyInfo> Lobbies = new List<LobbyInfo>();

        public static bool InLobby { get; private set; }
        public static ulong CurrentLobby { get; private set; }
        public static ulong LobbyOwner { get; private set; }
        public static string LastError = "";

        private bool _registered;
        private ulong _createCall;
        private ulong _joinCall;
        private ulong _listCall;
        private float _memberSync;
        private bool _listPending;
        private float _listRequestTime;
        private readonly HashSet<ulong> _knownMembers = new HashSet<ulong>();
        private string _pendingName;
        private string _pendingPasswordHash;
        private int _pendingMax;

        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (SteamIntegration.Initialized) EnsureRegistered();
        }

        private void Update()
        {
            if (!_registered && SteamIntegration.Initialized) EnsureRegistered();
            if (!SteamIntegration.Initialized) return;

            PollApiCalls();
            PollLobbyMembers();

            if (_listPending && Time.unscaledTime - _listRequestTime > 10f)
            {
                _listPending = false;
                LastError = "Lobby list timed out";
            }


        }

        private void EnsureRegistered()
        {
            if (_registered) return;
            _registered = true;

            // Callbacks are not used: Steamworks.NET's native callback objects are not
            // available to a net35 assembly, so lobby/API results are polled in Update
            // and lobby member changes are detected by diffing the member list.
            RakeMultiplayerPlugin.Log?.LogInfo("[Steam] Lobby manager ready (polling mode)");
        }

        // ------------------------------------------------------------- create

        public bool CanUseSteam
        {
            get { return SteamIntegration.Initialized; }
        }

        public string SteamStatus
        {
            get
            {
                if (!SteamIntegration.Initialized)
                    return "Steam unavailable: " + SteamIntegration.Status;
                if (InLobby) return "In lobby (" + SteamNative.PersonaName() + ")";
                return "Steam ready as " + SteamNative.PersonaName();
            }
        }

        public void CreateLobby(string name, string password, int maxPlayers)
        {
            EnsureRegistered();
            LastError = "";
            if (InLobby) LeaveLobby();

            if (!SteamIntegration.Initialized)
            {
                LastError = SteamLobbyStatus();
                return;
            }

            _pendingName = string.IsNullOrEmpty(name) ? SteamNative.PersonaName() + "'s Hunt" : name;
            _pendingPasswordHash = HashPassword(password);
            _pendingMax = Mathf.Clamp(maxPlayers, 2, 4);

            _createCall = SteamNative.CreateLobby(_pendingMax);
            RakeMultiplayerPlugin.Log?.LogInfo("[Steam] CreateLobby call=" + _createCall);
            if (_createCall == 0)
            {
                LastError = "CreateLobby failed";
                return;
            }

            LastError = "Creating lobby...";
        }

        private void OnLobbyCreated(SteamNative.LobbyCreated result)
        {
            RakeMultiplayerPlugin.Log?.LogInfo("[Steam] LobbyCreated callback result=" + result.Result + " lobby=" + result.LobbyId);
            if (result.Result != 1 || result.LobbyId == 0)
            {
                LastError = "CreateLobby failed (result " + result.Result + ")";
                RakeMultiplayerPlugin.Log?.LogError("[Steam] " + LastError);
                return;
            }

            CurrentLobby = result.LobbyId;
            LobbyOwner = SteamIntegration.LocalSteamId;
            InLobby = true;

            SteamNative.SetLobbyData(CurrentLobby, "game", "RakeMultiplayer");
            SteamNative.SetLobbyData(CurrentLobby, "name", _pendingName);
            SteamNative.SetLobbyData(CurrentLobby, "host", SteamNative.PersonaName());
            SteamNative.SetLobbyData(CurrentLobby, "pw", _pendingPasswordHash ?? "");
            SteamNative.SetLobbyData(CurrentLobby, "max", _pendingMax.ToString());
            SteamNative.SetLobbyMemberData(CurrentLobby, "name", SteamNative.PersonaName());
            SteamNative.SetLobbyJoinable(CurrentLobby, true);

            LastError = "";
            RakeMultiplayerPlugin.Log?.LogInfo("[Steam] Lobby created: " + CurrentLobby +
                                               " '" + _pendingName + "' max=" + _pendingMax +
                                               (string.IsNullOrEmpty(_pendingPasswordHash) ? "" : " (password)"));

            StartSteamHost();
        }

        private void StartSteamHost()
        {
            RakeMultiplayerPlugin.UseSteamP2P.Value = true;

            var manager = RakeNetworkManager.Instance;
            if (manager == null)
            {
                var go = new GameObject("RakeNetworkManager");
                DontDestroyOnLoad(go);
                manager = go.AddComponent<RakeNetworkManager>();
            }

            manager.StartHost();

            var menu = FindObjectOfType<UI.MultiplayerMenu>();
            if (menu != null) menu.ShowLobby();
        }

        // ------------------------------------------------------------- browse

        public void RefreshLobbies()
        {
            EnsureRegistered();

            if (!SteamIntegration.Initialized)
            {
                LastError = SteamLobbyStatus();
                return;
            }

            // Do not stack requests: Steam returns 0 for a second concurrent list call.
            if (_listPending) return;
            _listPending = true;
            _listRequestTime = Time.unscaledTime;

            Lobbies.Clear();
            LastError = "";

            SteamNative.AddGameFilter("game", "RakeMultiplayer");
            // Default lobby distance is "close": friends in another region never saw
            // each other. Worldwide + a larger limit fixes that.
            SteamNative.SetLobbyDistanceFilter(3);
            SteamNative.LimitLobbyResults(50);

            _listCall = SteamNative.RequestLobbyList();
            if (_listCall == 0)
            {
                _listPending = false;
                LastError = "RequestLobbyList failed";
                return;
            }

            LastError = "Refreshing...";
        }

        private void OnLobbyMatchList(SteamNative.LobbyMatchList list)
        {
            RakeMultiplayerPlugin.Log?.LogInfo("[Steam] LobbyMatchList callback count=" + list.Count);
            _listPending = false;
            Lobbies.Clear();

            int count = (int)list.Count;
            for (int i = 0; i < count; i++)
            {
                ulong id = SteamNative.GetLobbyByIndex(i);
                if (id == 0) continue;

                var info = new LobbyInfo
                {
                    Id = id,
                    Name = SteamNative.GetLobbyData(id, "name"),
                    Host = SteamNative.GetLobbyData(id, "host"),
                    PasswordHash = SteamNative.GetLobbyData(id, "pw") ?? "",
                    Members = SteamNative.GetNumLobbyMembers(id)
                };

                if (string.IsNullOrEmpty(info.Name)) info.Name = "Hunt";
                if (string.IsNullOrEmpty(info.Host)) info.Host = "Host";
                info.HasPassword = !string.IsNullOrEmpty(info.PasswordHash);

                int max;
                if (int.TryParse(SteamNative.GetLobbyData(id, "max"), out max) && max > 0) info.Max = max;
                else info.Max = 4;

                Lobbies.Add(info);
            }

            LastError = Lobbies.Count == 0 ? "No lobbies found" : "";
            RakeMultiplayerPlugin.Log?.LogInfo("[Steam] Lobby list: " + Lobbies.Count + " result(s)");
        }

        public bool JoinLobby(LobbyInfo info, string password)
        {
            EnsureRegistered();
            LastError = "";
            if (InLobby) LeaveLobby();

            if (info == null) return false;

            if (info.HasPassword && HashPassword(password) != info.PasswordHash)
            {
                LastError = "Wrong password";
                return false;
            }

            _joinCall = SteamNative.JoinLobby(info.Id);
            if (_joinCall == 0)
            {
                LastError = "JoinLobby failed";
                return false;
            }

            LastError = "Joining lobby...";
            return true;
        }

        private void OnLobbyEnter(SteamNative.LobbyEnter result)
        {
            if (result.Response != 1 || result.LobbyId == 0)
            {
                LastError = "Join failed (response " + result.Response + ")";
                RakeMultiplayerPlugin.Log?.LogError("[Steam] " + LastError);
                return;
            }

            CurrentLobby = result.LobbyId;
            LobbyOwner = SteamNative.GetLobbyOwner(CurrentLobby);
            InLobby = true;
            LastError = "";

            SteamNative.SetLobbyMemberData(CurrentLobby, "name", SteamNative.PersonaName());

            RakeMultiplayerPlugin.Log?.LogInfo("[Steam] Joined lobby " + CurrentLobby +
                                               " owner=" + LobbyOwner);

            RakeMultiplayerPlugin.UseSteamP2P.Value = true;
            RakeMultiplayerPlugin.NetworkAddress.Value = LobbyOwner.ToString();
            SteamNative.AcceptSession(LobbyOwner);

            var manager = RakeNetworkManager.Instance;
            if (manager == null)
            {
                var go = new GameObject("RakeNetworkManager");
                DontDestroyOnLoad(go);
                manager = go.AddComponent<RakeNetworkManager>();
            }

            manager.StartClient();

            var menu = FindObjectOfType<UI.MultiplayerMenu>();
            if (menu != null) menu.ShowLobby();
        }

        private void PollApiCalls()
        {
            SteamNative.LobbyCreated created;
            if (_createCall != 0 && SteamNative.TryGetCallResult(
                    _createCall, SteamNative.CallbackLobbyCreated, out created))
            {
                _createCall = 0;
                OnLobbyCreated(created);
            }

            SteamNative.LobbyEnter enter;
            if (_joinCall != 0 && SteamNative.TryGetCallResult(
                    _joinCall, SteamNative.CallbackLobbyEnter, out enter))
            {
                _joinCall = 0;
                OnLobbyEnter(enter);
            }

            SteamNative.LobbyMatchList list;
            if (_listCall != 0 && SteamNative.TryGetCallResult(
                    _listCall, SteamNative.CallbackLobbyMatchList, out list))
            {
                _listCall = 0;
                OnLobbyMatchList(list);
            }
        }

        /// <summary>Server: keep the P2P peers in sync with the Steam lobby members.</summary>
        private void PollLobbyMembers()
        {
            if (!InLobby || CurrentLobby == 0) return;
            if (LobbyOwner != SteamIntegration.LocalSteamId) return;

            _memberSync -= Time.unscaledDeltaTime;
            if (_memberSync > 0f) return;
            _memberSync = 1f;

            var current = new HashSet<ulong>();
            int count = SteamNative.GetNumLobbyMembers(CurrentLobby);
            for (int i = 0; i < count; i++)
            {
                ulong member = SteamNative.GetLobbyMemberByIndex(CurrentLobby, i);
                if (member != 0) current.Add(member);
            }

            if (SteamServerTransport.Current != null)
            {
                foreach (ulong old in _knownMembers)
                {
                    if (old == SteamIntegration.LocalSteamId) continue;
                    if (!current.Contains(old)) SteamServerTransport.Current.OnPeerLeft(old);
                }

                foreach (ulong member in current)
                {
                    if (member == SteamIntegration.LocalSteamId) continue;
                    SteamServerTransport.Current.OnSessionRequest(member);
                }
            }

            _knownMembers.Clear();
            foreach (ulong member in current) _knownMembers.Add(member);
        }
        // ------------------------------------------------------------- leave

        public void LeaveLobby()
        {
            if (!InLobby) return;

            try { SteamNative.LeaveLobby(CurrentLobby); } catch { }

            var manager = RakeNetworkManager.Instance;
            if (manager != null) manager.Disconnect();

            InLobby = false;
            CurrentLobby = 0;
            _knownMembers.Clear();
            _createCall = _joinCall = _listCall = 0;
            LobbyOwner = 0;
            RakeMultiplayerPlugin.Log?.LogInfo("[Steam] Left lobby");
        }

        // ------------------------------------------------------------- helpers

        public static bool IsLobbyMember(ulong steamId)
        {
            if (!InLobby || CurrentLobby == 0 || steamId == 0) return false;

            int count = SteamNative.GetNumLobbyMembers(CurrentLobby);
            for (int i = 0; i < count; i++)
            {
                if (SteamNative.GetLobbyMemberByIndex(CurrentLobby, i) == steamId) return true;
            }

            return false;
        }

        public static List<string> GetMemberNames()
        {
            var names = new List<string>();
            if (!InLobby || CurrentLobby == 0) return names;

            int count = SteamNative.GetNumLobbyMembers(CurrentLobby);
            for (int i = 0; i < count; i++)
            {
                ulong id = SteamNative.GetLobbyMemberByIndex(CurrentLobby, i);
                string name = SteamNative.GetLobbyMemberData(CurrentLobby, id, "name");
                if (string.IsNullOrEmpty(name)) name = id.ToString();
                if (id == LobbyOwner) name = "HOST · " + name;
                names.Add(name);
            }

            return names;
        }

        public static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password)) return "";

            using (var sha = SHA1.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes("rakemp:" + password));
                var sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
                return sb.ToString();
            }
        }

        private static string SteamLobbyStatus()
        {
            return "Steam unavailable: " + SteamIntegration.Status;
        }

        // ------------------------------------------------------------- callbacks

    }
}
