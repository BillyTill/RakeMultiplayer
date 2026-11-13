using UnityEngine;

namespace RakeMultiplayer
{
    /// <summary>
    /// Visible bullet for every shot: a glowing head and a tracer that follow the same
    /// ballistic path as the authoritative server projectile (speed + gravity), stopping
    /// at whatever the bullet hits. Damage is applied separately by ServerProjectile.
    /// </summary>
    public static class BulletVisuals
    {
        public static void Spawn(Vector3 origin, Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.0001f) return;
            direction.Normalize();

            SpawnTracer(origin, direction);
            SpawnMuzzleLight(origin);
        }

        internal static bool IsLocalPlayerCollider(Collider collider)
        {
            var spawner = PlayerSpawner.Instance;
            if (spawner == null || spawner.LocalPlayer == null) return false;

            Transform local = spawner.LocalPlayer.transform;
            if (collider.transform == local || collider.transform.IsChildOf(local)) return true;

            var net = collider.GetComponentInParent<NetworkedPlayer>();
            return net != null && net.isLocalPlayer;
        }

        private static void SpawnTracer(Vector3 origin, Vector3 direction)
        {
            var go = new GameObject("RakeBulletTracer");
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.SetVertexCount(2);
            line.SetWidth(0.10f, 0.03f);

            Shader lineShader = Shader.Find("Sprites/Default");
            if (lineShader == null) lineShader = Shader.Find("Diffuse");
            if (lineShader == null) lineShader = Shader.Find("Unlit/Color");
            if (lineShader == null)
            {
                Object.Destroy(go);
                return;
            }

            var material = new Material(lineShader);
            material.name = "RakeTracer";
            line.material = material;
            line.SetColors(new Color(1f, 0.92f, 0.55f, 1f), new Color(1f, 0.45f, 0.10f, 0.85f));

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "RakeBulletHead";
            var headCollider = head.GetComponent<Collider>();
            if (headCollider != null) Object.Destroy(headCollider);
            head.transform.position = origin;
            head.transform.localScale = Vector3.one * 0.13f;

            Shader headShader = Shader.Find("Unlit/Color");
            if (headShader == null) headShader = Shader.Find("Sprites/Default");
            if (headShader == null) headShader = Shader.Find("Diffuse");
            if (headShader == null) headShader = lineShader;

            var headMaterial = new Material(headShader);
            headMaterial.color = new Color(1f, 0.95f, 0.65f, 1f);
            var renderer = head.GetComponent<Renderer>();
            if (renderer != null) renderer.material = headMaterial;

            line.SetPosition(0, origin);
            line.SetPosition(1, origin);

            var mover = go.AddComponent<BulletTracerMover>();
            mover.Init(origin, direction, line, head);
        }

        private static void SpawnMuzzleLight(Vector3 origin)
        {
            var go = new GameObject("RakeMuzzleLight");
            go.transform.position = origin;

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.78f, 0.42f, 1f);
            light.intensity = 5.5f;
            light.range = 10f;
            light.shadows = LightShadows.None;

            go.AddComponent<MuzzleLightFade>();
        }
    }

    public sealed class BulletTracerMover : MonoBehaviour
    {
        private Vector3 _start;
        private Vector3 _position;
        private Vector3 _velocity;
        private LineRenderer _line;
        private GameObject _head;
        private float _life;
        private float _linger;
        private bool _arrived;

        public void Init(Vector3 origin, Vector3 direction, LineRenderer line, GameObject head)
        {
            _start = origin;
            _position = origin;
            _velocity = direction.normalized * Ballistics.Speed;
            _line = line;
            _head = head;
        }

        private void Update()
        {
            if (!_arrived)
            {
                float dt = Time.deltaTime;
                if (dt <= 0f) return;

                _life += dt;
                if (_life > Ballistics.MaxLifetime)
                {
                    _arrived = true;
                }
                else
                {
                    Vector3 previous = _position;
                    _velocity += Vector3.up * (-Ballistics.Gravity * dt);
                    Vector3 next = previous + _velocity * dt;
                    Vector3 segment = next - previous;
                    float distance = segment.magnitude;

                    if (distance < 0.0001f)
                    {
                        _position = next;
                    }
                    else
                    {
                        Vector3 direction = segment / distance;
                        RaycastHit[] hits = Physics.RaycastAll(previous, direction, distance);
                        float best = distance;
                        bool found = false;
                        Vector3 point = next;

                        for (int i = 0; i < hits.Length; i++)
                        {
                            RaycastHit hit = hits[i];
                            if (hit.collider == null || hit.collider.isTrigger) continue;
                            if (BulletVisuals.IsLocalPlayerCollider(hit.collider)) continue;
                            if (hit.distance >= best) continue;

                            best = hit.distance;
                            point = hit.point;
                            found = true;
                        }

                        _position = point;
                        if (found) _arrived = true;
                    }
                }

                if (_head != null) _head.transform.position = _position;
                if (_line != null)
                {
                    _line.SetPosition(0, _start);
                    _line.SetPosition(1, _position);
                }

                if (!_arrived) return;
            }

            _linger += Time.deltaTime;
            float fade = 1f - Mathf.Clamp01(_linger / 0.10f);

            if (_line != null)
            {
                Color a = new Color(1f, 0.92f, 0.55f, fade);
                Color b = new Color(1f, 0.45f, 0.10f, fade * 0.85f);
                _line.SetColors(a, b);
            }

            if (_head != null)
            {
                _head.transform.localScale = Vector3.one * (0.13f * fade);
            }

            if (_linger >= 0.10f) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_head != null) Destroy(_head);
        }
    }

    public sealed class MuzzleLightFade : MonoBehaviour
    {
        private Light _light;
        private float _life = 0.09f;
        private float _t;
        private float _startIntensity;

        private void Awake()
        {
            _light = GetComponent<Light>();
            if (_light != null) _startIntensity = _light.intensity;
        }

        private void Update()
        {
            _t += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(_t / _life);
            if (_light != null) _light.intensity = _startIntensity * k;

            if (_t >= _life) Destroy(gameObject);
        }
    }
}
