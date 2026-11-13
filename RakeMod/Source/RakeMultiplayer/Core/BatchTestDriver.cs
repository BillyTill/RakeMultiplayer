using System;
using System.Collections;
using UnityEngine;

namespace RakeMultiplayer
{
    /// <summary>
    /// Headless smoke test used while developing: `Rake.exe -batchmode -nographics -autotest`.
    /// Hosts a LAN match, loads level 1, builds the floating test arena and fires one
    /// visual tracer, then quits. It exists only when the command line says so.
    /// </summary>
    public class BatchTestDriver : MonoBehaviour
    {
        private bool _enabled;
        private bool _aiTest;
        private bool _deathTest;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            var args = Environment.GetCommandLineArgs();
            _enabled = Array.IndexOf(args, "-autotest") >= 0;
            _aiTest = Array.IndexOf(args, "-aitest") >= 0;
            _deathTest = Array.IndexOf(args, "-deathtest") >= 0;
        }

        private void Start()
        {
            if (_enabled) StartCoroutine(Run());
            else if (_aiTest) StartCoroutine(RunAiTest());
            else if (_deathTest) StartCoroutine(RunDeathTest());
        }

        private float _heart;
        private void Update()
        {
            if (!_enabled) return;
            _heart += Time.unscaledDeltaTime;
            if (_heart < 4f) return;
            _heart = 0f;
            RakeMultiplayerPlugin.Log?.LogInfo("[AutoTest] alive t=" + Time.realtimeSinceStartup.ToString("F1"));
        }

        private IEnumerator Run()
        {
            RakeMultiplayerPlugin.Log?.LogInfo("[AutoTest] starting");

            yield return new WaitForSeconds(3f);

            var menu = FindObjectOfType<UI.MultiplayerMenu>();
            if (menu != null)
            {
                menu.OnHostGame();
                RakeMultiplayerPlugin.Log?.LogInfo("[AutoTest] host started");
            }
            else
            {
                RakeMultiplayerPlugin.Log?.LogError("[AutoTest] menu not found");
            }

            yield return new WaitForSeconds(1.5f);

            if (menu != null) menu.OnStartGame();
            RakeMultiplayerPlugin.Log?.LogInfo("[AutoTest] start-game sent");

            RakeMultiplayerPlugin.Log?.LogInfo("[AutoTest] waiting for the level");
            yield return new WaitForSeconds(14f);
            RakeMultiplayerPlugin.Log?.LogInfo("[AutoTest] building arena now");

            TestArena.CreateOnHost();
            RakeMultiplayerPlugin.Log?.LogInfo("[AutoTest] arena requested");

            yield return new WaitForSeconds(1.5f);

            var spawner = PlayerSpawner.Instance;
            var local = spawner != null ? spawner.LocalPlayer : null;
            if (local != null)
            {
                BulletVisuals.Spawn(local.transform.position + Vector3.up * 1.5f,
                                    local.transform.forward);
                RakeMultiplayerPlugin.Log?.LogInfo("[AutoTest] tracer spawned from " +
                                                   local.transform.position.ToString("F1"));
            }
            else
            {
                RakeMultiplayerPlugin.Log?.LogWarning("[AutoTest] no local player for the tracer");
            }

            // Authoritative projectile damage check against the AFK Rake.
            var rake = RakeHighlight.FindRake();
            Enemy_AI rakeEnemy = rake != null ? rake.GetComponent<Enemy_AI>() : null;
            float before = rakeEnemy != null ? rakeEnemy.Health : -1f;



            if (rake != null && local != null && rakeEnemy != null)
            {
                RakeMultiplayerPlugin.Log?.LogInfo("[AutoTest] rake pos=" + rake.position.ToString("F1") + " local=" + local.transform.position.ToString("F1"));

                Vector3 origin = local.transform.position + Vector3.up * 1.5f;
                Vector3 target = rake.position + Vector3.up * 1.2f;
                ProjectileSystem.SpawnServerProjectile(origin, (target - origin).normalized,
                    100, "SniperShot", 1);
                RakeMultiplayerPlugin.Log?.LogInfo("[AutoTest] projectile fired at the Rake");
            }

            yield return new WaitForSeconds(1.5f);

            float after = rakeEnemy != null ? rakeEnemy.Health : -1f;
            RakeMultiplayerPlugin.Log?.LogInfo("[AutoTest] projectile damage: before=" + before +
                                               " after=" + after);

            // Shotgun pellet check: 6 pellets x 17 damage with a 4.5 degree cone.
            if (rake != null && rakeEnemy != null && local != null)
            {
                float pelletBefore = rakeEnemy.Health;
                Vector3 origin2 = local.transform.position + Vector3.up * 1.5f;
                Vector3 target2 = rake.position + Vector3.up * 1.2f;
                ProjectileSystem.SpawnShot(origin2, (target2 - origin2).normalized, 6, 4.5f, 17, "Bullet", 1);
                yield return new WaitForSeconds(1.5f);
                RakeMultiplayerPlugin.Log?.LogInfo("[AutoTest] shotgun pellets: before=" + pelletBefore + " after=" + rakeEnemy.Health);
            }

            yield return new WaitForSeconds(2f);
            RakeMultiplayerPlugin.Log?.LogInfo("[AutoTest] done");
            Application.Quit();
        }

