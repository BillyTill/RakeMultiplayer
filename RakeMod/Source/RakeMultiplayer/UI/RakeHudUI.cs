using System;
using System.Collections.Generic;
using RakeMultiplayer.Networking;
using UnityEngine;
using UnityEngine.UI;

namespace RakeMultiplayer.UI
{
    /// <summary>
    /// The Rake's in-game HUD, built with uGUI in the same horror style as the menu:
    /// health plate, phase banner, phase timer, sense markers (no ring, the centre stays
    /// empty), side callouts, four ability cards and a thin frame.
    ///
    /// Created by <see cref="RakePlayerController"/> when a player takes the Rake and
    /// destroyed with it, so it only exists while someone is playing the Rake.
    /// </summary>
    public class RakeHudUI : MonoBehaviour
    {
        // ---- palette (same as RuntimeUIBuilder / LoadingScreen)
        private static readonly Color Red = new Color(0.72f, 0.16f, 0.12f, 1f);
        private static readonly Color RedDim = new Color(0.46f, 0.12f, 0.09f, 1f);
        private static readonly Color RedDeep = new Color(0.16f, 0.05f, 0.04f, 1f);
        private static readonly Color Cream = new Color(0.93f, 0.90f, 0.82f, 1f);
        private static readonly Color Muted = new Color(0.55f, 0.52f, 0.46f, 1f);
        private static readonly Color Dim = new Color(0.36f, 0.34f, 0.30f, 1f);
        private static readonly Color DayAccent = new Color(0.42f, 0.48f, 0.62f, 1f);

        private const float RefWidth = 1920f;
        private const float RefHeight = 1080f;
        private const float BandRadiusX = 470f;
        private const float BandRadiusY = 300f;
        private const int MarkerPoolSize = 20;

        // ---- layout constants (reference resolution)
        private const float CardSize = 132f;
        private const float CardGap = 12f;
        private const float BarWidth = 448f;

        private RakePlayerController _owner;

        private CanvasGroup _group;
        private float _fade;

        private Text _hpName;
        private Text _hpValue;
        private Text _hpSub;
        private Image _hpFill;

        private Text _phaseKicker;
        private Text _phaseTitle;
        private Image _phaseLineLeft;
        private Image _phaseLineRight;
        private Image _phaseDiamondLeft;
        private Image _phaseDiamondRight;

        private GameObject _timerPlate;
        private Image _moon;
        private Text _timerLabel;
        private Text _timerValue;
        private Image _timerFill;

        private Text _forwardLabel;
        private readonly List<Image> _markers = new List<Image>(MarkerPoolSize);

        private GameObject _preyPill;
        private Text _preyText;
        private GameObject _statePill;
        private Text _stateText;
        private GameObject _lockPill;
        private Text _lockText;
        private GameObject _dayPill;
        private Text _dayText;

        private readonly Image[] _sweeps = new Image[4];
        private readonly Text[] _cardNames = new Text[4];
        private readonly Text[] _cardStates = new Text[4];

        private float _slowRefresh;
        private float _fastRefresh;

        private Font _font;

        // ---------------------------------------------------------------- build

