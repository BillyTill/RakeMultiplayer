using System.Collections.Generic;
using UnityEngine;
using RakeMultiplayer.Networking;

namespace RakeMultiplayer
{
    /// <summary>
    /// Death, corpse and respawn flow for hunters.
    ///
    /// A fatal hit no longer runs the vanilla "Game over" + LoadLevel(0) path. Instead the
    /// victim stays in the ragdoll, the ragdoll is frozen after a few seconds and left in
    /// the world as a corpse, the inventory is scattered around it, and the server brings
    /// the player back at the safehouse after a short delay.
    /// </summary>
    public static class PlayerDeathSystem
    {
        /// <summary>Seconds a dead player waits before respawning.</summary>
        public static float RespawnDelay = 8f;

        /// <summary>Direction of the last damage, used to throw the death ragdoll.</summary>
        public static Vector3 LastHitDirection;

        private static readonly HashSet<int> _dead = new HashSet<int>();
        private static readonly Dictionary<int, List<Collider>> _disabledColliders =
            new Dictionary<int, List<Collider>>();

        public static bool IsDead(int playerId)
        {
            return playerId > 0 && _dead.Contains(playerId);
        }

        /// <summary>New scene: forget corpses and pending revives from the old level.</summary>
        public static void ResetForScene()
        {
            _dead.Clear();
            _disabledColliders.Clear();
            LastHitDirection = Vector3.zero;
        }

        public static void SetLastHitDirection(Vector3 direction)
        {
            LastHitDirection = direction;
        }

        /// <summary>
        /// Called from the KillPlayer prefix on the victim machine. Returns true when the
        /// mod handled the death (the vanilla restart must not run).
        /// </summary>
        public static bool BeginLocalDeath(FPSController fps)
        {
            if (fps == null) return false;

            var spawner = PlayerSpawner.Instance;
            var local = spawner != null ? spawner.LocalPlayer : null;
            if (local == null) return false;
            if (local.GetComponent<FPSController>() != fps) return false;
            if (local.PlayerId <= 0 || IsDead(local.PlayerId)) return false;

            int playerId = local.PlayerId;
            Vector3 position = fps.transform.position;
            Quaternion rotation = fps.transform.rotation;
            Vector3 direction = LastHitDirection;
            if (direction.sqrMagnitude < 0.01f) direction = fps.transform.forward;
            LastHitDirection = Vector3.zero;

            _dead.Add(playerId);
            local.SetDead(true);

            var weaponIds = new List<int>();
            var weaponAmmo = new List<int>();
            CollectInventory(fps, weaponIds, weaponAmmo);
            RemoveInventory(fps, weaponIds);

            PlayerRagdoll.TriggerDeathLocal(direction);
            DropItems(position, rotation, weaponIds.ToArray(), weaponAmmo.ToArray());
            DisablePlayerColliders(playerId, fps.transform);

            var packet = new PlayerDiedPacket
            {
                PlayerId = playerId,
                Position = position,
                Rotation = rotation,
                Direction = direction,
                WeaponIds = weaponIds.ToArray(),
                WeaponAmmo = weaponAmmo.ToArray()
            };

            var manager = RakeNetworkManager.Instance;
            if (RakeMultiplayerPlugin.isServer)
            {
                manager?.BroadcastPlayerDied(packet);
                manager?.SchedulePlayerRespawn(playerId);
            }
            else
            {
                manager?.ClientSend(packet.Serialize());
            }

            RakeMultiplayerPlugin.Log?.LogInfo("[Death] player " + playerId + " died at " +
                                               position.ToString("F1") + " with " +
                                               weaponIds.Count + " dropped item(s)");
            return true;
        }

        /// <summary>Build the corpse and scatter the items on every peer.</summary>
        public static void ApplyDeath(PlayerDiedPacket packet)
        {
            if (packet == null || packet.PlayerId <= 0) return;
            if (IsDead(packet.PlayerId)) return;

            int playerId = packet.PlayerId;
            _dead.Add(playerId);

            var spawner = PlayerSpawner.Instance;
            var local = spawner != null ? spawner.LocalPlayer : null;

            if (local != null && local.PlayerId == playerId)
            {
                local.SetDead(true);
                PlayerRagdoll.TriggerDeathLocal(packet.Direction);
                DisablePlayerColliders(playerId, local.transform);
            }
            else
            {
                NetworkedPlayer remote;
                if (spawner != null && spawner.TryGetRemotePlayer(playerId, out remote) && remote != null)
                {
                    remote.SetDead(true);
                    PlayerRagdoll.TriggerDeathRemote(playerId, remote.transform, packet.Direction);
                    DisablePlayerColliders(playerId, remote.transform);
                }
            }

            DropItems(packet.Position, packet.Rotation, packet.WeaponIds, packet.WeaponAmmo);
            RakeMultiplayerPlugin.Log?.LogInfo("[Death] applied for player " + playerId +
                                               " at " + packet.Position.ToString("F1"));
        }