        /// <summary>
        /// Headless death check: host, build the arena, kill the host with a PvP bullet,
        /// log the corpse and wait for the server respawn. Run with -deathtest.
        /// </summary>
        private IEnumerator RunDeathTest()
        {
            RakeMultiplayerPlugin.Log?.LogInfo("[DeathTest] starting");
            yield return new WaitForSeconds(3f);

            var menu = FindObjectOfType<UI.MultiplayerMenu>();
            if (menu != null) menu.OnHostGame();
            yield return new WaitForSeconds(1.5f);
            if (menu != null) menu.OnStartGame();

            yield return new WaitForSeconds(14f);
            TestArena.CreateOnHost();
            yield return new WaitForSeconds(1.5f);

            var rakeForReport = RakeHighlight.FindRake();
            var aiForReport = rakeForReport != null ? rakeForReport.GetComponent<NetworkedRakeAI>() : null;
            if (aiForReport != null)
            {
                RakeMultiplayerPlugin.Log?.LogInfo("[DeathTest] AI report:\n" + aiForReport.GetAiStatusReport());
            }

            var spawner = PlayerSpawner.Instance;
            var local = spawner != null ? spawner.LocalPlayer : null;
            if (local == null)
            {
                RakeMultiplayerPlugin.Log?.LogError("[DeathTest] no local player");
                Application.Quit();
                yield break;
            }

            var fps = local.GetComponent<FPSController>();
            RakeMultiplayerPlugin.Log?.LogInfo("[DeathTest] before: health=" +
                (fps != null ? fps.Health.ToString() : "?") +
                " id=" + local.PlayerId +
                " pos=" + local.transform.position.ToString("F1"));

            var cc = local.GetComponent<CharacterController>();
            if (cc != null)
            {
                RakeMultiplayerPlugin.Log?.LogInfo("[DeathTest] controller center=" + cc.center +
                    " height=" + cc.height + " radius=" + cc.radius +
                    " step=" + cc.stepOffset);
            }

            var playerColliders = local.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < playerColliders.Length; i++)
            {
                var c = playerColliders[i];
                RakeMultiplayerPlugin.Log?.LogInfo("[DeathTest] collider " + c.GetType().Name +
                    " on " + c.name + " trigger=" + c.isTrigger + " enabled=" + c.enabled +
                    " layer=" + c.gameObject.layer);
            }

            // Give the hunter a rifle so the loot drop has a real pickup template.
            var wm = local.GetComponentInChildren<WeaponManager>();
            if (wm != null && !wm.CheckWeapon(1)) wm.AddWeapon(1, 2, true);

