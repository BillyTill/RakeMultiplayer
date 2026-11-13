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
            RaycastHit[] hits = Physics.RaycastAll(previous, direction, distance);

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
