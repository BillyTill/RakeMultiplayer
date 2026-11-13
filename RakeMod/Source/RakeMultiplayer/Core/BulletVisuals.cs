using UnityEngine;

namespace RakeMultiplayer
{
    /// <summary>
    /// Visual-only projectile: a bright tracer and a short muzzle light for every shot.
    ///
    /// Damage/hit registration stays exactly as before (hitscan on the shooter, validated
    /// by the server for clients), so this never changes hit results - it only makes the
    /// bullet visible in flight and lights up the surroundings for a split second.
    /// </summary>
    public static class BulletVisuals
    {
        private const float MaxDistance = 400f;
        private const float Speed = 320f;
        private const float TrailLinger = 0.08f;

        public static void Spawn(Vector3 origin, Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.0001f) return;
            direction.Normalize();

            float distance = MaxDistance;
            Vector3 end = origin + direction * distance;

            // Visual ray only: find where the tracer should stop (world geometry or a body).
            RaycastHit[] hits = Physics.RaycastAll(origin, direction, MaxDistance);
            float best = MaxDistance;
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null || hit.collider.isTrigger) continue;
                if (IsLocalPlayerCollider(hit.collider)) continue;
                if (hit.distance >= best) continue;
                best = hit.distance;
                end = hit.point;
            }

            SpawnTracer(origin, end, distance: best);
            SpawnMuzzleLight(origin);
        }

        private static bool IsLocalPlayerCollider(Collider collider)
        {
            var spawner = PlayerSpawner.Instance;
            if (spawner == null || spawner.LocalPlayer == null) return false;

            Transform local = spawner.LocalPlayer.transform;
            if (collider.transform == local || collider.transform.IsChildOf(local)) return true;

            // The weapon/viewmodel colliders may be parented to the camera, not the body.
            var net = collider.GetComponentInParent<NetworkedPlayer>();
            return net != null && net.isLocalPlayer;
        }

        private static void SpawnTracer(Vector3 start, Vector3 end, float distance)
        {
            var go = new GameObject("RakeBulletTracer");
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.SetVertexCount(2);
            line.SetWidth(0.10f, 0.03f);

            Shader lineShader = Shader.Find("Sprites/Default");
            if (lineShader == null) lineShader = Shader.Find("Diffuse");
            if (lineShader == null) lineShader = Shader.Find("Unlit/Color");
            if (lineShader == null) return;

            var material = new Material(lineShader);
            material.name = "RakeTracer";
            line.material = material;
            line.SetColors(new Color(1f, 0.92f, 0.55f, 1f), new Color(1f, 0.45f, 0.10f, 0.85f));

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "RakeBulletHead";
            var headCollider = head.GetComponent<Collider>();
            if (headCollider != null) Object.Destroy(headCollider);
            head.transform.position = start;
            head.transform.localScale = Vector3.one * 0.13f;

            Shader headShader = Shader.Find("Unlit/Color");
            if (headShader == null) headShader = Shader.Find("Sprites/Default");
            if (headShader == null) headShader = Shader.Find("Diffuse");
            if (headShader == null) headShader = lineShader;

            var headMaterial = new Material(headShader);
            headMaterial.color = new Color(1f, 0.95f, 0.65f, 1f);
            var renderer = head.GetComponent<Renderer>();
            if (renderer != null) renderer.material = headMaterial;

            line.SetPosition(0, start);
            line.SetPosition(1, start);

            var mover = go.AddComponent<BulletTracerMover>();
            mover.Init(start, end, line, head, Mathf.Max(0.02f, distance / Speed));
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
        private Vector3 _end;
        private LineRenderer _line;
        private GameObject _head;
        private float _travel;
        private float _elapsed;
        private float _linger;
        private bool _arrived;

        public void Init(Vector3 start, Vector3 end, LineRenderer line, GameObject head, float travelTime)
        {
            _start = start;
            _end = end;
            _line = line;
            _head = head;
            _travel = Mathf.Max(0.001f, travelTime);
        }

        private void Update()
        {
            if (!_arrived)
            {
                _elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(_elapsed / _travel);
                Vector3 pos = Vector3.Lerp(_start, _end, t);

                if (_head != null) _head.transform.position = pos;
                if (_line != null)
                {
                    _line.SetPosition(0, _start);
                    _line.SetPosition(1, pos);
                }

                if (t >= 1f) _arrived = true;
                return;
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