            Vector3 origin = local.transform.position + Vector3.up * 1.5f + local.transform.forward * 8f;
            Vector3 direction = (local.transform.position + Vector3.up * 0.6f - origin).normalized;

            // 1) A non-lethal PvP hit: the victim must ragdoll with the shot impulse.
            ProjectileSystem.SpawnServerProjectile(origin, direction, 10, "SniperShot", 2);
            yield return new WaitForSeconds(1.2f);
            RakeMultiplayerPlugin.Log?.LogInfo("[DeathTest] non-lethal: health=" +
                (fps != null ? fps.Health.ToString() : "?") +
                " ragdollActive=" + PlayerRagdoll.IsActive);
            yield return new WaitForSeconds(3.4f);
            RakeMultiplayerPlugin.Log?.LogInfo("[DeathTest] non-lethal restored: ragdollActive=" +
                PlayerRagdoll.IsActive);

            // 2) A lethal PvP bullet: permanent corpse, loot, respawn, no LoadLevel(0).
            ProjectileSystem.SpawnServerProjectile(origin, direction, 200, "SniperShot", 2);

            yield return new WaitForSeconds(1.5f);
            RakeMultiplayerPlugin.Log?.LogInfo("[DeathTest] after hit: health=" +
                (fps != null ? fps.Health.ToString() : "?") +
                " dead=" + PlayerDeathSystem.IsDead(local.PlayerId) +
                " ragdolls=" + UnityEngine.Object.FindObjectsOfType<PlayerRagdoll>().Length +
                " pos=" + local.transform.position.ToString("F1"));

            yield return new WaitForSeconds(5f);
            var ragdolls = UnityEngine.Object.FindObjectsOfType<PlayerRagdoll>();
            int frozen = 0;
            for (int i = 0; i < ragdolls.Length; i++) if (ragdolls[i].IsCorpseFrozen) frozen++;
            RakeMultiplayerPlugin.Log?.LogInfo("[DeathTest] t+6.5: ragdolls=" + ragdolls.Length +
                                               " frozen=" + frozen);

            yield return new WaitForSeconds(9f);
            RakeMultiplayerPlugin.Log?.LogInfo("[DeathTest] after respawn: health=" +
                (fps != null ? fps.Health.ToString() : "?") +
                " dead=" + PlayerDeathSystem.IsDead(local.PlayerId) +
                " pos=" + local.transform.position.ToString("F1") +
                " ragdolls=" + UnityEngine.Object.FindObjectsOfType<PlayerRagdoll>().Length);

