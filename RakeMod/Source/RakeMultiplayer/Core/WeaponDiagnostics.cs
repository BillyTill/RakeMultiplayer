using System;
using UnityEngine;

namespace RakeMultiplayer
{
    /// <summary>
    /// Runtime weapon/inventory diagnostics. Press F8 in game to dump the real
    /// WeaponManager state (memory-backed Unity objects) into the BepInEx log.
    /// </summary>
    public static class WeaponDiagnostics
    {
        public static void Dump(string reason)
        {
            var log = RakeMultiplayerPlugin.Log;
            if (log == null) return;

            log.LogInfo($"[WeaponDiag] ===== DUMP ({reason}) =====");
            log.LogInfo($"[WeaponDiag] isServer={RakeMultiplayerPlugin.isServer} isConnected={RakeMultiplayerPlugin.isConnected} scene={Application.loadedLevelName}");

            var spawner = PlayerSpawner.Instance;
            var local = spawner != null ? spawner.LocalPlayer : null;
            int localId = local != null ? local.PlayerId : -1;

            log.LogInfo($"[WeaponDiag] LocalPlayer present={local != null} id={localId}");

            var localWm = local != null ? local.GetComponentInChildren<WeaponManager>() : null;
            log.LogInfo($"[WeaponDiag] WeaponManager.instance={(WeaponManager.instance != null ? WeaponManager.instance.transform.root.name : "null")}");
            log.LogInfo($"[WeaponDiag] instance==localWM={ReferenceEquals(WeaponManager.instance, localWm)}");

            DumpManager("LOCAL", localWm);

            var all = UnityEngine.Object.FindObjectsOfType<WeaponManager>();
            log.LogInfo($"[WeaponDiag] WeaponManager count in scene={all.Length}");
            foreach (var wm in all)
            {
                if (wm == null || ReferenceEquals(wm, localWm)) continue;
                DumpManager("OTHER " + wm.transform.root.name, wm);
            }

            var netPlayers = UnityEngine.Object.FindObjectsOfType<NetworkedPlayer>();
            foreach (var np in netPlayers)
            {
                if (np == null) continue;
                log.LogInfo($"[WeaponDiag] NetworkedPlayer {np.gameObject.name}: id={np.PlayerId} isLocal={np.isLocalPlayer}");
            }

            log.LogInfo("[WeaponDiag] ===== END DUMP =====");
        }

        private static void DumpManager(string tag, WeaponManager wm)
        {
            var log = RakeMultiplayerPlugin.Log;
            if (wm == null)
            {
                log?.LogInfo($"[WeaponDiag] {tag}: <null>");
                return;
            }

            log.LogInfo($"[WeaponDiag] {tag}: enabled={wm.enabled} root={wm.transform.root.name}");

            try
            {
                int count = wm.playerWeapons != null ? wm.playerWeapons.Count : -1;
                log.LogInfo($"[WeaponDiag] {tag}: playerWeapons.Count={count}");
                if (wm.playerWeapons != null)
                {
                    for (int i = 0; i < wm.playerWeapons.Count; i++)
                    {
                        var t = wm.playerWeapons[i];
                        if (t == null)
                        {
                            log.LogInfo($"[WeaponDiag] {tag}:   [{i}] <null>");
                            continue;
                        }
                        var weapon = t.GetComponent<Weapon>();
                        int id = weapon != null ? weapon.weaponID : -1;
                        log.LogInfo($"[WeaponDiag] {tag}:   [{i}] {t.name} weaponID={id} active={t.gameObject.activeSelf}");
                    }
                }
            }
            catch (Exception ex)
            {
                log.LogError($"[WeaponDiag] {tag}: playerWeapons dump failed: {ex.Message}");
            }

            try
            {
                if (wm.weaponsAmmo != null)
                {
                    log.LogInfo($"[WeaponDiag] {tag}: weaponsAmmo.Count={wm.weaponsAmmo.Count}");
                    for (int i = 0; i < wm.weaponsAmmo.Count; i++)
                    {
                        log.LogInfo($"[WeaponDiag] {tag}:   ammo[{i}]={wm.weaponsAmmo[i]}");
                    }
                }
            }
            catch (Exception ex)
            {
                log.LogError($"[WeaponDiag] {tag}: weaponsAmmo dump failed: {ex.Message}");
            }

            log.LogInfo($"[WeaponDiag] {tag}: currentWeaponID={wm.currentWeaponID}");
            for (int id = 1; id <= 8; id++)
            {
                try
                {
                    log.LogInfo($"[WeaponDiag] {tag}: CheckWeapon({id})={wm.CheckWeapon(id)}");
                }
                catch (Exception ex)
                {
                    log.LogError($"[WeaponDiag] {tag}: CheckWeapon({id}) threw: {ex.Message}");
                }
            }
        }
    }
}
