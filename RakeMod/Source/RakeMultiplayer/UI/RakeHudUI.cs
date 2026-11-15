using System;
using System.Collections.Generic;
using RakeMultiplayer.Networking;
using UnityEngine;
using UnityEngine.UI;

namespace RakeMultiplayer.UI
{
    /// <summary>
    /// The Rake's in-game HUD: dark, organic, bone / ember / blood.
    ///
    /// Health is a ribbed wound that fills with blood and sprouts veins as it empties;
    /// the phase is a scratched moon with an ember ring; abilities are four dark sockets
    /// with claw / paw / eye / leap glyphs; the sense markers are torn rings and chevrons.
    /// Nothing here is paper, tape or handwriting.
    /// </summary>
    public class RakeHudUI : MonoBehaviour
    {
        // ---- palette
        private static readonly Color Bone = new Color(0.847f, 0.816f, 0.745f, 1f);
        private static readonly Color BoneDim = new Color(0.588f, 0.569f, 0.518f, 1f);
        private static readonly Color Ember = new Color(0.776f, 0.463f, 0.141f, 1f);
        private static readonly Color Moss = new Color(0.588f, 0.690f, 0.549f, 1f);
        private static readonly Color Blood = new Color(0.588f, 0.102f, 0.071f, 1f);
        private static readonly Color BloodHot = new Color(0.729f, 0.149f, 0.102f, 1f);

        private const float RefWidth = 1920f;
        private const float RefHeight = 1080f;
        private const float BandRadiusX = 470f;
        private const float BandRadiusY = 300f;
        private const int MarkerPoolSize = 16;

        private RakePlayerController _owner;

        private CanvasGroup _group;
        private float _fade;

        private Font _font;

        private Text _hpName;
        private Text _hpValue;
        private Image _hpFill;
        private Image _hpVeins;
        private RectTransform _hpPlate;

        private Text _phaseKicker;
        private Text _phaseSubtitle;
        private Text _timer;
        private Image _emberArc;
        private Image _moon;

        private Text _status;

        private readonly Image[] _ringFill = new Image[4];
        private readonly Image[] _socketGlow = new Image[4];
        private readonly Text[] _abilityNames = new Text[4];
        private readonly Text[] _abilityStates = new Text[4];

        private readonly List<Image> _markerIcons = new List<Image>(MarkerPoolSize);
        private readonly List<Text> _markerText = new List<Text>(MarkerPoolSize);
        private readonly List<RectTransform> _markerRoots = new List<RectTransform>(MarkerPoolSize);

        private Image _bloodOverlay;
        private RawImage _grain;

        private float _slowRefresh;
        private float _fastRefresh;
        private float _time;
        private float _damage;

        public static RakeHudUI Instance { get; private set; }

        private static Font _cachedFont;

        // ---------------------------------------------------------------- build

        public static RakeHudUI Create(RakePlayerController owner)
        {
            var go = new GameObject("RakeHud");
            go.transform.SetParent(owner.transform, false);

            var hud = go.AddComponent<RakeHudUI>();
            hud._owner = owner;
            Instance = hud;

            try
            {
                hud.Build();
            }
            catch (Exception ex)
            {
                RakeMultiplayerPlugin.Log?.LogError("[HUD] failed to build the Rake HUD: " + ex);
                UnityEngine.Object.Destroy(go);
                return null;
            }

            return hud;
        }

        private void Build()
        {
            _font = FontForHud();

            var canvasGo = new GameObject("RakeHudCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 31000;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var root = NewRect("Root", canvasGo.transform);
            Stretch(root);
            _group = root.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;

            BuildScreenEdges(root);
            BuildAbilities(root);
            BuildHealth(root);
            BuildPhase(root);
            BuildStatus(root);
            BuildMarkers(root);
        }

        private static Font FontForHud()
        {
            if (_cachedFont != null) return _cachedFont;

            try
            {
                _cachedFont = Font.CreateDynamicFontFromOSFont(
                    new[] { "Bahnschrift SemiCondensed", "Bahnschrift", "Segoe UI Semibold", "Arial" }, 32);
            }
            catch (Exception)
            {
                _cachedFont = null;
            }

            if (_cachedFont == null) _cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return _cachedFont;
        }

        private void BuildScreenEdges(RectTransform root)
        {
            var vignette = NewImage("Vignette", root, new Color(0f, 0f, 0f, 0.85f), Tex.Vignette());
            Stretch(vignette.rectTransform);

            _bloodOverlay = NewImage("Blood", root, new Color(1f, 1f, 1f, 0f), Tex.BloodVignette());
            Stretch(_bloodOverlay.rectTransform);

            // Hit grain: a tiled noise layer that only shows while damage is fresh.
            var grainGo = new GameObject("Grain", typeof(RectTransform), typeof(RawImage));
            grainGo.transform.SetParent(root, false);
            var grt = (RectTransform)grainGo.transform;
            Stretch(grt);

            _grain = grainGo.GetComponent<RawImage>();
            _grain.texture = Tex.NoiseTexture();
            _grain.uvRect = new Rect(0f, 0f, 3f, 3f);
            _grain.color = new Color(1f, 1f, 1f, 0f);
        }

        private void BuildHealth(RectTransform root)
        {
            var plate = NewImage("HealthPlate", root, new Color(0.027f, 0.035f, 0.031f, 0.92f), Tex.Plate(700, 122, 31));
            _hpPlate = plate.rectTransform;
            TopLeft(_hpPlate, 620f, 928f, 690f, 122f);

            // veins behind the plate, hidden until the Rake is hurt
            _hpVeins = NewImage("Veins", plate.transform, new Color(Blood.r, Blood.g, Blood.b, 0f), Tex.Veins());
            SetRect(_hpVeins.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 0f), new Vector2(1180f, 420f));

            _hpName = NewText("HpName", plate.transform, "THE RAKE", 20, BoneDim, TextAnchor.MiddleLeft);
            TopLeft(_hpName.rectTransform, 30f, 18f, 300f, 24f);

            _hpValue = NewText("HpValue", plate.transform, "2500 / 2500", 23, Bone, TextAnchor.MiddleRight);
            TopLeft(_hpValue.rectTransform, 360f, 16f, 300f, 26f);

            // wound track
            var track = NewImage("WoundTrack", plate.transform, new Color(0.071f, 0.031f, 0.027f, 0.92f), Tex.Wound());
            TopLeft(track.rectTransform, 34f, 70f, 622f, 28f);

            _hpFill = NewImage("WoundFill", track.transform, Blood, Tex.Bar());
            var frt = _hpFill.rectTransform;
            frt.anchorMin = new Vector2(0f, 0.5f);
            frt.anchorMax = new Vector2(0f, 0.5f);
            frt.pivot = new Vector2(0f, 0.5f);
            frt.anchoredPosition = new Vector2(5f, 0f);
            frt.sizeDelta = new Vector2(612f, 18f);

            var teeth = NewImage("Ribs", plate.transform, Color.white, Tex.Ribs());
            TopLeft(teeth.rectTransform, 34f, 62f, 622f, 44f);
        }