        /// <summary>Restore a player after the server's respawn timer.</summary>
        public static void ApplyRespawn(PlayerRespawnPacket packet)
        {
            if (packet == null || packet.PlayerId <= 0) return;

            int playerId = packet.PlayerId;
            _dead.Remove(playerId);

            var spawner = PlayerSpawner.Instance;
            var local = spawner != null ? spawner.LocalPlayer : null;

            if (local != null && local.PlayerId == playerId)
            {
                var fps = local.GetComponent<FPSController>();
                if (fps != null)
                {
                    // Health must be positive before ReleaseRagdoll, otherwise the input
                    // lock assumes the player is still dead and keeps movement frozen.
                    fps.Health = 100;
                    fps.playerState = FPSController.Player_state.Full;
                    if (fps.cameraAnimator != null) fps.cameraAnimator.SetBool("Dead", false);
                    if (fps.effectAnimator != null) fps.effectAnimator.SetBool("Blood", false);
                    // FPSController.Update toggles these off while dead (inventory),
                    // and they were never re-enabled on respawn: the player could not
                    // move or look after coming back.
                    if (fps.playerMovement != null)
                    {
                        fps.playerMovement.enabled = true;
                        fps.playerMovement.bCanMove = true;
                    }
                    if (fps.camMouseLook != null)
                    {
                        fps.camMouseLook.enabled = true;
                        fps.camMouseLook.bControl = true;
                    }
                }

                PlayerRagdoll.ReviveLocal();
                local.SetDead(false);
                local.transform.position = packet.Position;
                local.transform.rotation = packet.Rotation;
                EnablePlayerColliders(playerId, local.transform);
                PlayerInputLock.ReleaseRagdoll();
            }
            else
            {
                NetworkedPlayer remote;
                if (spawner != null && spawner.TryGetRemotePlayer(playerId, out remote) && remote != null)
                {
                    PlayerRagdoll.ReviveRemote(playerId);
                    remote.SetDead(false);
                    remote.transform.position = packet.Position;
                    remote.transform.rotation = packet.Rotation;
                    EnablePlayerColliders(playerId, remote.transform);
                }
            }

            RakeMultiplayerPlugin.Log?.LogInfo("[Death] player " + playerId + " respawned at " +
                                               packet.Position.ToString("F1"));
        }

        /// <summary>Safehouse / spawn point for a respawning player.</summary>
        public static Vector3 GetRespawnPosition(int playerId)
        {
            Vector3 position;
            var settings = MapSettings.Current;

            if (settings != null && settings.hasSafehouse)
            {
                position = settings.safehousePosition;
            }
            else if (settings != null && settings.spawnPoints != null && settings.spawnPoints.Length > 0)
            {
                position = settings.spawnPoints[Mathf.Abs(playerId) % settings.spawnPoints.Length];
            }
            else
            {
                var spawner = PlayerSpawner.Instance;
                if (spawner != null && spawner.playerSpawnPoints != null && spawner.playerSpawnPoints.Length > 0)
                {
                    position = spawner.playerSpawnPoints[Mathf.Abs(playerId) % spawner.playerSpawnPoints.Length].position;
                }
                else
                {
                    var local = spawner != null ? spawner.LocalPlayer : null;
                    position = local != null ? local.transform.position : Vector3.zero;
                }
            }

            // Do not stack several corpses/respawns on one point.
            position += new Vector3(((playerId % 3) - 1) * 1.1f, 0.15f, (((playerId / 3) % 3) - 1) * 1.1f);

            NavMeshHit hit;
            if (NavMesh.SamplePosition(position, out hit, 12f, NavMesh.AllAreas))
            {
                position = hit.position;
            }
            return position;
        }

        // ---------------------------------------------------------------- inventory

        private static void CollectInventory(FPSController fps, List<int> ids, List<int> ammo)
        {
            var wm = fps.GetComponentInChildren<WeaponManager>();
            if (wm == null || wm.playerWeapons == null) return;

            for (int i = 0; i < wm.playerWeapons.Count; i++)
            {
                var transform = wm.playerWeapons[i];
                if (transform == null) continue;

                var weapon = transform.GetComponent<Weapon>();
                if (weapon == null || weapon.weaponID <= 0) continue;

                int count = 0;
                if (wm.weaponsAmmo != null && weapon.weaponID >= 0 && weapon.weaponID < wm.weaponsAmmo.Count)
                {
                    count = wm.weaponsAmmo[weapon.weaponID];
                }

                ids.Add(weapon.weaponID);
                ammo.Add(count);
            }
        }

