using UnityEngine;
using UnityEngine.EventSystems;

namespace BorrowedSouls.InputHandling
{
    /// <summary>
    /// Floating move stick: the base appears where the thumb lands inside this area and the knob follows the drag,
    /// up to <see cref="_radius"/>. Value is 0..1 in each axis, screen up = forward.
    /// </summary>
    public sealed class TouchStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform _area;
        [SerializeField] private RectTransform _base;
        [SerializeField] private RectTransform _knob;
        [SerializeField, Min(10f)] private float _radius = 120f;
        [SerializeField, Range(0f, 0.5f)] private float _deadZone = 0.12f;
        [SerializeField] private CanvasGroup _visual;
        [SerializeField, Range(0f, 1f)] private float _restAlpha = 0.35f;

        private int _pointer = int.MinValue;
        private Vector2 _restPosition;
        private Vector2 _origin;

        public Vector2 Value { get; private set; }

        private void Awake()
        {
            _restPosition = _base.anchoredPosition;
            ShowRest();
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (_pointer != int.MinValue) return;
            _pointer = e.pointerId;
            if (!ToLocal(e, out _origin)) return;
            _base.anchoredPosition = _origin;
            _knob.anchoredPosition = Vector2.zero;
            if (_visual != null) _visual.alpha = 1f;
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != _pointer || !ToLocal(e, out Vector2 local)) return;
            Vector2 offset = Vector2.ClampMagnitude(local - _origin, _radius);
            _knob.anchoredPosition = offset;
            Vector2 v = offset / _radius;
            float m = v.magnitude;
            Value = m < _deadZone ? Vector2.zero : v * ((m - _deadZone) / (1f - _deadZone) / m);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId == _pointer) Release();
        }

        public void Release()
        {
            _pointer = int.MinValue;
            Value = Vector2.zero;
            ShowRest();
        }

        private void OnDisable() => Release();

        private void ShowRest()
        {
            _base.anchoredPosition = _restPosition;
            _knob.anchoredPosition = Vector2.zero;
            if (_visual != null) _visual.alpha = _restAlpha;
        }

        private bool ToLocal(PointerEventData e, out Vector2 local)
        {
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, e.position, e.pressEventCamera, out local);
        }
    }
}