        private void BuildPhase(RectTransform root)
        {
            var halo = NewImage("PhaseHalo", root, new Color(1f, 1f, 1f, 1f), Tex.Glow(620));
            TopLeft(halo.rectTransform, 1442f, -200f, 620f, 620f);

            var ringBg = NewImage("RingBg", root, new Color(Bone.r, Bone.g, Bone.b, 0.16f), Tex.Ring(128, 2f));
            TopLeft(ringBg.rectTransform, 1688f, 46f, 128f, 128f);

            _emberArc = NewImage("EmberArc", root, Ember, Tex.Ring(140, 5f));
            _emberArc.type = Image.Type.Filled;
            _emberArc.fillMethod = Image.FillMethod.Radial360;
            _emberArc.fillOrigin = (int)Image.Origin360.Top;
            _emberArc.fillClockwise = true;
            _emberArc.fillAmount = 1f;
            TopLeft(_emberArc.rectTransform, 1682f, 40f, 140f, 140f);

            _moon = NewImage("Moon", root, Bone, Tex.Crescent(160));
            TopLeft(_moon.rectTransform, 1672f, 30f, 160f, 160f);

            _phaseKicker = NewText("Kicker", root, "NIGHT 1 / 3", 21, Ember, TextAnchor.MiddleCenter);
            TopLeft(_phaseKicker.rectTransform, 1552f, 10f, 400f, 26f);

            _timer = NewText("Timer", root, "05:00", 46, Bone, TextAnchor.MiddleCenter);
            TopLeft(_timer.rectTransform, 1552f, 178f, 400f, 56f);

            _phaseSubtitle = NewText("Subtitle", root, "THE HUNT IS ON", 16, BoneDim, TextAnchor.MiddleCenter);
            TopLeft(_phaseSubtitle.rectTransform, 1552f, 248f, 400f, 22f);
        }

        private void BuildAbilities(RectTransform root)
        {
            var plate = NewImage("AbilityPlate", root, new Color(0.024f, 0.031f, 0.027f, 0.88f), Tex.Plate(508, 160, 77));
            TopLeft(plate.rectTransform, 92f, 884f, 496f, 160f);

            string[] names = { "GRAB", "ANIMAL", "CAMERA", "LEAP" };
            Sprite[] glyphs = { Tex.Claw(), Tex.Paw(), Tex.Eye(), Tex.Leap() };

            for (int i = 0; i < 4; i++)
            {
                float cx = 158f + i * 124f;
                float cy = 936f;

                var socket = NewImage("Socket" + i, plate.transform,
                    new Color(0.035f, 0.043f, 0.039f, 0.92f), Tex.Socket(78, 40 + i));
                TopLeft(socket.rectTransform, cx - 92f - 39f, cy - 884f - 39f, 78f, 78f);

                _socketGlow[i] = NewImage("Glow", socket.transform, new Color(Ember.r, Ember.g, Ember.b, 0.35f),
                    Tex.Ring(96, 3f));
                _ringFill[i] = NewImage("Cooldown", socket.transform, Ember, Tex.Ring(96, 4f));
                _ringFill[i].type = Image.Type.Filled;
                _ringFill[i].fillMethod = Image.FillMethod.Radial360;
                _ringFill[i].fillOrigin = (int)Image.Origin360.Top;
                _ringFill[i].fillClockwise = false;
                _ringFill[i].fillAmount = 1f;

                foreach (var ring in new[] { _socketGlow[i], _ringFill[i] })
                {
                    var rt = ring.rectTransform;
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = Vector2.zero;
                    rt.sizeDelta = new Vector2(96f, 96f);
                }

                var glyph = NewImage("Glyph", socket.transform, Bone, glyphs[i]);
                glyph.preserveAspect = true;
                var grt = glyph.rectTransform;
                grt.anchorMin = new Vector2(0.5f, 0.5f);
                grt.anchorMax = new Vector2(0.5f, 0.5f);
                grt.anchoredPosition = Vector2.zero;
                grt.sizeDelta = new Vector2(34f, 34f);

                _abilityNames[i] = NewText("Name", socket.transform, names[i], 16, Bone, TextAnchor.MiddleCenter);
                var nrt = _abilityNames[i].rectTransform;
                nrt.anchorMin = new Vector2(0.5f, 0f);
                nrt.anchorMax = new Vector2(0.5f, 0f);
                nrt.pivot = new Vector2(0.5f, 1f);
                nrt.anchoredPosition = new Vector2(0f, -22f);
                nrt.sizeDelta = new Vector2(110f, 20f);

                _abilityStates[i] = NewText("State", socket.transform, "READY", 16, Ember, TextAnchor.MiddleCenter);
                var srt = _abilityStates[i].rectTransform;
                srt.anchorMin = new Vector2(0.5f, 0f);
                srt.anchorMax = new Vector2(0.5f, 0f);
                srt.pivot = new Vector2(0.5f, 1f);
                srt.anchoredPosition = new Vector2(0f, -44f);
                srt.sizeDelta = new Vector2(110f, 20f);
            }
        }