        private static void RemoveInventory(FPSController fps, List<int> ids)
        {
            var wm = fps.GetComponentInChildren<WeaponManager>();
            if (wm == null) return;

            for (int i = 0; i < ids.Count; i++)
            {
                try { wm.RemoveWeapon(ids[i]); }
                catch { /* the list may already be empty */ }
            }

            // Hide whatever was in hand: the corpse carries the loot now.
            var weapons = fps.GetComponentsInChildren<Weapon>(true);
            for (int i = 0; i < weapons.Length; i++)
            {
                if (weapons[i] != null) weapons[i].gameObject.SetActive(false);
            }
        }

        // ---------------------------------------------------------------- drops

        private static System.Type ItemTypeForWeapon(int weaponId)
        {
            switch (weaponId)
            {
                case 1: return typeof(Rifle_item);
                case 2: return typeof(Camera_item);
                case 4: return typeof(Trap_item);
                case 5: return typeof(Food_item);
                case 6: return typeof(SniperRifle_item);
                case 8: return typeof(Medkit_item);
                default: return null;
            }
        }

        private static System.Type AmmoTypeForWeapon(int weaponId)
        {
            switch (weaponId)
            {
                case 1: return typeof(RifleAmmo_item);
                case 6: return typeof(SniperAmmo_item);
                default: return null;
            }
        }

        private static Item FindTemplate(System.Type type)
        {
            if (type == null) return null;

            var items = Object.FindObjectsOfType<Item>();
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] != null && items[i].GetType() == type) return items[i];
            }
            return null;
        }

        private static void DropItems(Vector3 position, Quaternion rotation, int[] ids, int[] ammo)
        {
            if (ids == null) return;

            int dropped = 0;
            for (int i = 0; i < ids.Length && dropped < 12; i++)
            {
                int weaponId = ids[i];
                int ammoCount = ammo != null && i < ammo.Length ? ammo[i] : 0;

                var type = ItemTypeForWeapon(weaponId);
                if (SpawnPickup(type, position, rotation)) dropped++;

                if (ammoCount > 0 && dropped < 12)
                {
                    if (SpawnPickup(AmmoTypeForWeapon(weaponId), position, rotation)) dropped++;
                }
            }
        }

        private static bool SpawnPickup(System.Type type, Vector3 position, Quaternion rotation)
        {
            var template = FindTemplate(type);
            if (template == null) return false;

            Vector3 offset = new Vector3(
                UnityEngine.Random.Range(-1.1f, 1.1f), 0.8f,
                UnityEngine.Random.Range(-1.1f, 1.1f));

            var go = Object.Instantiate(template.gameObject,
                position + offset,
                Quaternion.Euler(0f, rotation.eulerAngles.y + UnityEngine.Random.Range(-40f, 40f), 0f)) as GameObject;
            if (go == null) return false;

            go.name = "Dropped_" + type.Name;
            go.SetActive(true);

            var net = go.GetComponent<NetworkedItem>();
            if (net == null) net = go.AddComponent<NetworkedItem>();
            net.itemId = 0; // unregistered pickups use the local vanilla path
            net.isNetworked = false;

            var body = go.GetComponent<Rigidbody>();
            if (body == null) body = go.AddComponent<Rigidbody>();
            body.mass = 0.4f;
            body.drag = 0.35f;
            body.angularDrag = 0.8f;
            body.AddForce(new Vector3(
                UnityEngine.Random.Range(-1.6f, 1.6f),
                UnityEngine.Random.Range(1.8f, 3.0f),
                UnityEngine.Random.Range(-1.6f, 1.6f)), ForceMode.VelocityChange);
            body.AddTorque(UnityEngine.Random.insideUnitSphere * 2f, ForceMode.VelocityChange);

            RakeMultiplayerPlugin.Log?.LogInfo("[Death] dropped " + type.Name +
                                               " at " + (position + offset).ToString("F1"));
            return true;
        }

        // ---------------------------------------------------------------- colliders

        private static void DisablePlayerColliders(int playerId, Transform player)
        {
            if (player == null) return;

            List<Collider> stored;
            if (!_disabledColliders.TryGetValue(playerId, out stored))
            {
                stored = new List<Collider>();
                _disabledColliders[playerId] = stored;
            }
            stored.Clear();

            var colliders = player.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                if (collider == null || !collider.enabled) continue;
                collider.enabled = false;
                stored.Add(collider);
            }
        }

        private static void EnablePlayerColliders(int playerId, Transform player)
        {
            List<Collider> stored;
            if (!_disabledColliders.TryGetValue(playerId, out stored)) return;

            for (int i = 0; i < stored.Count; i++)
            {
                if (stored[i] != null) stored[i].enabled = true;
            }
            stored.Clear();
            _disabledColliders.Remove(playerId);
        }
    }
}
