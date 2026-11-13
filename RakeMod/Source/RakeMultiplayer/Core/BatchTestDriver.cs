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

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            _enabled = Array.IndexOf(Environment.GetCommandLineArgs(), "-autotest") >= 0;
        }

        private void Start()
        {
            if (_enabled) StartCoroutine(Run());
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

            yield return new WaitForSeconds(2f);
            RakeMultiplayerPlugin.Log?.LogInfo("[AutoTest] done");
            Application.Quit();
        }
    }
}
