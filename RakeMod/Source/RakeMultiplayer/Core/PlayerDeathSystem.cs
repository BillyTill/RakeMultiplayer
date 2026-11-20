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

        /// <summary>
        /// Deaths belong to the running match, so a new match cannot inherit them.
        /// Outside a match (menu, dev tools) the local set keeps the old behaviour.
        /// </summary>
        private static HashSet<int> DeadSet
        {
            get { return MatchState.IsActive ? MatchState.Current.DeadPlayers : _dead; }
        }
        private static readonly Dictionary<int, List<Collider>> _disabledColliders =
            new Dictionary<int, List<Collider>>();

        // A lethal hit that arrived before the death flow could run (level load, no local
        // player yet). Retried every frame instead of clamping HP to 1 (N18).
        private static FPSController _pendingLocalDeath;

        public static bool IsDead(int playerId)
        {
            return playerId > 0 && DeadSet.Contains(playerId);
        }

        /// <summary>New scene: forget corpses and pending revives from the old level.</summary>
        public static void ResetForScene()
        {
            _dead.Clear();
            if (MatchState.IsActive) MatchState.Current.DeadPlayers.Clear();
            _disabledColliders.Clear();
            LastHitDirection = Vector3.zero;
            _pendingLocalDeath = null;
        }

        public static void MarkPendingLocalDeath(FPSController fps)
        {
            if (fps != null) _pendingLocalDeath = fps;
        }

        /// <summary>Retry a lethal hit that could not start the death flow yet (N18).</summary>
        public static void TickPendingLocalDeath()
        {
            var fps = _pendingLocalDeath;
            if (fps == null)
            {
                _pendingLocalDeath = null;
                return;
            }

            if (BeginLocalDeath(fps))
            {
                _pendingLocalDeath = null;
            }
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

            DeadSet.Add(playerId);
            local.SetDead(true);

            var weaponIds = new List<int>();
            var weaponAmmo = new List<int>();
            CollectInventory(fps, weaponIds, weaponAmmo);
            RemoveInventory(fps, weaponIds);

            PlayerRagdoll.TriggerDeathLocal(direction);
            if (RakeMultiplayerPlugin.isServer)
            {
                DropItems(position, rotation, weaponIds.ToArray(), weaponAmmo.ToArray());
            }
            DisablePlayerColliders(playerId, fps.transform);

            var packet = new PlayerDiedPacket
            {
                PlayerId = playerId,
                Position = position,
                Rotation = rotation,
                Direction = direction,
                WeaponIds = weaponIds.ToArray(),
                WeaponAmmo = weaponAmmo.ToArray(),
                Health = Mathf.Max(0, fps.Health)
            };

            var manager = RakeNetworkManager.Instance;
            if (RakeMultiplayerPlugin.isServer)
            {
                manager?.BroadcastPlayerDied(packet);
                manager?.SchedulePlayerRespawn(playerId);

                // The host never receives its own PlayerDied packet, so the "all hunters
                // down" check was only evaluated when a client was the last to die.
                SessionManager.Instance?.NotifyHunterDied(playerId);
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
            DeadSet.Add(playerId);

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

            // Loot is spawned only by the server and replicated with shared ids.
            if (RakeMultiplayerPlugin.isServer)
            {
                DropItems(packet.Position, packet.Rotation, packet.WeaponIds, packet.WeaponAmmo);
            }
            RakeMultiplayerPlugin.Log?.LogInfo("[Death] applied for player " + playerId +
                                               " at " + packet.Position.ToString("F1"));

            if (RakeMultiplayerPlugin.isServer)
            {
                SessionManager.Instance?.NotifyHunterDied(playerId);
            }
        }

        /// <summary>Restore a player after the server's respawn timer.</summary>
        public static void ApplyRespawn(PlayerRespawnPacket packet)
        {
            if (packet == null || packet.PlayerId <= 0) return;

            int playerId = packet.PlayerId;
            DeadSet.Remove(playerId);

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

                        // The ragdoll leaves the view wherever the body died - often
                        // staring at the ground. Level it out on respawn.
                        fps.camMouseLook.m_CameraTargetRot = Quaternion.identity;
                        if (fps.camMouseLook.camera != null)
                        {
                            fps.camMouseLook.camera.localRotation = Quaternion.identity;
                        }
                    }

                    // If the ragdoll was destroyed before it could restore the cameras
                    // (level change mid-death), turn the view cameras back on here.
                    fps.CameraRander(true);
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

        /// <summary>Respawns must not drop a hunter into the Rake's lap (I-DEATH-4).</summary>
        private const float MinRespawnDistanceFromRake = 25f;

        /// <summary>Safehouse / spawn point for a respawning player.</summary>
        public static Vector3 GetRespawnPosition(int playerId)
        {
            // Every configured point is a candidate; the Rake's position then decides which
            // one is actually safe.
            var candidates = new List<Vector3>();
            var settings = MapSettings.Current;

            if (settings != null && settings.hasSafehouse)
            {
                candidates.Add(settings.safehousePosition);
            }

            if (settings != null && settings.spawnPoints != null)
            {
                for (int i = 0; i < settings.spawnPoints.Length; i++) candidates.Add(settings.spawnPoints[i]);
            }

            var spawner = PlayerSpawner.Instance;
            if (spawner != null && spawner.playerSpawnPoints != null)
            {
                for (int i = 0; i < spawner.playerSpawnPoints.Length; i++)
                {
                    if (spawner.playerSpawnPoints[i] != null)
                    {
                        candidates.Add(spawner.playerSpawnPoints[i].position);
                    }
                }
            }

            if (candidates.Count == 0)
            {
                var local = spawner != null ? spawner.LocalPlayer : null;
                candidates.Add(local != null ? local.transform.position : Vector3.zero);
            }

            int index = Mathf.Abs(playerId) % candidates.Count;
            Vector3 position = candidates[index];

            Vector3 rakePosition;
            bool hasRake = TryGetRakePosition(out rakePosition);

            if (hasRake)
            {
                // Prefer a point that is already far enough from the Rake; if none is,
                // take the farthest one and push it away from him.
                float best = Vector3.Distance(position, rakePosition);

                for (int i = 0; i < candidates.Count; i++)
                {
                    float distance = Vector3.Distance(candidates[i], rakePosition);
                    if (distance >= MinRespawnDistanceFromRake)
                    {
                        position = candidates[i];
                        best = distance;
                        break;
                    }

                    if (distance > best)
                    {
                        best = distance;
                        position = candidates[i];
                    }
                }

                if (best < MinRespawnDistanceFromRake)
                {
                    position = PushAwayFrom(rakePosition, position, MinRespawnDistanceFromRake + 5f);
                }
            }

            // Do not stack several corpses/respawns on one point.
            position += new Vector3(((playerId % 3) - 1) * 1.1f, 0.15f, (((playerId / 3) % 3) - 1) * 1.1f);

            NavMeshHit hit;
            if (NavMesh.SamplePosition(position, out hit, 12f, NavMesh.AllAreas))
            {
                position = hit.position;
            }

            // Snapping to the NavMesh may have pulled the point back towards the Rake; the
            // full 25 m gap is the contract, not 60% of it (N16).
            if (hasRake && Vector3.Distance(position, rakePosition) < MinRespawnDistanceFromRake)
            {
                Vector3 pushed = PushAwayFrom(rakePosition, position, MinRespawnDistanceFromRake + 5f);
                NavMeshHit safeHit;
                if (NavMesh.SamplePosition(pushed, out safeHit, 20f, NavMesh.AllAreas))
                {
                    position = safeHit.position;
                }
                else
                {
                    position = pushed;
                }
            }

            return position;
        }

        /// <summary>A point at least <paramref name="distance"/> metres away from the Rake.</summary>
        private static Vector3 PushAwayFrom(Vector3 rakePosition, Vector3 position, float distance)
        {
            Vector3 away = position - rakePosition;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = Vector3.forward;

            Vector3 pushed = rakePosition + away.normalized * distance;
            pushed.y = position.y;
            return pushed;
        }

        private static bool TryGetRakePosition(out Vector3 position)
        {
            position = Vector3.zero;

            var rake = UnityEngine.Object.FindObjectOfType<NetworkedRakeAI>();
            if (rake == null) return false;

            position = rake.transform.position;
            return true;
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
                case 3: return typeof(TabletComputer_item);
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
            if (!RakeMultiplayerPlugin.isServer || ids == null) return;

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

        /// <summary>
        /// Server: spawn one item, register it with a shared network id and tell every peer.
        /// Clients only instantiate the ItemSpawn packet, so loot exists once per peer.
        /// </summary>
        private static bool SpawnPickup(System.Type type, Vector3 position, Quaternion rotation)
        {
            var template = FindTemplate(type);
            if (template == null) return false;

            Vector3 offset = new Vector3(
                UnityEngine.Random.Range(-1.1f, 1.1f), 0.8f,
                UnityEngine.Random.Range(-1.1f, 1.1f));
            Vector3 dropPosition = position + offset;
            Quaternion dropRotation = Quaternion.Euler(
                0f, rotation.eulerAngles.y + UnityEngine.Random.Range(-40f, 40f), 0f);

            var go = Object.Instantiate(template.gameObject, dropPosition, dropRotation) as GameObject;
            if (go == null) return false;

            go.name = "Dropped_" + type.Name;
            go.SetActive(true);

            var net = go.GetComponent<NetworkedItem>();
            if (net == null) net = go.AddComponent<NetworkedItem>();
            net.isNetworked = false;
            net.itemId = 0;

            var item = go.GetComponent<Item>();
            var manager = RakeNetworkManager.Instance;
            int networkId = manager != null ? manager.RegisterDroppedItem(item) : 0;
            if (networkId > 0)
            {
                net.itemId = networkId;
                net.isNetworked = true;
            }

            var body = go.GetComponent<Rigidbody>();
            if (body == null) body = go.AddComponent<Rigidbody>();
            body.mass = 0.4f;
            body.drag = 0.35f;
            body.angularDrag = 0.8f;
            // No random impulse: every peer gets the same spawn transform and the item
            // must not slide to a different place on each machine.

            if (manager != null && networkId > 0)
            {
                manager.ServerBroadcast(new ItemSpawnPacket
                {
                    ItemNetworkId = networkId,
                    ItemTypeName = type.Name,
                    Position = dropPosition,
                    Rotation = dropRotation
                }.Serialize());
            }

            RakeMultiplayerPlugin.Log?.LogInfo("[Death] dropped " + type.Name + " id=" + networkId +
                                               " at " + dropPosition.ToString("F1"));
            return true;
        }

        /// <summary>Client: create the loot item the server announced.</summary>
        public static void SpawnNetworkedDrop(ItemSpawnPacket packet)
        {
            if (packet == null || packet.ItemNetworkId <= 0) return;

            var type = ItemTypeByName(packet.ItemTypeName);
            var template = FindTemplate(type);
            if (template == null)
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[Death] no template for networked drop " +
                                                      packet.ItemTypeName);
                return;
            }

            var go = Object.Instantiate(template.gameObject, packet.Position, packet.Rotation) as GameObject;
            if (go == null) return;

            go.name = "Dropped_" + packet.ItemTypeName;
            go.SetActive(true);

            var net = go.GetComponent<NetworkedItem>();
            if (net == null) net = go.AddComponent<NetworkedItem>();
            net.itemId = packet.ItemNetworkId;
            net.isNetworked = true;

            var item = go.GetComponent<Item>();
            var manager = RakeNetworkManager.Instance;
            if (item != null && manager != null)
            {
                manager.RegisterItem(item, packet.ItemNetworkId);
            }

            var body = go.GetComponent<Rigidbody>();
            if (body == null) body = go.AddComponent<Rigidbody>();
            body.mass = 0.4f;
            body.drag = 0.35f;
            body.angularDrag = 0.8f;

            RakeMultiplayerPlugin.Log?.LogInfo("[Death] spawned networked drop " +
                                               packet.ItemTypeName + " id=" + packet.ItemNetworkId);
        }

        private static System.Type ItemTypeByName(string name)
        {
            switch (name)
            {
                case "Rifle_item": return typeof(Rifle_item);
                case "Camera_item": return typeof(Camera_item);
                case "Trap_item": return typeof(Trap_item);
                case "Food_item": return typeof(Food_item);
                case "SniperRifle_item": return typeof(SniperRifle_item);
                case "Medkit_item": return typeof(Medkit_item);
                case "RifleAmmo_item": return typeof(RifleAmmo_item);
                case "SniperAmmo_item": return typeof(SniperAmmo_item);
                default: return null;
            }
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
