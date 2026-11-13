using UnityEngine;

namespace RakeMultiplayer
{
    /// <summary>
    /// Server-authoritative bullet ballistics. Every shot spawns a real projectile that
    /// travels with velocity and gravity and applies its damage only on impact, so moving
    /// targets, lead and bullet drop work naturally. Clients never apply bullet damage;
    /// they only simulate the visual tracer.
    /// </summary>
    public static class Ballistics
    {
        /// <summary>Muzzle velocity (m/s).</summary>
        public const float Speed = 260f;

        /// <summary>Gravity applied to the bullet (m/s^2). Set to 0 for flat shots.</summary>
        public const float Gravity = 9.81f;

        public const float MaxLifetime = 5f;
    }

    public static class ProjectileSystem
    {
        /// <summary>Set while the authoritative projectile applies its hit, so the
        /// vanilla instant raycast damage can be suppressed without blocking it.</summary>
        public static bool ApplyingImpact;

        private static GameObject _root;

        /// <summary>Spawn one projectile, or a shotgun cone of pellets.</summary>
        public static void SpawnShot(Vector3 origin, Vector3 direction, int pellets, float spreadDegrees,
                                     int amount, string damageType, int ownerId)
        {
            pellets = Mathf.Clamp(pellets, 1, 12);
            if (pellets <= 1)
            {
                SpawnServerProjectile(origin, direction, amount, damageType, ownerId);
                return;
            }

            if (direction.sqrMagnitude < 0.0001f) return;
            direction.Normalize();

            Vector3 right = Vector3.Cross(Vector3.up, direction);
            if (right.sqrMagnitude < 0.001f) right = Vector3.Cross(Vector3.forward, direction);
            right.Normalize();
            Vector3 up = Vector3.Cross(direction, right).normalized;

            float radius = Mathf.Tan(Mathf.Clamp(spreadDegrees, 0f, 10f) * Mathf.Deg2Rad);
            for (int i = 0; i < pellets; i++)
            {
                float x = UnityEngine.Random.Range(-radius, radius);
                float y = UnityEngine.Random.Range(-radius, radius);
                Vector3 pelletDirection = (direction + right * x + up * y).normalized;
                SpawnServerProjectile(origin, pelletDirection, amount, damageType, ownerId);
            }
        }
        public static void SpawnServerProjectile(Vector3 origin, Vector3 direction,
                                                 int amount, string damageType, int ownerId)
        {
            if (direction.sqrMagnitude < 0.0001f) return;

            if (_root == null)
            {
                _root = new GameObject("RakeProjectiles");
                Object.DontDestroyOnLoad(_root);
            }

            var go = new GameObject("ServerProjectile");
            go.transform.SetParent(_root.transform, false);

            var projectile = go.AddComponent<ServerProjectile>();
            projectile.Init(origin, direction.normalized, amount, damageType, ownerId);

            RakeMultiplayerPlugin.Log?.LogInfo("[Ballistics] shot from " + origin.ToString("F1") +
                                               " owner=" + ownerId + " dmg=" + amount);
        }
    }

    public sealed class ServerProjectile : MonoBehaviour
    {
        private Vector3 _position;
        private Vector3 _velocity;
        private int _amount;
        private string _damageType;
        private int _ownerId;
        private float _life;

        public void Init(Vector3 origin, Vector3 direction, int amount, string damageType, int ownerId)
        {
            _position = origin;
            _velocity = direction * Ballistics.Speed;
            _amount = Mathf.Clamp(amount, 1, 300);
            _damageType = string.IsNullOrEmpty(damageType) ? "SniperShot" : damageType;
            _ownerId = ownerId;
            transform.position = origin;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            _life += dt;
            if (_life > Ballistics.MaxLifetime)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 previous = _position;
            _velocity += Vector3.up * (-Ballistics.Gravity * dt);
            Vector3 next = previous + _velocity * dt;

            Vector3 segment = next - previous;
            float distance = segment.magnitude;
            if (distance < 0.0001f)
            {
                _position = next;
                transform.position = next;
                return;
            }

            Vector3 direction = segment / distance;
            // ~0 includes the players: their CharacterController lives on the built-in
            // "Ignore Raycast" layer (2), which DefaultRaycastLayers excludes. A thin
            // sphere cast also prevents fast bullets from tunnelling through a body.
            RaycastHit[] hits = Physics.SphereCastAll(previous, 0.06f, direction, distance, ~0);

            float best = distance;
            bool found = false;
            RaycastHit bestHit = new RaycastHit();

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null || hit.collider.isTrigger) continue;

                if (IsOwnerCollider(hit.collider)) continue;
                if (hit.distance >= best) continue;

                best = hit.distance;
                bestHit = hit;
                found = true;
            }

            if (found)
            {
                Impact(bestHit);
                Destroy(gameObject);
                return;
            }

            _position = next;
            transform.position = next;
        }

        private bool IsOwnerCollider(Collider collider)
        {
            if (_ownerId <= 0) return false;

            var net = collider.GetComponentInParent<NetworkedPlayer>();
            return net != null && net.PlayerId == _ownerId;
        }

        private void Impact(RaycastHit hit)
        {
            if (hit.collider == null) return;

            RakeMultiplayerPlugin.Log?.LogInfo("[Ballistics] impact " + hit.collider.name + " trigger=" + hit.collider.isTrigger + " enemy=" + (hit.collider.GetComponentInParent<Enemy_AI>() != null) + " unit=" + (hit.collider.GetComponentInParent<Unit_obj>() != null) + " at " + hit.point.ToString("F1"));

            var rake = hit.collider.GetComponentInParent<Enemy_AI>();
            if (rake != null)
            {
                var rakeAI = rake.GetComponent<NetworkedRakeAI>();

                int damage = _amount;
                if (rakeAI != null)
                {
                    Vector3 hitDirection = (rake.transform.position - _position).normalized;
                    float reduction = rakeAI.CalculateDamageReduction(hitDirection, rake.transform.forward);
                    damage = Mathf.RoundToInt(_amount * reduction);
                    rakeAI.OnDamageTaken(hitDirection, _amount, _damageType);
                }

                RakeMultiplayerPlugin.Log?.LogInfo("[Ballistics] hit the Rake for " + damage +
                                                   " (" + _damageType + ")");

                ProjectileSystem.ApplyingImpact = true;
                try
                {
                    rake.TakeDamage(damage, _damageType);
                }
                finally
                {
                    ProjectileSystem.ApplyingImpact = false;
                }

                RakeNetworkManager.Instance?.BroadcastRakeHealth();
                return;
            }

            // PvP: a bullet hit another hunter. The server applies the damage on the
            // victim and broadcasts a ragdoll with the impulse along the shot.
            var player = hit.collider.GetComponentInParent<NetworkedPlayer>();
            if (player != null && player.PlayerId != _ownerId && player.PlayerId > 0)
            {
                if (player.IsDead) return;

                Vector3 away = _velocity.sqrMagnitude > 0.01f
                    ? _velocity.normalized
                    : (hit.point - _position).normalized;

                RakeNetworkManager.Instance?.ServerPlayerHit(player.PlayerId, _amount,
                    _damageType, _position, away);
                return;
            }

            var window = hit.collider.GetComponentInParent<Window_obj>();
            if (window != null)
            {
                window.TakeDamage(_amount, _damageType);
                return;
            }

            var unit = hit.collider.GetComponentInParent<Unit_obj>();
            if (unit != null)
            {
                unit.TakeDamage(_amount, _damageType);
            }
        }
    }
}
