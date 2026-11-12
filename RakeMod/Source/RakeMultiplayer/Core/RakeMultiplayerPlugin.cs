using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using RakeMultiplayer.Networking;

namespace RakeMultiplayer
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.NAME, PluginInfo.VERSION)]
    public class RakeMultiplayerPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<string> NetworkAddress;
        internal static ConfigEntry<ushort> NetworkPort;
        internal static ConfigEntry<int> MaxPlayers;
        internal static ConfigEntry<bool> IsServer;
        internal static ConfigEntry<bool> UseSteamP2P;
        internal static ConfigEntry<string> SteamAppId;
        internal static ConfigEntry<bool> RunInBackground;
        internal static ConfigEntry<bool> ControlLighting;
        internal static ConfigEntry<bool> HitReaction;

        // Network state (managed by RakeNetworkManager)
        internal static bool isServer = false;
        internal static bool isConnected = false;

        // Item synchronization flag (prevents recursive patch execution)
        internal static bool IsApplyingNetworkDestroy = false;

        // Camera synchronization flag (prevents recursive patch execution when the
        // network layer drives RecordCamera_obj.SetSetupState/SetUnsetupState)
        internal static bool IsApplyingNetworkCameraState = false;

        // Config accessors
        internal static ConfigEntry<string> GameSceneName { get; private set; }

        // Singleton instance
        public static RakeMultiplayerPlugin Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            Log.LogInfo($"[{PluginInfo.NAME}] Loading version {PluginInfo.VERSION}");

            // Configuration
            Enabled = Config.Bind("General", "Enabled", true, "Enable the multiplayer mod");
            NetworkAddress = Config.Bind("Network", "Address", "127.0.0.1", "Server IP address");
            NetworkPort = Config.Bind("Network", "Port", (ushort)7777, "Server port");
            MaxPlayers = Config.Bind("Network", "MaxPlayers", 4, "Maximum players (2-4)");
            IsServer = Config.Bind("Network", "IsServer", false, "Run as server/host");
            UseSteamP2P = Config.Bind("Network", "UseSteamP2P", false, "Use Steam P2P networking (not implemented)");
            SteamAppId = Config.Bind("Network", "SteamAppId", "480", "Steam App ID for P2P");
            GameSceneName = Config.Bind("Network", "GameSceneName", "level1", "Name of the game scene to load (must match build settings, e.g. level1)");
            RunInBackground = Config.Bind("General", "RunInBackground", true,
                "Keep simulating while the game window is not focused (lets you host and play on one machine)");

            // The game levels ship their own day/night lighting; overriding it makes
            // night look like day under a black sky. Off by default.
            HitReaction = Config.Bind("General", "HitReaction", true,
                "Play the ragdoll hit reaction when the Rake hits the local player");

            ControlLighting = Config.Bind("General", "ControlLighting", false,
                "Let the mod drive scene ambient light and sun intensity (off = keep the level lighting)");

            // Two instances on one machine are unusable otherwise: the unfocused
            // window freezes, so the host stops simulating for the client.
            Application.runInBackground = RunInBackground.Value;
            Log.LogInfo($"[Multiplayer] runInBackground = {Application.runInBackground}");

            // In-game debug console (toggle with the backquote key)
            try
            {
                var consoleGo = new GameObject("RakeDebugConsole");
                UnityEngine.Object.DontDestroyOnLoad(consoleGo);
                consoleGo.AddComponent<DebugConsole>();
            }
            catch (Exception ex)
            {
                Log.LogWarning("[Multiplayer] debug console failed: " + ex.Message);
            }

            // Initialize Harmony — ВСЕГДА (для F1 меню)
            // Diagnostic: verify that every type in this assembly can be loaded by the
            // game's Mono runtime (mismatched mscorlib/System.Core versions show up here).
            try
            {
                var loadedTypes = typeof(RakeMultiplayerPlugin).Assembly.GetTypes();
                Log?.LogInfo($"[Diag] Assembly types loaded: {loadedTypes.Length}");
            }
            catch (System.Reflection.ReflectionTypeLoadException ex)
            {
                int count = ex.LoaderExceptions != null ? ex.LoaderExceptions.Length : 0;
                Log?.LogError($"[Diag] Type load failures: {count}");
                if (ex.LoaderExceptions != null)
                {
                    for (int i = 0; i < ex.LoaderExceptions.Length && i < 20; i++)
                    {
                        Log?.LogError($"  [{i + 1}] {ex.LoaderExceptions[i]}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log?.LogError($"[Diag] Assembly type scan failed: {ex.Message}");
            }

            var harmony = new HarmonyLib.Harmony(PluginInfo.GUID);
            var applied = 0;
            var failed = 0;
            // Apply each patch class independently: one broken target must never abort
            // all remaining patches (PatchAll is all-or-nothing and hides the culprit).
            var patchTypes = new System.Collections.Generic.List<Type>();
            foreach (var t in Assembly.GetExecutingAssembly().GetTypes())
            {
                if (t == null) continue;
                if (t.GetCustomAttributes(typeof(HarmonyLib.HarmonyPatch), false).Length > 0)
                {
                    patchTypes.Add(t);
                }
            }

            foreach (var patchType in patchTypes)
            {
                try
                {
                    harmony.CreateClassProcessor(patchType).Patch();
                    applied++;
                }
                catch (Exception ex)
                {
                    failed++;
                    Log.LogError("[Harmony] Patch class " + patchType.FullName + " failed: " + ex);
                }
            }

            try
            {
                var patchedMethods = harmony.GetPatchedMethods();
                var methodNames = new System.Collections.Generic.List<string>();
                foreach (var m in patchedMethods)
                {
                    methodNames.Add(m.DeclaringType.Name + "." + m.Name);
                }
                Log.LogInfo("[Harmony] Patched methods (" + methodNames.Count + "): " + string.Join(", ", methodNames.ToArray()));
            }
            catch (Exception ex)
            {
                Log.LogWarning("[Harmony] Could not list patched methods: " + ex.Message);
            }
            Log.LogInfo("[RakeMultiplayer] Harmony patches: applied=" + applied + ", failed=" + failed);

            // Create MultiplayerMenu (DontDestroyOnLoad) — available in ALL scenes (MainMenu, Game, Lobby)
            var menuObj = new GameObject("MultiplayerMenu");
            UnityEngine.Object.DontDestroyOnLoad(menuObj);
            menuObj.AddComponent<UI.MultiplayerMenu>();
            Log.LogInfo("[RakeMultiplayer] MultiplayerMenu created in plugin Awake");

            // InitializeNetworking() УБРАН — сеть только через UI
            // Если Enabled = false, сеть не стартует, но патчи применены (F1 работает)

            // Serialization unit test (DEBUG only)
            RunSerializationTest();

            Log.LogInfo($"[{PluginInfo.NAME}] Loaded successfully");
        }

        /// <summary>
        /// Unit test for packet serialization/deserialization
        /// </summary>
        private static void RunSerializationTest()
        {
            Log.LogInfo("[SerTest] ===== FULL FIELD VERIFICATION v2 =====");
            
            int passed = 0, failed = 0;
            const float EPS = 0.001f;

            // Test 1: PlayerState - проверка всех полей
            try
            {
                var original = new PlayerStatePacket 
                { 
                    PlayerId = 42, 
                    Position = new Vector3(1, 2, 3), 
                    Rotation = new Quaternion(0.1f, 0.2f, 0.3f, 0.4f), 
                    AnimState = 1 
                };
                var bytes = original.Serialize();
                var restored = PacketSerializer.Deserialize(bytes) as PlayerStatePacket;
                if (restored.PlayerId == 42
                    && restored.Position == new Vector3(1, 2, 3)
                    && Math.Abs(restored.Rotation.x - 0.1f) < EPS
                    && Math.Abs(restored.Rotation.y - 0.2f) < EPS
                    && Math.Abs(restored.Rotation.z - 0.3f) < EPS
                    && Math.Abs(restored.Rotation.w - 0.4f) < EPS
                    && restored.AnimState == 1)
                { passed++; Log.LogInfo("[SerTest] PlayerState: OK (" + bytes.Length + " bytes)"); }
                else { failed++; Log.LogError("[SerTest] PlayerState: FAILED (values mismatch)"); }
            }
            catch (Exception ex) { failed++; Log.LogError("[SerTest] PlayerState: EXCEPTION " + ex.Message); }

            // Test 2: RakeState - проверка всех полей
            try
            {
                var original = new RakeStatePacket 
                { 
                    Position = new Vector3(10, 20, 30), 
                    Rotation = new Quaternion(0.5f, -0.5f, 0.5f, -0.5f), 
                    State = 2, 
                    Health = 100f 
                };
                var bytes = original.Serialize();
                var restored = PacketSerializer.Deserialize(bytes) as RakeStatePacket;
                if (restored.Position == new Vector3(10, 20, 30)
                    && Math.Abs(restored.Rotation.x - 0.5f) < EPS
                    && Math.Abs(restored.Rotation.y + 0.5f) < EPS
                    && Math.Abs(restored.Rotation.z - 0.5f) < EPS
                    && Math.Abs(restored.Rotation.w + 0.5f) < EPS
                    && restored.State == 2
                    && Math.Abs(restored.Health - 100f) < EPS)
                { passed++; Log.LogInfo("[SerTest] RakeState: OK (" + bytes.Length + " bytes)"); }
                else { failed++; Log.LogError("[SerTest] RakeState: FAILED (values mismatch)"); }
            }
            catch (Exception ex) { failed++; Log.LogError("[SerTest] RakeState: EXCEPTION " + ex.Message); }

            // Test 3: PlayerConnected - проверка всех полей
            try
            {
                var original = new PlayerConnectedPacket 
                { 
                    PlayerId = 7, 
                    SpawnPosition = new Vector3(5, 0, 5) 
                };
                var bytes = original.Serialize();
                var restored = PacketSerializer.Deserialize(bytes) as PlayerConnectedPacket;
                if (restored.PlayerId == 7
                    && restored.SpawnPosition == new Vector3(5, 0, 5))
                { passed++; Log.LogInfo("[SerTest] PlayerConnected: OK (" + bytes.Length + " bytes)"); }
                else { failed++; Log.LogError("[SerTest] PlayerConnected: FAILED (values mismatch)"); }
            }
            catch (Exception ex) { failed++; Log.LogError("[SerTest] PlayerConnected: EXCEPTION " + ex.Message); }

            // Test 4: PlayerDisconnected - проверка всех полей
            try
            {
                var original = new PlayerDisconnectedPacket 
                { 
                    PlayerId = 7 
                };
                var bytes = original.Serialize();
                var restored = PacketSerializer.Deserialize(bytes) as PlayerDisconnectedPacket;
                if (restored.PlayerId == 7)
                { passed++; Log.LogInfo("[SerTest] PlayerDisconnected: OK (" + bytes.Length + " bytes)"); }
                else { failed++; Log.LogError("[SerTest] PlayerDisconnected: FAILED (values mismatch)"); }
            }
            catch (Exception ex) { failed++; Log.LogError("[SerTest] PlayerDisconnected: EXCEPTION " + ex.Message); }

            // Test 5: DamageRequest - проверка всех полей
            try
            {
                var original = new DamageRequestPacket 
                { 
                    ShooterId = 1, 
                    TargetId = 2, 
                    Amount = 100, 
                    DamageType = "SniperShot",
                    ShooterPosition = new Vector3(1.5f, 2.5f, 3.5f)
                };
                var bytes = original.Serialize();
                var restored = PacketSerializer.Deserialize(bytes) as DamageRequestPacket;
                if (restored.ShooterId == 1
                    && restored.TargetId == 2
                    && restored.Amount == 100
                    && restored.DamageType == "SniperShot"
                    && restored.ShooterPosition == new Vector3(1.5f, 2.5f, 3.5f))
                { passed++; Log.LogInfo("[SerTest] DamageRequest: OK (" + bytes.Length + " bytes)"); }
                else { failed++; Log.LogError("[SerTest] DamageRequest: FAILED (values mismatch)"); }
            }
            catch (Exception ex) { failed++; Log.LogError("[SerTest] DamageRequest: EXCEPTION " + ex.Message); }

            // Test 6: DamageApplied - проверка всех полей
            try
            {
                var original = new DamageAppliedPacket 
                { 
                    TargetId = 2, 
                    NewHealth = 900f 
                };
                var bytes = original.Serialize();
                var restored = PacketSerializer.Deserialize(bytes) as DamageAppliedPacket;
                if (restored.TargetId == 2
                    && Math.Abs(restored.NewHealth - 900f) < EPS)
                { passed++; Log.LogInfo("[SerTest] DamageApplied: OK (" + bytes.Length + " bytes)"); }
                else { failed++; Log.LogError("[SerTest] DamageApplied: FAILED (values mismatch)"); }
            }
            catch (Exception ex) { failed++; Log.LogError("[SerTest] DamageApplied: EXCEPTION " + ex.Message); }

            // Test 7: FireVisuals - проверка всех полей
            try
            {
                var original = new FireVisualsPacket 
                { 
                    ShooterId = 1, 
                    WeaponType = 0, 
                    Origin = new Vector3(1.5f, 2.5f, 3.5f), 
                    Direction = new Vector3(0.1f, 0.2f, 0.3f) 
                };
                var bytes = original.Serialize();
                var restored = PacketSerializer.Deserialize(bytes) as FireVisualsPacket;
                if (restored.ShooterId == 1
                    && restored.WeaponType == 0
                    && restored.Origin == new Vector3(1.5f, 2.5f, 3.5f)
                    && Math.Abs(restored.Direction.x - 0.1f) < EPS
                    && Math.Abs(restored.Direction.y - 0.2f) < EPS
                    && Math.Abs(restored.Direction.z - 0.3f) < EPS)
                { passed++; Log.LogInfo("[SerTest] FireVisuals: OK (" + bytes.Length + " bytes)"); }
                else { failed++; Log.LogError("[SerTest] FireVisuals: FAILED (values mismatch)"); }
            }
            catch (Exception ex) { failed++; Log.LogError("[SerTest] FireVisuals: EXCEPTION " + ex.Message); }

            Log.LogInfo("[SerTest] Summary: passed=" + passed + ", failed=" + failed);

            // DispatchTest: симуляция маршрутизации
            RakeMultiplayerPlugin.Log?.LogInfo("[DispatchTest] ===== ROUTING TEST =====");

            var statePacket = new PlayerStatePacket 
            { 
                PlayerId = 1, 
                Position = new Vector3(1, 2, 3), 
                Rotation = new Quaternion(0.1f, 0.2f, 0.3f, 0.4f), 
                AnimState = 1 
            };
            var stateBytes = statePacket.Serialize();
            var stateRestored = PacketSerializer.Deserialize(stateBytes);

            if (stateRestored is PlayerStatePacket ps && ps.PlayerId == 1 && ps.Position == new Vector3(1, 2, 3))
                RakeMultiplayerPlugin.Log?.LogInfo("[DispatchTest] PlayerState routing: OK");
            else
                RakeMultiplayerPlugin.Log?.LogError("[DispatchTest] PlayerState routing: FAILED");

            var damagePacket = new DamageRequestPacket 
            { 
                ShooterId = 1, 
                TargetId = 2, 
                Amount = 100, 
                DamageType = "SniperShot",
                ShooterPosition = new Vector3(1.5f, 2.5f, 3.5f)
            };
            var damageBytes = damagePacket.Serialize();
            var damageRestored = PacketSerializer.Deserialize(damageBytes);

            if (damageRestored is DamageRequestPacket dr && dr.DamageType == "SniperShot" && dr.ShooterPosition == new Vector3(1.5f, 2.5f, 3.5f))
                RakeMultiplayerPlugin.Log?.LogInfo("[DispatchTest] DamageRequest routing: OK");
            else
                RakeMultiplayerPlugin.Log?.LogError("[DispatchTest] DamageRequest routing: FAILED");
        }

        public void InitializeNetworking()
        {
            // Networking is now handled by RakeNetworkManager
            // This method kept for compatibility with UI calls
            isServer = IsServer.Value;
        }

        // Static helper methods for components to send packets
        public static void SendToServer(byte[] data)
        {
            if (isConnected && RakeNetworkManager.Instance != null)
            {
                RakeNetworkManager.Instance.SendToServer(data);
            }
        }

        public static void SendToAllClients(byte[] data)
        {
            if (isServer && RakeNetworkManager.Instance != null)
            {
                RakeNetworkManager.Instance.SendToAllClients(data);
            }
        }

        public static int GetConnectedClientCount()
        {
            if (isServer && RakeNetworkManager.Instance != null)
            {
                return RakeNetworkManager.Instance.GetConnectedClientCount();
            }
            return 0;
        }

        private void Update()
        {
            if (!Enabled.Value) return;
            if (Application.runInBackground != RunInBackground.Value)
            {
                Application.runInBackground = RunInBackground.Value;
            }

            // Networking is event-driven via RakeNetworkManager
        }

        private void OnDestroy()
        {
            // Cleanup handled by RakeNetworkManager
        }
    }

    public static class PluginInfo
    {
        public const string GUID = "com.rake.multiplayer";
        public const string NAME = "Rake Multiplayer";
        public const string VERSION = "1.0.0";
    }
}