        public static RakeHudUI Create(RakePlayerController owner)
        {
            var go = new GameObject("RakeHud");
            go.transform.SetParent(owner.transform, false);

            var hud = go.AddComponent<RakeHudUI>();
            hud._owner = owner;

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
            _font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            var canvasGo = new GameObject("RakeHudCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 31000; // under the menu (32000) and the loader (33000)

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            var root = NewRect("Root", canvasGo.transform);
            Stretch(root);
            _group = root.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;

            BuildFrame(root);
            BuildHealthPlate(root);
            BuildPhase(root);
            BuildTimer(root);
            BuildSense(root);
            BuildSidePills(root);
            BuildAbilities(root);
            BuildFooter(root);
        }

        private void BuildFrame(RectTransform root)
        {
            // corner brackets
            AddCorner(root, new Vector2(0f, 1f), 1f, -1f);
            AddCorner(root, new Vector2(1f, 1f), -1f, -1f);
            AddCorner(root, new Vector2(0f, 0f), 1f, 1f);
            AddCorner(root, new Vector2(1f, 0f), -1f, 1f);

            // bottom hairline + diamond
            var line = NewImage("BottomLine", root, new Color(Red.r, Red.g, Red.b, 0.28f));
            var lrt = line.rectTransform;
            lrt.anchorMin = new Vector2(0f, 0f);
            lrt.anchorMax = new Vector2(1f, 0f);
            lrt.pivot = new Vector2(0.5f, 0f);
            lrt.anchoredPosition = new Vector2(0f, 6f);
            lrt.sizeDelta = new Vector2(-440f, 1f);

            var diamond = NewImage("BottomDiamond", root, new Color(Red.r, Red.g, Red.b, 0.55f), DiamondSprite());
            var drt = diamond.rectTransform;
            drt.anchorMin = new Vector2(0.5f, 0f);
            drt.anchorMax = new Vector2(0.5f, 0f);
            drt.pivot = new Vector2(0.5f, 0f);
            drt.anchoredPosition = new Vector2(0f, 1f);
            drt.sizeDelta = new Vector2(10f, 10f);
        }

        private void AddCorner(RectTransform root, Vector2 anchor, float dx, float dy)
        {
            var h = NewImage("CornerH", root, new Color(Red.r, Red.g, Red.b, 0.32f));
            var hrt = h.rectTransform;
            hrt.anchorMin = anchor;
            hrt.anchorMax = anchor;
            hrt.pivot = new Vector2(dx > 0f ? 0f : 1f, 0.5f);
            hrt.anchoredPosition = new Vector2(dx * 12f, dy * 12f);
            hrt.sizeDelta = new Vector2(46f, 2f);

            var v = NewImage("CornerV", root, new Color(Red.r, Red.g, Red.b, 0.32f));
            var vrt = v.rectTransform;
            vrt.anchorMin = anchor;
            vrt.anchorMax = anchor;
            vrt.pivot = new Vector2(0.5f, dy > 0f ? 0f : 1f);
            vrt.anchoredPosition = new Vector2(dx * 12f, dy * 12f);
            vrt.sizeDelta = new Vector2(2f, 46f);
        }

        private void BuildHealthPlate(RectTransform root)
        {
            var plate = NewImage("HealthPlate", root, new Color(0.04f, 0.04f, 0.05f, 0.80f));
            var rt = plate.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(28f, -26f);
            rt.sizeDelta = new Vector2(484f, 100f);

            var edge = NewImage("HealthEdge", plate.transform, Red);
            var ert = edge.rectTransform;
            ert.anchorMin = new Vector2(0f, 0f);
            ert.anchorMax = new Vector2(0f, 1f);
            ert.pivot = new Vector2(0f, 0.5f);
            ert.anchoredPosition = Vector2.zero;
            ert.sizeDelta = new Vector2(3f, 0f);

            _hpName = NewText("HpName", plate.transform, "THE RAKE", 13, Red, TextAnchor.MiddleLeft);
            SetRect(_hpName.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(18f, -16f), new Vector2(300f, 20f));
            _hpName.fontStyle = FontStyle.Bold;

            _hpValue = NewText("HpValue", plate.transform, "2500 / 2500", 13, Muted, TextAnchor.MiddleRight);
            SetRect(_hpValue.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-18f, -16f), new Vector2(220f, 20f));

            var track = NewImage("HpTrack", plate.transform, new Color(0.10f, 0.09f, 0.10f, 0.95f));
            var trt = track.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(0f, 1f);
            trt.pivot = new Vector2(0f, 1f);
            trt.anchoredPosition = new Vector2(18f, -50f);
            trt.sizeDelta = new Vector2(BarWidth, 12f);

            _hpFill = NewImage("HpFill", track.transform, Red);
            var frt = _hpFill.rectTransform;
            frt.anchorMin = new Vector2(0f, 0f);
            frt.anchorMax = new Vector2(0f, 1f);
            frt.pivot = new Vector2(0f, 0.5f);
            frt.anchoredPosition = Vector2.zero;
            frt.sizeDelta = new Vector2(BarWidth, 0f);

            for (int i = 1; i < 5; i++)
            {
                var tick = NewImage("HpTick" + i, track.transform, new Color(0.06f, 0.02f, 0.02f, 0.9f));
                var krt = tick.rectTransform;
                krt.anchorMin = new Vector2(0f, 0f);
                krt.anchorMax = new Vector2(0f, 1f);
                krt.pivot = new Vector2(0.5f, 0.5f);
                krt.anchoredPosition = new Vector2(BarWidth * i / 5f, 0f);
                krt.sizeDelta = new Vector2(1f, 0f);
            }

            _hpSub = NewText("HpSub", plate.transform, "UNDEAD  ·  NIGHT HUNTER", 10, Dim, TextAnchor.MiddleLeft);
            SetRect(_hpSub.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(18f, -74f), new Vector2(440f, 16f));
        }