        private void BuildStatus(RectTransform root)
        {
            _status = NewText("Status", root, string.Empty, 18, Ember, TextAnchor.MiddleCenter);
            TopLeft(_status.rectTransform, 660f, 878f, 600f, 24f);
            _status.gameObject.SetActive(false);
        }

        private void BuildMarkers(RectTransform root)
        {
            for (int i = 0; i < MarkerPoolSize; i++)
            {
                var holder = NewRect("Marker" + i, root);
                holder.anchorMin = new Vector2(0.5f, 0.5f);
                holder.anchorMax = new Vector2(0.5f, 0.5f);
                holder.pivot = new Vector2(0.5f, 0.5f);
                holder.sizeDelta = new Vector2(110f, 110f);

                var icon = NewImage("Icon", holder, BloodHot, Tex.PreyRing());
                var irt = icon.rectTransform;
                irt.anchorMin = new Vector2(0.5f, 0.5f);
                irt.anchorMax = new Vector2(0.5f, 0.5f);
                irt.anchoredPosition = Vector2.zero;
                irt.sizeDelta = new Vector2(108f, 108f);

                var label = NewText("Distance", holder, "0 m", 16, BoneDim, TextAnchor.MiddleCenter);
                var lrt = label.rectTransform;
                lrt.anchorMin = new Vector2(0.5f, 0.5f);
                lrt.anchorMax = new Vector2(0.5f, 0.5f);
                lrt.anchoredPosition = new Vector2(0f, -62f);
                lrt.sizeDelta = new Vector2(120f, 22f);

                holder.gameObject.SetActive(false);
                _markerRoots.Add(holder);
                _markerIcons.Add(icon);
                _markerText.Add(label);
            }
        }

        // ---------------------------------------------------------------- runtime

        /// <summary>Dev bridge helper.</summary>
        public static string BridgePulse()
        {
            if (Instance == null) return "{\"ok\":false,\"error\":\"hud is not active\"}";
            Instance.PulseDamage();
            return "{\"ok\":true}";
        }

        public void PulseDamage()
        {
            _damage = 1f;
        }

        private void Update()
        {
            if (_owner == null) return;

            Transform rake = _owner.RakeTransform;
            if (rake == null)
            {
                Destroy(gameObject);
                return;
            }

            float dt = Time.unscaledDeltaTime;
            _time += dt;

            _fade = Mathf.MoveTowards(_fade, 1f, dt * 4f);
            if (_group != null) _group.alpha = _fade;

            _damage = Mathf.MoveTowards(_damage, 0f, dt * 0.9f);

            _slowRefresh -= dt;
            if (_slowRefresh <= 0f)
            {
                _slowRefresh = 0.12f;
                RefreshTexts();
            }

            Animate();

            _fastRefresh -= dt;
            if (_fastRefresh <= 0f)
            {
                _fastRefresh = 0.05f;
                RefreshMarkers(rake);
            }
        }

        private void RefreshTexts()
        {
            if (_owner == null) return;

            float hp = _owner.Health;
            float max = Mathf.Max(1f, _owner.MaxHealth);
            float ratio = Mathf.Clamp01(hp / max);

            _hpValue.text = Mathf.CeilToInt(hp) + " / " + Mathf.CeilToInt(max);
            _hpFill.rectTransform.sizeDelta = new Vector2(612f * ratio, 18f);

            bool night = _owner.AttacksAllowedForHud;
            DayNightManager dayNight = DayNightManager.Instance;
            SessionManager session = SessionManager.Instance;

            int nightNumber = session != null ? Mathf.Max(1, session.CurrentNight) : 1;
            Color accent = night ? Ember : Moss;

            _phaseKicker.text = (night ? "NIGHT " : "DAY ") + nightNumber + " / 3";
            _phaseKicker.color = accent;
            _phaseSubtitle.text = night ? "THE HUNT IS ON" : "THE RAKE SLEEPS";
            _phaseSubtitle.color = new Color(BoneDim.r, BoneDim.g, BoneDim.b, 0.8f);
            _moon.color = night ? Bone : new Color(0.72f, 0.78f, 0.70f, 1f);

            bool hasCycle = dayNight != null && dayNight.IsRunning;
            float left = hasCycle ? dayNight.GetRemainingTime() : 0f;
            float total = dayNight != null
                ? (dayNight.CurrentPhase == Phase.Day ? dayNight.DayDuration : dayNight.NightDuration)
                : 0f;

            _timer.text = hasCycle
                ? Mathf.FloorToInt(left / 60f).ToString("00") + ":" + Mathf.FloorToInt(left % 60f).ToString("00")
                : "--:--";

            _emberArc.fillAmount = hasCycle && total > 0.01f ? Mathf.Clamp01(left / total) : 0f;
            _emberArc.color = accent;

            // abilities
            SetAbility(0, _owner.GrabCooldownLeft, _owner.GrabCooldownTotal);
            SetAbility(1, _owner.AnimalCooldownLeft, _owner.AnimalCooldownTotal);
            SetAbility(2, _owner.CameraCooldownLeft, _owner.CameraCooldownTotal);
            SetAbility(3, _owner.LeapCooldownLeft, _owner.LeapCooldownTotal);

            // veins + status line
            _hpVeins.color = new Color(Blood.r, Blood.g, Blood.b, Mathf.Clamp01((0.6f - ratio) * 1.6f) * 0.55f);

            bool locked = _owner.DamageLockoutLeft > 0.05f;
            bool prey = night && _owner.PreyInReach;

            if (locked)
            {
                _status.gameObject.SetActive(true);
                _status.color = BloodHot;
                _status.text = "DAMAGED  ·  ATTACK LOCKED " + Mathf.CeilToInt(_owner.DamageLockoutLeft) + "s";
            }
            else if (_owner.IsClimbing)
            {
                _status.gameObject.SetActive(true);
                _status.color = BoneDim;
                _status.text = _owner.IsHanging ? "HANGING" : "CLIMBING";
            }
            else if (prey)
            {
                _status.gameObject.SetActive(true);
                _status.color = Ember;
                _status.text = "PREY IN REACH";
            }
            else
            {
                _status.gameObject.SetActive(false);
            }
        }

