using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RakeMultiplayer.UI
{
    /// <summary>Subtle scale feedback for menu buttons (hover/press) using unscaled time.</summary>
    public class ButtonHoverScale : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public float HoverScale = 1.035f;
        public float PressScale = 0.97f;
        public float Speed = 14f;

        private float _target = 1f;
        private float _current = 1f;
        private bool _hover;

        private void OnEnable()
        {
            _current = 1f;
            _target = 1f;
            transform.localScale = Vector3.one;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hover = true;
            _target = HoverScale;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hover = false;
            _target = 1f;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _target = PressScale;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _target = _hover ? HoverScale : 1f;
        }

        private void Update()
        {
            if (Mathf.Abs(_current - _target) < 0.0005f) return;

            _current = Mathf.Lerp(_current, _target, 1f - Mathf.Exp(-Speed * Time.unscaledDeltaTime));
            transform.localScale = new Vector3(_current, _current, 1f);
        }
    }

    /// <summary>
    /// Fade + short horizontal slide when a panel is enabled. Add it to panel roots
    /// (and the main container) for a softer menu transition.
    /// </summary>
    public class PanelAnimator : MonoBehaviour
    {
        public float Duration = 0.16f;
        public float Slide = 22f;
        public bool AnimateScale;

        private CanvasGroup _group;
        private RectTransform _rect;
        private Vector2 _target;
        private float _time;

        private void Awake()
        {
            _rect = transform as RectTransform;
            _target = _rect != null ? _rect.anchoredPosition : Vector2.zero;
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            if (_rect == null) _rect = transform as RectTransform;
            if (_group == null) _group = GetComponent<CanvasGroup>();

            _time = 0f;
            _group.alpha = 0f;
            if (_rect != null) _rect.anchoredPosition = _target + new Vector2(Slide, 0f);
            if (AnimateScale) transform.localScale = new Vector3(0.985f, 0.985f, 1f);
        }

        private void Update()
        {
            _time += Time.unscaledDeltaTime;
            float t = Duration > 0.001f ? Mathf.Clamp01(_time / Duration) : 1f;
            float e = 1f - Mathf.Pow(1f - t, 3f); // ease-out cubic

            if (_group != null) _group.alpha = e;
            if (_rect != null) _rect.anchoredPosition = Vector2.Lerp(_target + new Vector2(Slide, 0f), _target, e);
            if (AnimateScale) transform.localScale = Vector3.Lerp(new Vector3(0.985f, 0.985f, 1f), Vector3.one, e);

        }

        private void OnDisable()
        {
            if (_group != null) _group.alpha = 1f;
            if (_rect != null) _rect.anchoredPosition = _target;
            transform.localScale = Vector3.one;
        }
    }
}