            yield return new WaitForSeconds(2f);
            RakeMultiplayerPlugin.Log?.LogInfo("[DeathTest] done, scene=" + Application.loadedLevelName);
            Application.Quit();
        }

        /// <summary>
        /// Headless stalk-cycle check: host a match, move the hunter 120 m away,
        /// force a lurk and log the state until the Rake closes in and charges.
        /// Run with -aitest.
        /// </summary>
        private IEnumerator RunAiTest()
        {
            RakeMultiplayerPlugin.Log?.LogInfo("[AITest] starting");
            yield return new WaitForSeconds(3f);

            var menu = FindObjectOfType<UI.MultiplayerMenu>();
            if (menu != null) menu.OnHostGame();
            yield return new WaitForSeconds(1.5f);
            if (menu != null) menu.OnStartGame();

            yield return new WaitForSeconds(14f);

            var rake = RakeHighlight.FindRake();
            var ai = rake != null ? rake.GetComponent<NetworkedRakeAI>() : null;
            var spawner = PlayerSpawner.Instance;
            var local = spawner != null ? spawner.LocalPlayer : null;
            if (rake == null || ai == null || local == null)
            {
                RakeMultiplayerPlugin.Log?.LogError("[AITest] no rake/AI/local player");
                Application.Quit();
                yield break;
            }

            var manager = RakeNetworkManager.Instance;
            Vector3 rakePos = rake.position;
            Vector3 desired = rakePos + new Vector3(0f, 0f, 120f);
            NavMeshHit hit;
            if (NavMesh.SamplePosition(desired, out hit, 45f, NavMesh.AllAreas)) desired = hit.position;

            // Face away from the Rake so being "seen" does not interfere with the lurk.
            Quaternion away = Quaternion.LookRotation(desired - rakePos);
            if (manager != null) manager.ServerTeleportPlayer(1, desired, away);
            else local.transform.position = desired;

            RakeMultiplayerPlugin.Log?.LogInfo("[AITest] player moved to " + desired.ToString("F1") +
                                               " rake at " + rakePos.ToString("F1"));
            yield return new WaitForSeconds(1f);

            ai.ForceLurk();
            for (int i = 0; i < 20; i++)
            {
                yield return new WaitForSeconds(1f);
                RakeMultiplayerPlugin.Log?.LogInfo("[AITest] " + ai.GetAiDebugInfo() +
                                                   " rakePos=" + rake.position.ToString("F1"));
            }

            // Look straight at the watching Rake: expect Lurk -> Flee.
            var look = local.GetComponent<CameraMouseLook>();
            if (look != null) look.enabled = false;
            var cam = Camera.main;
            Vector3 aimPoint = rake.position + Vector3.up * 1.2f;
            local.transform.rotation = Quaternion.LookRotation(aimPoint - local.transform.position);
            if (look != null && look.camera != null)
            {
                look.camera.rotation = Quaternion.LookRotation(aimPoint - look.camera.position);
            }
            if (cam != null)
            {
                cam.transform.rotation = Quaternion.LookRotation(aimPoint - cam.transform.position);
            }
            RakeMultiplayerPlugin.Log?.LogInfo("[AITest] now looking at the Rake");
            for (int i = 0; i < 10; i++)
            {
                yield return new WaitForSeconds(1f);
                RakeMultiplayerPlugin.Log?.LogInfo("[AITest] " + ai.GetAiDebugInfo() +
                                                   " rakePos=" + rake.position.ToString("F1"));
            }

            // Put the hunter at melee range and force the charge: this exercises the
            // wind-up, the single strike and the recovery pause.
            Vector3 toRake = rake.position - desired;
            toRake.y = 0f;
            toRake.Normalize();
            Vector3 near = rake.position - toRake * 2f;
            if (NavMesh.SamplePosition(near, out hit, 20f, NavMesh.AllAreas)) near = hit.position;
            if (manager != null)
                manager.ServerTeleportPlayer(1, near, Quaternion.LookRotation(rake.position - near));
            else
                local.transform.position = near;

            RakeMultiplayerPlugin.Log?.LogInfo("[AITest] player moved to melee range: " + near.ToString("F1"));
            yield return new WaitForSeconds(1f);
            ai.ForceAttack();

            for (int i = 0; i < 12; i++)
            {
                yield return new WaitForSeconds(1f);
                RakeMultiplayerPlugin.Log?.LogInfo("[AITest] " + ai.GetAiDebugInfo() +
                                                   " rakePos=" + rake.position.ToString("F1"));
            }

            // Escape: the charge is lost, the encounter must finish and the next task
            // must be scheduled 60-90 s later (not at the start of the task).
            Vector3 runAway = local.transform.position - rake.position;
            runAway.y = 0f;
            runAway.Normalize();
            Vector3 far = rake.position + runAway * 130f;
            if (NavMesh.SamplePosition(far, out hit, 45f, NavMesh.AllAreas)) far = hit.position;
            if (manager != null)
                manager.ServerTeleportPlayer(1, far, Quaternion.LookRotation(rake.position - far));
            else
                local.transform.position = far;

            RakeMultiplayerPlugin.Log?.LogInfo("[AITest] player escaped to " + far.ToString("F1"));
            yield return new WaitForSeconds(7f);
            RakeMultiplayerPlugin.Log?.LogInfo("[AITest] after escape: " + ai.GetAiDebugInfo());

            RakeMultiplayerPlugin.Log?.LogInfo("[AITest] done");
            Application.Quit();
        }
    }
}
