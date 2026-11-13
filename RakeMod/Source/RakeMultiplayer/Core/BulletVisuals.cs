using UnityEngine;

namespace RakeMultiplayer
{
    /// <summary>
    /// Visible bullets: a bright camera-facing glow + a short 3 m tracer segment that
    /// travels with the projectile along the same ballistic path as the server bullet.
    /// The glow is visible from any angle, including from behind / from the shooter's
    /// scope; the short streak reads as a tracer rather than a laser from the muzzle.
    /// </summary>
    public static class BulletVisuals
    {
        private const float TrailLength = 3f;
        private static Sprite _glow;

        public static void Spawn(Vector3 origin, Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.0001f) return;
            SpawnTracer(origin, direction.normalized);
            SpawnMuzzleLight(origin);
        }

        /// <summary>Shotgun: several visual pellets in a cone around the vanilla direction.</summary>
        public static void SpawnSpread(Vector3 origin, Vector3 direction, int pellets, float spreadDegrees)
        {
            if (direction.sqrMagnitude < 0.0001f) return;
            direction.Normalize();

            pellets = Mathf.Clamp(pellets, 1, 12);
            if (pellets == 1)
            {
                Spawn(origin, direction);
                return;
            }

            Vector3 right = Vector3.Cross(Vector3.up, direction);
            if (right.sqrMagnitude < 0.001f) right = Vector3.Cross(Vector3.forward, direction);
            right.Normalize();
            Vector3 up = Vector3.Cross(direction, right).normalized;

            float radius = Mathf.Tan(Mathf.Clamp(spreadDegrees, 0f, 10f) * Mathf.Deg2Rad);
            for (int i = 0; i < pellets; i++)
            {
                float x = UnityEngine.Random.Range(-radius, radius);
                float y = UnityEngine.Random.Range(-radius, radius);
                SpawnTracer(origin, (direction + right * x + up * y).normalized);
            }

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

        private static Sprite GlowSprite()
        {
            if (_glow != null) return _glow;

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            var pixels = new Color32[size * size];
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            _glow = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 64f);
            _glow.name = "RakeBulletGlow";
            return _glow;
        }

        private static void SpawnTracer(Vector3 origin, Vector3 direction)
        {
            var go = new GameObject("RakeBullet");
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.SetVertexCount(2);
            line.SetWidth(0.015f, 0.06f);

            Shader lineShader = Shader.Find("Sprites/Default");
            if (lineShader == null) lineShader = Shader.Find("Diffuse");
            if (lineShader == null) lineShader = Shader.Find("Unlit/Color");
            if (lineShader == null) lineShader = Shader.Find("Unlit/Transparent");
            if (lineShader == null)
            {
                Object.Destroy(go);
                return;
            }

            var material = new Material(lineShader);
            material.name = "RakeTracer";
            line.material = material;
            line.SetColors(new Color(1f, 0.90f, 0.50f, 0.95f), new Color(1f, 0.55f, 0.15f, 0.95f));
            line.SetPosition(0, origin);
            line.SetPosition(1, origin);

            var head = new GameObject("RakeBulletHead");
            head.transform.position = origin;

            var glowGo = new GameObject("Glow");
            glowGo.transform.SetParent(head.transform, false);
            var glow = glowGo.AddComponent<SpriteRenderer>();
            glow.sprite = GlowSprite();
            glow.color = new Color(1f, 0.85f, 0.45f, 1f);
            glow.sortingOrder = 100;
            glowGo.transform.localScale = Vector3.one * 0.55f;

            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "Core";
            var coreCollider = core.GetComponent<Collider>();
            if (coreCollider != null) Object.Destroy(coreCollider);
            core.transform.SetParent(head.transform, false);
            core.transform.localScale = Vector3.one * 0.09f;

            Shader coreShader = Shader.Find("Unlit/Color");
            if (coreShader == null) coreShader = lineShader;
            var coreMaterial = new Material(coreShader);
            coreMaterial.color = new Color(1f, 0.95f, 0.65f, 1f);
            var coreRenderer = core.GetComponent<Renderer>();
            if (coreRenderer != null) coreRenderer.material = coreMaterial;

            var mover = go.AddComponent<BulletTracerMover>();
            mover.Init(origin, direction, line, head, glow);
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
        private Vector3 _position;
        private Vector3 _velocity;
        private LineRenderer _line;
        private GameObject _head;
        private SpriteRenderer _glow;
        private float _life;
        private float _linger;
        private bool _arrived;

        public void Init(Vector3 origin, Vector3 direction, LineRenderer line, GameObject head, SpriteRenderer glow)
        {
            _position = origin;
            _velocity = direction.normalized * Ballistics.Speed;
            _line = line;
            _head = head;
            _glow = glow;
            UpdateVisuals();
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
                        RaycastHit[] hits = Physics.RaycastAll(previous, direction, distance, ~0);
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

                UpdateVisuals();
                if (!_arrived) return;
            }

            _linger += Time.deltaTime;
            float fade = 1f - Mathf.Clamp01(_linger / 0.10f);

            if (_line != null)
            {
                _line.SetColors(new Color(1f, 0.90f, 0.50f, fade * 0.95f),
                                new Color(1f, 0.55f, 0.15f, fade * 0.95f));
            }

            if (_glow != null)
            {
                Color c = _glow.color;
                c.a = fade;
                _glow.color = c;
            }

            if (_head != null)
            {
                _head.transform.localScale = Vector3.one * (0.6f + 0.4f * fade);
            }

            if (_linger >= 0.10f) Destroy(gameObject);
        }

        private void UpdateVisuals()
        {
            Vector3 direction = _velocity.sqrMagnitude > 0.01f ? _velocity.normalized : Vector3.forward;

            if (_head != null) _head.transform.position = _position;

            if (_line != null)
            {
                _line.SetPosition(0, _position - direction * 3f);
                _line.SetPosition(1, _position);
            }
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