        private void SetAbility(int index, float remaining, float total)
        {
            if (index < 0 || index >= 4) return;

            bool ready = remaining <= 0.01f;
            _ringFill[index].gameObject.SetActive(!ready);
            if (!ready)
            {
                float ratio = Mathf.Clamp01(remaining / Mathf.Max(0.01f, total));
                _ringFill[index].fillAmount = 1f - ratio;
            }

            _socketGlow[index].color = new Color(Ember.r, Ember.g, Ember.b, ready ? 0.22f : 0.06f);
            _abilityStates[index].text = ready ? "READY" : Mathf.CeilToInt(remaining) + "s";
            _abilityStates[index].color = ready ? Ember : new Color(0.59f, 0.47f, 0.33f, 1f);
            _abilityNames[index].color = ready ? Bone : BoneDim;
        }

        private void Animate()
        {
            float pulse = 0.78f + 0.22f * Mathf.Sin(_time * 4.2f);
            Color fill = _damage > 0.02f ? Color.Lerp(Blood, BloodHot, _damage) : Blood;
            _hpFill.color = new Color(fill.r * pulse, fill.g * pulse, fill.b * pulse, 1f);

            if (_bloodOverlay != null)
            {
                _bloodOverlay.color = new Color(1f, 1f, 1f, Mathf.Clamp01(_damage) * 0.75f);
            }

            if (_grain != null)
            {
                float grain = Mathf.Clamp01(_damage) * 0.45f;
                _grain.color = new Color(1f, 1f, 1f, grain);
                if (grain > 0.01f)
                {
                    float g = Time.unscaledTime;
                    _grain.uvRect = new Rect(Mathf.Repeat(g * 3.1f, 1f), Mathf.Repeat(g * 5.7f, 1f), 3f, 3f);
                }
            }

            if (_hpPlate != null)
            {
                float bump = 1f + 0.02f * _damage;
                _hpPlate.localScale = new Vector3(bump, bump, 1f);
            }

            if (_emberArc != null)
            {
                float glow = 0.75f + 0.25f * Mathf.Sin(_time * 2.1f);
                _emberArc.color = new Color(_emberArc.color.r, _emberArc.color.g, _emberArc.color.b,
                    Mathf.Lerp(0.55f, 1f, glow));
            }
        }

        private void RefreshMarkers(Transform rake)
        {
            int used = 0;

            Vector3 forward = rake.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            IList<NetworkedPlayer> players = _owner.SensePlayers;
            for (int i = 0; i < players.Count && used < _markerRoots.Count; i++)
            {
                if (players[i] == null) continue;
                used = PlaceMarker(rake, players[i].transform.position, forward, right, used, 0);
            }

            IList<Animal_AI> animals = _owner.SenseAnimals;
            for (int i = 0; i < animals.Count && used < _markerRoots.Count; i++)
            {
                if (animals[i] == null) continue;
                used = PlaceMarker(rake, animals[i].transform.position, forward, right, used, 1);
            }

            IList<Trap_proj> traps = _owner.SenseTraps;
            for (int i = 0; i < traps.Count && used < _markerRoots.Count; i++)
            {
                if (traps[i] == null) continue;
                used = PlaceMarker(rake, traps[i].transform.position, forward, right, used, 2);
            }

            for (int i = used; i < _markerRoots.Count; i++)
            {
                if (_markerRoots[i].gameObject.activeSelf) _markerRoots[i].gameObject.SetActive(false);
            }
        }