        private void BuildPhase(RectTransform root)
        {
            _phaseKicker = NewText("PhaseKicker", root, "NIGHT 1 / 3", 12, Red, TextAnchor.MiddleCenter);
            SetRect(_phaseKicker.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -26f), new Vector2(700f, 18f));
            _phaseKicker.fontStyle = FontStyle.Bold;

            _phaseTitle = NewText("PhaseTitle", root, "THE HUNT IS ON", 34, Cream, TextAnchor.MiddleCenter);
            SetRect(_phaseTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -46f), new Vector2(900f, 54f));
            _phaseTitle.fontStyle = FontStyle.Bold;

            _phaseLineLeft = NewImage("PhaseLineL", root, new Color(Red.r, Red.g, Red.b, 0.35f));
            SetRect(_phaseLineLeft.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-327f, -80f), new Vector2(205f, 1f));

            _phaseLineRight = NewImage("PhaseLineR", root, new Color(Red.r, Red.g, Red.b, 0.35f));
            SetRect(_phaseLineRight.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(327f, -80f), new Vector2(205f, 1f));

            _phaseDiamondLeft = NewImage("PhaseDiamondL", root, Red, DiamondSprite());
            SetRect(_phaseDiamondLeft.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-436f, -80f), new Vector2(9f, 9f));

            _phaseDiamondRight = NewImage("PhaseDiamondR", root, Red, DiamondSprite());
            SetRect(_phaseDiamondRight.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(436f, -80f), new Vector2(9f, 9f));
        }

        private void BuildTimer(RectTransform root)
        {
            var plate = NewImage("TimerPlate", root, new Color(0.04f, 0.04f, 0.05f, 0.80f));
            _timerPlate = plate.gameObject;
            var rt = plate.rectTransform;
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-28f, -26f);
            rt.sizeDelta = new Vector2(220f, 100f);

            var edge = NewImage("TimerEdge", plate.transform, Red);
            var ert = edge.rectTransform;
            ert.anchorMin = new Vector2(1f, 0f);
            ert.anchorMax = new Vector2(1f, 1f);
            ert.pivot = new Vector2(1f, 0.5f);
            ert.anchoredPosition = Vector2.zero;
            ert.sizeDelta = new Vector2(3f, 0f);

            _moon = NewImage("Moon", plate.transform, Cream, MoonSprite());
            SetRect(_moon.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(16f, -16f), new Vector2(30f, 30f));

            _timerLabel = NewText("TimerLabel", plate.transform, "NIGHT ENDS IN", 11, Muted, TextAnchor.MiddleLeft);
            SetRect(_timerLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(58f, -18f), new Vector2(150f, 18f));

            _timerValue = NewText("TimerValue", plate.transform, "05:42", 30, Cream, TextAnchor.MiddleLeft);
            SetRect(_timerValue.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(56f, -34f), new Vector2(150f, 36f));
            _timerValue.fontStyle = FontStyle.Bold;

            var track = NewImage("TimerTrack", plate.transform, new Color(0.14f, 0.12f, 0.13f, 0.9f));
            SetRect(track.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(22f, 20f), new Vector2(176f, 4f));

            _timerFill = NewImage("TimerFill", track.transform, Red);
            var frt = _timerFill.rectTransform;
            frt.anchorMin = new Vector2(0f, 0f);
            frt.anchorMax = new Vector2(0f, 1f);
            frt.pivot = new Vector2(0f, 0.5f);
            frt.anchoredPosition = Vector2.zero;
            frt.sizeDelta = new Vector2(176f, 0f);
        }

        private void BuildSense(RectTransform root)
        {
            var layer = NewRect("Sense", root);
            Stretch(layer);

            _forwardLabel = NewText("Forward", layer, "FORWARD", 9, Muted, TextAnchor.MiddleCenter);
            SetRect(_forwardLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, BandRadiusY + 36f), new Vector2(200f, 16f));

            var fwdDiamond = NewImage("ForwardMark", layer, new Color(Red.r, Red.g, Red.b, 0.55f), DiamondSprite());
            SetRect(fwdDiamond.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, BandRadiusY + 20f), new Vector2(7f, 7f));

            var markerSprite = ArrowSprite();
            for (int i = 0; i < MarkerPoolSize; i++)
            {
                var marker = NewImage("Marker" + i, layer, Red, markerSprite);
                var mrt = marker.rectTransform;
                mrt.anchorMin = new Vector2(0.5f, 0.5f);
                mrt.anchorMax = new Vector2(0.5f, 0.5f);
                mrt.pivot = new Vector2(0.5f, 0.5f);
                mrt.sizeDelta = new Vector2(20f, 20f);
                marker.gameObject.SetActive(false);
                _markers.Add(marker);
            }
        }

        private void BuildSidePills(RectTransform root)
        {
            _preyPill = NewPill(root, "PreyPill", 196f, new Vector2(BandRadiusX - 30f, 110f), true, RedDim);
            _preyText = _preyPill.GetComponentInChildren<Text>();
            _preyText.text = "PREY IN REACH";

            _statePill = NewPill(root, "StatePill", 196f, new Vector2(BandRadiusX - 30f, 72f), true, RedDeep);
            _stateText = _statePill.GetComponentInChildren<Text>();
            _stateText.text = "CLIMBING";

            _lockPill = NewPill(root, "LockPill", 300f, new Vector2(-(BandRadiusX - 30f), -72f), false, Red);
            _lockText = _lockPill.GetComponentInChildren<Text>();
            _lockText.text = "DAMAGED  ·  ATTACK LOCKED 7s";

            _dayPill = NewPill(root, "DayPill", 260f, new Vector2(-(BandRadiusX - 30f), -110f), false, DayAccent);
            _dayText = _dayPill.GetComponentInChildren<Text>();
            _dayText.text = "ATTACKS DISABLED";

            // Everything is wired; hide until the state actually happens.
            _preyPill.SetActive(false);
            _statePill.SetActive(false);
            _lockPill.SetActive(false);
            _dayPill.SetActive(false);
        }

        private GameObject NewPill(RectTransform root, string name, float width, Vector2 pos, bool leftAligned, Color notch)
        {
            var bg = NewImage(name, root, new Color(0.04f, 0.04f, 0.05f, 0.82f));
            var rt = bg.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = leftAligned ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(width, 28f);

            var edge = NewImage("Notch", bg.transform, notch);
            var ert = edge.rectTransform;
            ert.anchorMin = new Vector2(leftAligned ? 0f : 1f, 0f);
            ert.anchorMax = new Vector2(leftAligned ? 0f : 1f, 1f);
            ert.pivot = new Vector2(leftAligned ? 0f : 1f, 0.5f);
            ert.anchoredPosition = Vector2.zero;
            ert.sizeDelta = new Vector2(3f, 0f);

            var label = NewText("Label", bg.transform, "", 10, Cream,
                leftAligned ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);
            label.fontStyle = FontStyle.Bold;
            var lrt = label.rectTransform;
            Stretch(lrt);
            lrt.offsetMin = leftAligned ? new Vector2(14f, 0f) : new Vector2(6f, 0f);
            lrt.offsetMax = leftAligned ? new Vector2(-6f, 0f) : new Vector2(-14f, 0f);

            return bg.gameObject;
        }

        private void BuildAbilities(RectTransform root)
        {
            var rowRect = NewRect("Abilities", root);
            rowRect.anchorMin = new Vector2(0f, 0f);
            rowRect.anchorMax = new Vector2(0f, 0f);
            rowRect.pivot = new Vector2(0f, 0f);
            rowRect.anchoredPosition = new Vector2(28f, 64f);
            rowRect.sizeDelta = new Vector2(CardSize * 4f + CardGap * 3f, CardSize);

            string[] names = { "GRAB", "ANIMAL", "CAMERA", "LEAP" };
            Sprite[] icons = { ClawSprite(), PawSprite(), CameraSprite(), LeapSprite() };

            for (int i = 0; i < 4; i++)
            {
                var card = NewImage("Card" + i, rowRect, new Color(0.23f, 0.09f, 0.08f, 0.75f));
                var crt = card.rectTransform;
                crt.anchorMin = new Vector2(0f, 0f);
                crt.anchorMax = new Vector2(0f, 0f);
                crt.pivot = new Vector2(0f, 0f);
                crt.anchoredPosition = new Vector2(i * (CardSize + CardGap), 0f);
                crt.sizeDelta = new Vector2(CardSize, CardSize);

                var fill = NewImage("CardFill", card.transform, new Color(0.035f, 0.035f, 0.045f, 0.86f));
                var frt = fill.rectTransform;
                Stretch(frt);
                frt.offsetMin = new Vector2(1f, 1f);
                frt.offsetMax = new Vector2(-1f, -1f);

                var notch = NewImage("CardNotch", card.transform, RedDim);
                var nrt = notch.rectTransform;
                nrt.anchorMin = new Vector2(0f, 1f);
                nrt.anchorMax = new Vector2(0f, 1f);
                nrt.pivot = new Vector2(0f, 1f);
                nrt.anchoredPosition = Vector2.zero;
                nrt.sizeDelta = new Vector2(2f, 18f);

                // cooldown ring behind the icon
                var sweep = NewImage("Sweep", card.transform, new Color(Red.r, Red.g, Red.b, 0.7f), RingSprite());
                sweep.type = Image.Type.Filled;
                sweep.fillMethod = Image.FillMethod.Radial360;
                sweep.fillOrigin = (int)Image.Origin360.Top;
                sweep.fillClockwise = true;
                sweep.fillAmount = 0f;
                var srt = sweep.rectTransform;
                srt.anchorMin = new Vector2(0.5f, 1f);
                srt.anchorMax = new Vector2(0.5f, 1f);
                srt.pivot = new Vector2(0.5f, 1f);
                srt.anchoredPosition = new Vector2(0f, -16f);
                srt.sizeDelta = new Vector2(76f, 76f);
                _sweeps[i] = sweep;

                var icon = NewImage("Icon", card.transform, Cream, icons[i]);
                icon.preserveAspect = true;
                var irt = icon.rectTransform;
                irt.anchorMin = new Vector2(0.5f, 1f);
                irt.anchorMax = new Vector2(0.5f, 1f);
                irt.pivot = new Vector2(0.5f, 1f);
                irt.anchoredPosition = new Vector2(0f, -30f);
                irt.sizeDelta = new Vector2(48f, 48f);

                var name = NewText("Name", card.transform, names[i], 10, Cream, TextAnchor.MiddleLeft);
                name.fontStyle = FontStyle.Bold;
                SetRect(name.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                    new Vector2(12f, 12f), new Vector2(80f, 16f));
                _cardNames[i] = name;

                var state = NewText("State", card.transform, "READY", 9, Red, TextAnchor.MiddleRight);
                state.fontStyle = FontStyle.Bold;
                SetRect(state.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                    new Vector2(-12f, 12f), new Vector2(70f, 16f));
                _cardStates[i] = state;
            }
        }

        private void BuildFooter(RectTransform root)
        {
            var left = NewText("FooterLeft", root,
                "WASD MOVE   SHIFT RUN   CTRL SNEAK   SPACE LEAP   V VIEW", 9, Dim, TextAnchor.MiddleLeft);
            SetRect(left.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(28f, 36f), new Vector2(760f, 16f));

            var right = NewText("FooterRight", root,
                "TAB CAMERAS   M MAP   F1 MENU", 9, Dim, TextAnchor.MiddleRight);
            SetRect(right.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-28f, 36f), new Vector2(520f, 16f));
        }

        // ---------------------------------------------------------------- runtime

        private void Update()
        {
            if (_owner == null) return;

            Transform rake = _owner.RakeTransform;
            if (rake == null)
            {
                Destroy(gameObject);
                return;
            }

            _fade = Mathf.MoveTowards(_fade, 1f, Time.unscaledDeltaTime * 4f);
            if (_group != null) _group.alpha = _fade;

            _slowRefresh -= Time.unscaledDeltaTime;
            if (_slowRefresh <= 0f)
            {
                _slowRefresh = 0.15f;
                RefreshTexts();
            }

            _fastRefresh -= Time.unscaledDeltaTime;
            if (_fastRefresh <= 0f)
            {
                _fastRefresh = 0.05f;
                RefreshMarkers(rake);
            }
        }

        private void RefreshTexts()
        {
            if (_phaseKicker == null || _phaseTitle == null || _hpValue == null) return;

            // ---- health
            float hp = _owner.Health;
            float max = Mathf.Max(1f, _owner.MaxHealth);
            float ratio = Mathf.Clamp01(hp / max);

            _hpValue.text = Mathf.CeilToInt(hp) + " / " + Mathf.CeilToInt(max);
            if (_hpFill != null) _hpFill.rectTransform.sizeDelta = new Vector2(BarWidth * ratio, 0f);

            // ---- phase / timer
            bool night = _owner.AttacksAllowedForHud;
            DayNightManager dayNight = DayNightManager.Instance;
            SessionManager session = SessionManager.Instance;

            int nightNumber = session != null ? Mathf.Max(1, session.CurrentNight) : 1;
            _phaseKicker.text = (night ? "NIGHT " : "DAY ") + nightNumber + " / 3";
            _phaseTitle.text = night ? "THE HUNT IS ON" : "THE RAKE SLEEPS";

            Color accent = night ? Red : DayAccent;
            _phaseKicker.color = accent;
            _phaseLineLeft.color = new Color(accent.r, accent.g, accent.b, 0.35f);
            _phaseLineRight.color = new Color(accent.r, accent.g, accent.b, 0.35f);
            _phaseDiamondLeft.color = accent;
            _phaseDiamondRight.color = accent;
            _moon.color = night ? Cream : new Color(0.72f, 0.76f, 0.85f, 1f);
            _timerValue.color = night ? Cream : new Color(0.80f, 0.84f, 0.92f, 1f);

            bool hasCycle = dayNight != null && dayNight.IsRunning;
            _timerPlate.SetActive(true);
            _timerLabel.text = night ? "NIGHT ENDS IN" : "DAY ENDS IN";

            float remaining = hasCycle ? dayNight.GetRemainingTime() : 0f;
            float duration = dayNight != null
                ? (dayNight.CurrentPhase == Phase.Day ? dayNight.DayDuration : dayNight.NightDuration)
                : 0f;

            _timerValue.text = hasCycle
                ? Mathf.FloorToInt(remaining / 60f).ToString("00") + ":" + Mathf.FloorToInt(remaining % 60f).ToString("00")
                : "--:--";

            float timerRatio = hasCycle && duration > 0.01f ? Mathf.Clamp01(remaining / duration) : 0f;
            _timerFill.rectTransform.sizeDelta = new Vector2(176f * timerRatio, 0f);

            // ---- abilities
            SetCard(0, _owner.GrabCooldownLeft, _owner.GrabCooldownTotal);
            SetCard(1, _owner.AnimalCooldownLeft, _owner.AnimalCooldownTotal);
            SetCard(2, _owner.CameraCooldownLeft, _owner.CameraCooldownTotal);
            SetCard(3, _owner.LeapCooldownLeft, _owner.LeapCooldownTotal);

            // ---- side callouts
            bool prey = night && _owner.PreyInReach;
            _preyPill.SetActive(prey);

            bool climbing = _owner.IsClimbing;
            _statePill.SetActive(climbing);
            if (climbing) _stateText.text = _owner.IsHanging ? "HANGING" : "CLIMBING";

            float lockout = _owner.DamageLockoutLeft;
            bool locked = lockout > 0.05f;
            _lockPill.SetActive(locked);
            if (locked) _lockText.text = "DAMAGED  ·  ATTACK LOCKED " + Mathf.CeilToInt(lockout) + "s";

            _dayPill.SetActive(!night);
        }

        private void SetCard(int index, float remaining, float total)
        {
            if (index < 0 || index >= _sweeps.Length) return;
            if (_sweeps[index] == null || _cardStates[index] == null || _cardNames[index] == null) return;

            bool ready = remaining <= 0.01f;
            _sweeps[index].gameObject.SetActive(!ready);
            if (!ready)
            {
                float ratio = Mathf.Clamp01(remaining / Mathf.Max(0.01f, total));
                _sweeps[index].fillAmount = ratio;
            }

            _cardStates[index].text = ready ? "READY" : Mathf.CeilToInt(remaining) + "s";
            _cardStates[index].color = ready ? Red : Cream;
            _cardNames[index].color = ready ? Cream : Muted;
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
            for (int i = 0; i < players.Count && used < _markers.Count; i++)
            {
                if (players[i] == null) continue;
                used = PlaceMarker(rake, players[i].transform.position, forward, right, used);
            }

            IList<Animal_AI> animals = _owner.SenseAnimals;
            for (int i = 0; i < animals.Count && used < _markers.Count; i++)
            {
                if (animals[i] == null) continue;
                used = PlaceMarker(rake, animals[i].transform.position, forward, right, used);
            }

            IList<Trap_proj> traps = _owner.SenseTraps;
            for (int i = 0; i < traps.Count && used < _markers.Count; i++)
            {
                if (traps[i] == null) continue;
                used = PlaceMarker(rake, traps[i].transform.position, forward, right, used);
            }

            for (int i = used; i < _markers.Count; i++)
            {
                if (_markers[i].gameObject.activeSelf) _markers[i].gameObject.SetActive(false);
            }
        }

        private int PlaceMarker(Transform rake, Vector3 world, Vector3 forward, Vector3 right, int index)
        {
            Vector3 delta = world - rake.position;
            delta.y = 0f;
            float distance = delta.magnitude;
            if (distance > 100f || distance < 0.2f) return index;

            float nx = Vector3.Dot(delta, right) / distance;
            float nz = Vector3.Dot(delta, forward) / distance;

            float x = nx * BandRadiusX;
            float y = nz * BandRadiusY;

            var marker = _markers[index];
            var rt = marker.rectTransform;
            rt.anchoredPosition = new Vector2(x, y);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(-x, -y) * Mathf.Rad2Deg);

            float alpha = 0.55f + 0.45f * (1f - distance / 100f);
            marker.color = new Color(Red.r, Red.g, Red.b, alpha);
            marker.gameObject.SetActive(true);
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

        private static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static Image NewImage(string name, Transform parent, Color color, Sprite sprite = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.sprite = sprite;
            if (sprite != null) img.preserveAspect = false;
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

        // ---------------------------------------------------------------- sprites

        private static Sprite _arrow;
        private static Sprite _diamond;
        private static Sprite _ring;
        private static Sprite _moonSprite;
        private static Sprite _claw;
        private static Sprite _paw;
        private static Sprite _camera;
        private static Sprite _leap;

        private static Sprite ArrowSprite() { return _arrow ?? (_arrow = MakeSprite(32, p => p.Triangle(16f, 30f, 5f, 4f, 27f, 4f))); }
        private static Sprite DiamondSprite() { return _diamond ?? (_diamond = MakeSprite(16, p => p.Diamond(8f, 8f, 7f))); }
        private static Sprite RingSprite() { return _ring ?? (_ring = MakeSprite(96, p => p.Ring(48f, 48f, 46f, 41f))); }
        private static Sprite MoonSprite() { return _moonSprite ?? (_moonSprite = MakeSprite(32, p => p.Moon(16f, 16f, 11f, 10f, 5f))); }

        private static Sprite ClawSprite()
        {
            if (_claw != null) return _claw;
            _claw = MakeSprite(64, p =>
            {
                p.Line(9f, 15f, 31f, 53f, 5f);
                p.Line(21f, 13f, 43f, 51f, 5f);
                p.Line(33f, 11f, 55f, 49f, 5f);
            });
            return _claw;
        }

        private static Sprite PawSprite()
        {
            if (_paw != null) return _paw;
            _paw = MakeSprite(64, p =>
            {
                p.Disc(20f, 44f, 6f);
                p.Disc(32f, 50f, 6f);
                p.Disc(44f, 44f, 6f);
                p.Ellipse(32f, 24f, 13f, 9f);
            });
            return _paw;
        }

        private static Sprite CameraSprite()
        {
            if (_camera != null) return _camera;
            _camera = MakeSprite(64, p =>
            {
                p.Rect(12f, 18f, 52f, 46f, 4f, false);
                p.Ring(32f, 32f, 11f, 7f);
                p.Disc(46f, 41f, 3f);
            });
            return _camera;
        }

        private static Sprite LeapSprite()
        {
            if (_leap != null) return _leap;
            _leap = MakeSprite(64, p =>
            {
                for (int k = 0; k < 2; k++)
                {
                    float x = 25f + k * 14f;
                    p.Line(x, 46f, x + 12f, 32f, 5f);
                    p.Line(x + 12f, 32f, x, 18f, 5f);
                }
                p.Line(13f, 42f, 21f, 42f, 3f);
                p.Line(13f, 22f, 21f, 22f, 3f);
            });
            return _leap;
        }

        private static Sprite MakeSprite(int size, Action<Painter> draw)
        {
            // Draw exactly at the requested size: the painter uses the same logical
            // coordinates the shapes are authored in (no hidden 2x canvas), otherwise
            // every sprite lands half-size in the bottom-left corner.
            var painter = new Painter(size, size);
            draw(painter);

            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(painter.Pixels);
            tex.Apply(false, true);

            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
        /// <summary>Tiny white painter for procedural HUD icons (texture Y points up).</summary>
        private sealed class Painter
        {
            private readonly int _w;
            private readonly int _h;
            private readonly Color32[] _pixels;

            public Painter(int w, int h)
            {
                _w = w;
                _h = h;
                _pixels = new Color32[w * h];
            }

            public Color32[] Pixels { get { return _pixels; } }

            public Color32 Get(int x, int y)
            {
                if (x < 0 || y < 0 || x >= _w || y >= _h) return new Color32(0, 0, 0, 0);
                return _pixels[y * _w + x];
            }

            public void Set(float x, float y)
            {
                int ix = Mathf.RoundToInt(x);
                int iy = Mathf.RoundToInt(y);
                if (ix < 0 || iy < 0 || ix >= _w || iy >= _h) return;
                _pixels[iy * _w + ix] = new Color32(255, 255, 255, 255);
            }

            public void Clear(float x, float y)
            {
                int ix = Mathf.RoundToInt(x);
                int iy = Mathf.RoundToInt(y);
                if (ix < 0 || iy < 0 || ix >= _w || iy >= _h) return;
                _pixels[iy * _w + ix] = new Color32(0, 0, 0, 0);
            }

            public void Disc(float cx, float cy, float r)
            {
                int x0 = Mathf.FloorToInt(cx - r - 1);
                int x1 = Mathf.CeilToInt(cx + r + 1);
                int y0 = Mathf.FloorToInt(cy - r - 1);
                int y1 = Mathf.CeilToInt(cy + r + 1);
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        float dx = x - cx, dy = y - cy;
                        if (dx * dx + dy * dy <= r * r) Set(x, y);
                    }
                }
            }

            public void Ellipse(float cx, float cy, float rx, float ry)
            {
                int x0 = Mathf.FloorToInt(cx - rx - 1);
                int x1 = Mathf.CeilToInt(cx + rx + 1);
                int y0 = Mathf.FloorToInt(cy - ry - 1);
                int y1 = Mathf.CeilToInt(cy + ry + 1);
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        float dx = (x - cx) / rx, dy = (y - cy) / ry;
                        if (dx * dx + dy * dy <= 1f) Set(x, y);
                    }
                }
            }

            public void Ring(float cx, float cy, float outer, float inner)
            {
                int x0 = Mathf.FloorToInt(cx - outer - 1);
                int x1 = Mathf.CeilToInt(cx + outer + 1);
                int y0 = Mathf.FloorToInt(cy - outer - 1);
                int y1 = Mathf.CeilToInt(cy + outer + 1);
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        float dx = x - cx, dy = y - cy;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (d <= outer && d >= inner) Set(x, y);
                    }
                }
            }

            public void Moon(float cx, float cy, float r, float cutR, float cutOffset)
            {
                Disc(cx, cy, r);
                for (int y = 0; y < _h; y++)
                {
                    for (int x = 0; x < _w; x++)
                    {
                        float dx = x - (cx + cutOffset), dy = y - (cy + cutOffset * 0.5f);
                        if (dx * dx + dy * dy <= cutR * cutR) Clear(x, y);
                    }
                }
            }

            public void Rect(float x0, float y0, float x1, float y1, float thickness, bool filled)
            {
                if (filled)
                {
                    for (int y = Mathf.FloorToInt(y0); y <= Mathf.CeilToInt(y1); y++)
                        for (int x = Mathf.FloorToInt(x0); x <= Mathf.CeilToInt(x1); x++)
                            Set(x, y);
                    return;
                }

                Line(x0, y0, x1, y0, thickness);
                Line(x0, y1, x1, y1, thickness);
                Line(x0, y0, x0, y1, thickness);
                Line(x1, y0, x1, y1, thickness);
            }

            public void Line(float x0, float y0, float x1, float y1, float width)
            {
                float dx = x1 - x0, dy = y1 - y0;
                int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) * 1.5f) + 1;
                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    Disc(x0 + dx * t, y0 + dy * t, Mathf.Max(0.6f, width * 0.5f));
                }
            }

            public void Diamond(float cx, float cy, float r)
            {
                Triangle(cx, cy + r, cx - r, cy, cx + r, cy);
                Triangle(cx, cy - r, cx - r, cy, cx + r, cy);
            }

            public void Triangle(float ax, float ay, float bx, float by, float cx, float cy)
            {
                float minX = Mathf.Min(ax, Mathf.Min(bx, cx));
                float maxX = Mathf.Max(ax, Mathf.Max(bx, cx));
                float minY = Mathf.Min(ay, Mathf.Min(by, cy));
                float maxY = Mathf.Max(ay, Mathf.Max(by, cy));

                for (int y = Mathf.FloorToInt(minY); y <= Mathf.CeilToInt(maxY); y++)
                {
                    for (int x = Mathf.FloorToInt(minX); x <= Mathf.CeilToInt(maxX); x++)
                    {
                        float d1 = (x - bx) * (ay - by) - (ax - bx) * (y - by);
                        float d2 = (x - cx) * (by - cy) - (bx - cx) * (y - cy);
                        float d3 = (x - ax) * (cy - ay) - (cx - ax) * (y - ay);
                        bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
                        bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
                        if (!(hasNeg && hasPos)) Set(x, y);
                    }
                }
            }
        }
    }
}