        private int PlaceMarker(Transform rake, Vector3 world, Vector3 forward, Vector3 right, int index, int kind)
        {
            Vector3 delta = world - rake.position;
            delta.y = 0f;
            float distance = delta.magnitude;
            if (distance > 100f || distance < 0.2f) return index;

            float nx = Vector3.Dot(delta, right) / distance;
            float nz = Vector3.Dot(delta, forward) / distance;

            var root = _markerRoots[index];
            root.anchoredPosition = new Vector2(nx * BandRadiusX, nz * BandRadiusY);
            root.localRotation = Quaternion.identity;

            var icon = _markerIcons[index];
            icon.rectTransform.localRotation =
                Quaternion.Euler(0f, 0f, Mathf.Atan2(-nx * BandRadiusX, -nz * BandRadiusY) * Mathf.Rad2Deg);
            float pulse = 0.5f + 0.5f * Mathf.Sin(_time * 2.6f + index);
            float alpha = 0.5f + 0.4f * (1f - distance / 100f);

            if (kind == 0)
            {
                icon.sprite = Tex.PreyRing();
                icon.color = new Color(Mathf.Lerp(0.55f, 0.73f, pulse), 0.11f, 0.08f, alpha);
                icon.rectTransform.sizeDelta = new Vector2(108f, 108f);
                _markerText[index].color = BoneDim;
            }
            else if (kind == 1)
            {
                icon.sprite = Tex.Chevrons();
                icon.color = new Color(0.77f, 0.74f, 0.67f, alpha * 0.75f);
                icon.rectTransform.sizeDelta = new Vector2(44f, 44f);
                _markerText[index].color = new Color(BoneDim.r, BoneDim.g, BoneDim.b, 0.6f);
            }
            else
            {
                icon.sprite = Tex.Cross();
                icon.color = new Color(0.59f, 0.57f, 0.52f, alpha * 0.7f);
                icon.rectTransform.sizeDelta = new Vector2(32f, 32f);
                _markerText[index].color = new Color(BoneDim.r, BoneDim.g, BoneDim.b, 0.55f);
            }

            _markerText[index].text = Mathf.RoundToInt(distance) + " m";
            root.gameObject.SetActive(true);
            return index + 1;
        }

        // ---------------------------------------------------------------- ui helpers

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>Place a rect using top-left layout units (as in the concept program).</summary>
        private static void TopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static Image NewImage(string name, Transform parent, Color color, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.sprite = sprite;
            return img;
        }

        private Text NewText(string name, Transform parent, string text, int size, Color color, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = _font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        // ---------------------------------------------------------------- texture kit

        /// <summary>Small procedural texture kit; everything is cached.</summary>
        private static class Tex
        {
            private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

            private static readonly Dictionary<string, Texture2D> TexCache = new Dictionary<string, Texture2D>();

            private static Texture2D GetTex(string key, Func<Texture2D> make)
            {
                Texture2D tex;
                if (TexCache.TryGetValue(key, out tex) && tex != null) return tex;
                tex = make();
                TexCache[key] = tex;
                return tex;
            }

            /// <summary>Animated film grain for hits (tiled, point filtered).</summary>
            public static Texture2D NoiseTexture()
            {
                return GetTex("noise", delegate
                {
                    const int size = 256;
                    var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                    var px = new Color32[size * size];
                    var rnd = new System.Random(1234);

                    for (int i = 0; i < px.Length; i++)
                    {
                        byte v = (byte)rnd.Next(0, 256);
                        px[i] = new Color32(v, v, v, (byte)rnd.Next(30, 150));
                    }

                    tex.SetPixels32(px);
                    tex.Apply(false, true);
                    tex.wrapMode = TextureWrapMode.Repeat;
                    tex.filterMode = FilterMode.Point;
                    return tex;
                });
            }

            private static Sprite Get(string key, Func<Sprite> make)
            {
                Sprite sprite;
                if (Cache.TryGetValue(key, out sprite) && sprite != null) return sprite;
                sprite = make();
                Cache[key] = sprite;
                return sprite;
            }

            private static Sprite FromTexture(Texture2D tex)
            {
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }

            private static Texture2D NewTex(int w, int h)
            {
                return new Texture2D(w, h, TextureFormat.ARGB32, false);
            }

            private static void Blend(Color32[] px, int w, int h, int x, int y, Color color)
            {
                if (x < 0 || y < 0 || x >= w || y >= h) return;
                int i = y * w + x;
                float a = color.a + px[i].a / 255f * (1f - color.a);
                if (a <= 0.001f) return;
                float r = (color.r * color.a + px[i].r / 255f * (px[i].a / 255f) * (1f - color.a)) / a;
                float g = (color.g * color.a + px[i].g / 255f * (px[i].a / 255f) * (1f - color.a)) / a;
                float b = (color.b * color.a + px[i].b / 255f * (px[i].a / 255f) * (1f - color.a)) / a;
                px[i] = new Color32((byte)(Mathf.Clamp01(r) * 255f), (byte)(Mathf.Clamp01(g) * 255f),
                    (byte)(Mathf.Clamp01(b) * 255f), (byte)(Mathf.Clamp01(a) * 255f));
            }

            private static void Disc(Color32[] px, int w, int h, float cx, float cy, float r, Color color)
            {
                int x0 = Mathf.FloorToInt(cx - r - 1), x1 = Mathf.CeilToInt(cx + r + 1);
                int y0 = Mathf.FloorToInt(cy - r - 1), y1 = Mathf.CeilToInt(cy + r + 1);
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        float dx = x - cx, dy = y - cy;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(r - d);
                        if (a <= 0f) continue;
                        Blend(px, w, h, x, y, new Color(color.r, color.g, color.b, color.a * a));
                    }
                }
            }

            private static void Line(Color32[] px, int w, int h, float x0, float y0, float x1, float y1,
                float width, Color color)
            {
                float dx = x1 - x0, dy = y1 - y0;
                int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) * 1.5f) + 1;
                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    Disc(px, w, h, x0 + dx * t, y0 + dy * t, Mathf.Max(0.7f, width * 0.5f), color);
                }
            }

            private static void Arc(Color32[] px, int w, int h, float cx, float cy, float outer, float inner,
                float startDeg, float endDeg, Color color)
            {
                int steps = Mathf.Max(8, Mathf.CeilToInt((endDeg - startDeg) * 1.5f));
                for (int i = 0; i <= steps; i++)
                {
                    float a = Mathf.Deg2Rad * Mathf.Lerp(startDeg, endDeg, i / (float)steps);
                    float mid = (outer + inner) * 0.5f;
                    Disc(px, w, h, cx + Mathf.Cos(a) * mid, cy + Mathf.Sin(a) * mid,
                        (outer - inner) * 0.5f, color);
                }
            }

            // ---- plates and edges

            public static Sprite Plate(int w, int h, int seed)
            {
                return Get("plate" + w + "x" + h + "_" + seed, delegate
                {
                    var tex = NewTex(w, h);
                    var px = new Color32[w * h];
                    var rnd = new System.Random(seed);
                    float radius = Mathf.Min(w, h) * 0.22f;

                    // jittered rounded rectangle
                    float[] noise = new float[16];
                    for (int i = 0; i < noise.Length; i++) noise[i] = (float)rnd.NextDouble();

                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            float nx = x / (float)w, ny = y / (float)h;
                            float wobble = (Mathf.Sin(nx * 9.1f + seed) + Mathf.Sin(ny * 7.7f - seed)) * 1.8f;

                            float dx = Mathf.Max(Mathf.Abs(x - w * 0.5f) - (w * 0.5f - radius), 0f);
                            float dy = Mathf.Max(Mathf.Abs(y - h * 0.5f) - (h * 0.5f - radius), 0f);
                            float d = Mathf.Sqrt(dx * dx + dy * dy) - radius + wobble;

                            float a = Mathf.Clamp01(0.5f - d) * 0.97f;
                            if (a <= 0.01f) continue;

                            float shade = 0.92f + 0.08f * Mathf.Sin(nx * 21f) * Mathf.Sin(ny * 17f);
                            byte v = (byte)(Mathf.Clamp01(shade) * 255f);
                            px[y * w + x] = new Color32(v, v, v, (byte)(a * 255f));
                        }
                    }

                    tex.SetPixels32(px);
                    tex.Apply(false, true);
                    return FromTexture(tex);
                });
            }

            public static Sprite Socket(int size, int seed)
            {
                return Get("socket" + size + "_" + seed, delegate
                {
                    var tex = NewTex(size, size);
                    var px = new Color32[size * size];
                    float c = size * 0.5f;
                    var rnd = new System.Random(seed);
                    float[] phase = new float[8];
                    for (int i = 0; i < phase.Length; i++) phase[i] = (float)rnd.NextDouble() * 6.28f;

                    for (int y = 0; y < size; y++)
                    {
                        for (int x = 0; x < size; x++)
                        {
                            float dx = (x - c) / c, dy = (y - c) / c;
                            float ang = Mathf.Atan2(dy, dx);
                            float r = Mathf.Sqrt(dx * dx + dy * dy);
                            float edge = 0.86f
                                         + 0.05f * Mathf.Sin(ang * 5f + phase[0])
                                         + 0.04f * Mathf.Sin(ang * 8f + phase[1]);
                            float a = Mathf.Clamp01((edge - r) * 18f);
                            if (a <= 0.01f) continue;
                            px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                        }
                    }

                    tex.SetPixels32(px);
                    tex.Apply(false, true);
                    return FromTexture(tex);
                });
            }

            public static Sprite Glow(int size)
            {
                return Get("glow" + size, delegate
                {
                    var tex = NewTex(size, size);
                    var px = new Color32[size * size];
                    float c = size * 0.5f;
                    for (int y = 0; y < size; y++)
                    {
                        for (int x = 0; x < size; x++)
                        {
                            float dx = (x - c) / c, dy = (y - c) / c;
                            float d = Mathf.Sqrt(dx * dx + dy * dy);
                            float a = Mathf.Clamp01(1f - d);
                            a = a * a * a * 0.85f;
                            px[y * size + x] = new Color32(4, 6, 5, (byte)(a * 255f));
                        }
                    }

                    tex.SetPixels32(px);
                    tex.Apply(false, true);
                    return FromTexture(tex);
                });
            }

            public static Sprite Vignette()
            {
                return Get("vignette", delegate
                {
                    const int w = 512, h = 288;
                    var tex = NewTex(w, h);
                    var px = new Color32[w * h];
                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            float nx = x / (float)(w - 1) * 2f - 1f;
                            float ny = y / (float)(h - 1) * 2f - 1f;
                            float r = Mathf.Sqrt(nx * nx + ny * ny) * 0.72f;
                            float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.52f, 1.05f, r)) * 0.72f;
                            px[y * w + x] = new Color32(0, 0, 0, (byte)(Mathf.Clamp01(a) * 255f));
                        }
                    }

                    tex.SetPixels32(px);
                    tex.Apply(false, true);
                    return FromTexture(tex);
                });
            }

            public static Sprite BloodVignette()
            {
                return Get("blood", delegate
                {
                    const int w = 512, h = 288;
                    var tex = NewTex(w, h);
                    var px = new Color32[w * h];
                    var rnd = new System.Random(7);
                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            float nx = x / (float)(w - 1) * 2f - 1f;
                            float ny = y / (float)(h - 1) * 2f - 1f;
                            float r = Mathf.Sqrt(nx * nx + ny * ny) * 0.72f;
                            float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.48f, 1.02f, r));
                            px[y * w + x] = new Color32(110, 12, 8, (byte)(Mathf.Clamp01(a) * 255f));
                        }
                    }

                    // splatter blobs near the edges
                    for (int i = 0; i < 26; i++)
                    {
                        float cx = (float)rnd.NextDouble() * w;
                        float cy = (float)rnd.NextDouble() * h;
                        float nx = cx / w * 2f - 1f, ny = cy / h * 2f - 1f;
                        if (Mathf.Sqrt(nx * nx + ny * ny) < 0.7f) continue;
                        Disc(px, w, h, cx, cy, 1.5f + (float)rnd.NextDouble() * 5f, new Color(0.47f, 0.06f, 0.04f, 0.55f));
                    }

                    tex.SetPixels32(px);
                    tex.Apply(false, true);
                    return FromTexture(tex);
                });
            }

            public static Sprite Wound()
            {
                return Get("wound", delegate
                {
                    const int w = 512, h = 32;
                    var tex = NewTex(w, h);
                    var px = new Color32[w * h];
                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            float ny = (y - h * 0.5f) / (h * 0.5f);
                            float a = Mathf.Clamp01((1f - Mathf.Abs(ny)) * 6f);
                            if (a <= 0.01f) continue;
                            px[y * w + x] = new Color32(20, 8, 7, (byte)(a * 255f));
                        }
                    }

                    tex.SetPixels32(px);
                    tex.Apply(false, true);
                    return FromTexture(tex);
                });
            }

            public static Sprite Bar()
            {
                return Get("bar", delegate
                {
                    const int w = 256, h = 32;
                    var tex = NewTex(w, h);
                    var px = new Color32[w * h];
                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            float ny = (y - h * 0.5f) / (h * 0.5f);
                            float a = Mathf.Clamp01((1f - Mathf.Abs(ny)) * 8f);
                            if (a <= 0.01f) continue;
                            px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                        }
                    }

                    tex.SetPixels32(px);
                    tex.Apply(false, true);
                    return FromTexture(tex);
                });
            }

            public static Sprite Ribs()
            {
                return Get("ribs", delegate
                {
                    const int w = 512, h = 40;
                    var tex = NewTex(w, h);
                    var px = new Color32[w * h];
                    var bone = new Color(0.745f, 0.714f, 0.643f, 0.85f);
                    var dark = new Color(0.588f, 0.569f, 0.518f, 0.5f);

                    // teeth as filled triangles
                    for (int i = 0; i < 11; i++)
                    {
                        float x = 4f + i * (w - 8f) / 10f;
                        for (int y = 0; y < 12; y++)
                        {
                            float t = y / 12f;
                            float half = 2.5f + 4.5f * t;
                            for (float xx = x - half; xx <= x + half; xx++)
                            {
                                Blend(px, w, h, Mathf.RoundToInt(xx), y, bone);
                                Blend(px, w, h, Mathf.RoundToInt(xx), h - 1 - y, dark);
                            }
                        }
                    }

                    tex.SetPixels32(px);
                    tex.Apply(false, true);
                    return FromTexture(tex);
                });
            }

            public static Sprite Veins()
            {
                return Get("veins", delegate
                {
                    const int w = 512, h = 192;
                    var tex = NewTex(w, h);
                    var px = new Color32[w * h];
                    var rnd = new System.Random(11);
                    var color = new Color(0.60f, 0.10f, 0.07f, 0.75f);
                    for (int i = 0; i < 9; i++)
                    {
                        float x = w * 0.5f + (i - 4) * 26f + (float)rnd.NextDouble() * 10f;
                        float y = h - 6f;
                        float a = -Mathf.PI / 2f + (i - 4) * 0.22f;
                        float len = 50f + (float)rnd.NextDouble() * 70f;
                        Branch(px, w, h, x, y, a, len, 2.4f, color, 2, rnd);
                    }

                    tex.SetPixels32(px);
                    tex.Apply(false, true);
                    return FromTexture(tex);
                });
            }

            private static void Branch(Color32[] px, int w, int h, float x, float y, float angle, float length,
                float width, Color color, int depth, System.Random rnd)
            {
                int steps = Mathf.Max(3, Mathf.RoundToInt(length / 12f));
                float a = angle, px0 = x, py0 = y;
                for (int i = 0; i < steps; i++)
                {
                    a += ((float)rnd.NextDouble() - 0.5f) * 0.5f;
                    float nx = px0 + Mathf.Cos(a) * 12f;
                    float ny = py0 + Mathf.Sin(a) * 12f;
                    Line(px, w, h, px0, py0, nx, ny, Mathf.Max(0.8f, width), color);
                    px0 = nx;
                    py0 = ny;
                    if (depth > 0 && i > 1 && rnd.NextDouble() < 0.2)
                    {
                        Branch(px, w, h, px0, py0, a + (rnd.NextDouble() < 0.5 ? -0.7f : 0.7f),
                            length * 0.5f, width * 0.6f, color, depth - 1, rnd);
                    }
                }
            }

            // ---- ring / shapes

            public static Sprite Ring(int size, float thickness)
            {
                return Get("ring" + size + "_" + thickness, delegate
                {
                    var tex = NewTex(size, size);
                    var px = new Color32[size * size];
                    float c = size * 0.5f;
                    float outer = c - 1f;
                    float inner = outer - thickness * 2f;
                    for (int y = 0; y < size; y++)
                    {
                        for (int x = 0; x < size; x++)
                        {
                            float dx = x + 0.5f - c, dy = y + 0.5f - c;
                            float d = Mathf.Sqrt(dx * dx + dy * dy);
                            float a = Mathf.Clamp01(outer - d) * Mathf.Clamp01(d - inner);
                            if (a <= 0.01f) continue;
                            px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                        }
                    }

                    tex.SetPixels32(px);
                    tex.Apply(false, true);
                    return FromTexture(tex);
                });
            }

            public static Sprite Crescent(int size)
            {
                return Get("crescent" + size, delegate
                {
                    var tex = NewTex(size, size);
                    var px = new Color32[size * size];
                    float c = size * 0.5f;
                    float r = size * 0.31f;
                    float cutR = r * 0.98f;
                    float cutX = c + r * 0.52f;
                    float cutY = c + r * 0.18f;

                    for (int y = 0; y < size; y++)
                    {
                        for (int x = 0; x < size; x++)
                        {
                            float dx = x + 0.5f - c, dy = y + 0.5f - c;
                            if (dx * dx + dy * dy > r * r) continue;
                            float cx = x + 0.5f - cutX, cy = y + 0.5f - cutY;
                            if (cx * cx + cy * cy <= cutR * cutR) continue;
                            px[y * size + x] = new Color32(255, 255, 255, 255);
                        }
                    }

                    tex.SetPixels32(px);
                    tex.Apply(false, true);
                    return FromTexture(tex);
                });
            }

            public static Sprite PreyRing()
            {
                return Get("preyring", delegate
                {
                    const int size = 128;
                    var tex = NewTex(size, size);
                    var px = new Color32[size * size];
                    float c = size * 0.5f;
                    var col = new Color(1f, 1f, 1f, 1f);

                    for (int k = 0; k < 3; k++)
                    {
                        Arc(px, size, size, c, c, c - 2f, c - 5f, -90f + k * 120f + 12f, -90f + k * 120f + 108f, col);
                    }

                    // eye slit
                    Arc(px, size, size, c, c, c - 30f, c - 33f, 200f, 340f, col);
                    Arc(px, size, size, c, c, c - 30f, c - 33f, 20f, 160f, col);
                    Disc(px, size, size, c, c, 3f, col);

                    tex.SetPixels32(px);
                    tex.Apply(false, true);
                    return FromTexture(tex);
                });
            }

            public static Sprite Chevrons()
            {
                return Get("chevrons", delegate
                {
                    const int size = 64;
                    var tex = NewTex(size, size);
                    var px = new Color32[size * size];
                    var col = new Color(1f, 1f, 1f, 1f);
                    for (int k = -1; k <= 1; k += 2)
                    {
                        Line(px, size, size, size * 0.5f + k * 10f, size * 0.2f, size * 0.5f + k * 2f, size * 0.5f,
                            3f, col);
                        Line(px, size, size, size * 0.5f + k * 2f, size * 0.5f, size * 0.5f + k * 10f, size * 0.8f,
                            3f, col);
                    }

                    tex.SetPixels32(px);
                    tex.Apply(false, true);
                    return FromTexture(tex);
                });
            }

            public static Sprite Cross()
            {
                return Get("cross", delegate
                {
                    const int size = 48;
                    var tex = NewTex(size, size);
                    var px = new Color32[size * size];
                    var col = new Color(1f, 1f, 1f, 1f);
                    Line(px, size, size, 10f, 10f, size - 10f, size - 10f, 2.5f, col);
                    Line(px, size, size, size - 10f, 10f, 10f, size - 10f, 2.5f, col);
                    tex.SetPixels32(px);
                    tex.Apply(false, true);
                    return FromTexture(tex);
                });
            }

            // ---- glyphs (white, tinted by the Image colour)

            public static Sprite Claw()
            {
                return Get("claw", delegate
                {
                    const int size = 64;
                    var tex = NewTex(size, size);
                    var px = new Color32[size * size];
                    var col = new Color(1f, 1f, 1f, 1f);
                    for (int k = -1; k <= 1; k++)
                    {
                        Line(px, size, size, 32f + k * 11f - 6f, 9f, 32f + k * 11f + 5f, 55f, 5f, col);
                    }

                    tex.SetPixels32(px);
                    tex.Apply(false, true);
                    return FromTexture(tex);
                });
            }

            public static Sprite Paw()
            {
                return Get("paw", delegate
                {
                    const int size = 64;
                    var tex = NewTex(size, size);
                    var px = new Color32[size * size];
                    var col = new Color(1f, 1f, 1f, 1f);
                    Disc(px, size, size, 28f, 40f, 7f, col);
                    Disc(px, size, size, 38f, 44f, 7f, col);
                    Disc(px, size, size, 46f, 40f, 6f, col);
                    Disc(px, size, size, 37f, 25f, 11f, col);
                    tex.SetPixels32(px);
                    tex.Apply(false, true);
                    return FromTexture(tex);
                });
            }

            public static Sprite Eye()
            {
                return Get("eye", delegate
                {
                    const int size = 64;
                    var tex = NewTex(size, size);
                    var px = new Color32[size * size];
                    var col = new Color(1f, 1f, 1f, 1f);
                    Arc(px, size, size, 32f, 32f, 26f, 23f, 200f, 340f, col);
                    Arc(px, size, size, 32f, 32f, 26f, 23f, 20f, 160f, col);
                    Disc(px, size, size, 32f, 32f, 6f, col);
                    tex.SetPixels32(px);
                    tex.Apply(false, true);
                    return FromTexture(tex);
                });
            }

            public static Sprite Leap()
            {
                return Get("leap", delegate
                {
                    const int size = 64;
                    var tex = NewTex(size, size);
                    var px = new Color32[size * size];
                    var col = new Color(1f, 1f, 1f, 1f);
                    for (int k = -1; k <= 1; k += 2)
                    {
                        float cx = 32f;
                        Line(px, size, size, cx + k * 4f, 10f, cx + k * 16f, 32f, 5f, col);
                        Line(px, size, size, cx + k * 16f, 32f, cx + k * 4f, 54f, 5f, col);
                    }

                    tex.SetPixels32(px);
                    tex.Apply(false, true);
                    return FromTexture(tex);
                });
            }
        }
    }
}
