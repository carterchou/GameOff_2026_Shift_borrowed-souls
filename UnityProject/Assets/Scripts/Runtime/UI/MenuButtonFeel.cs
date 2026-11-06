using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BorrowedSouls.UI
{
    /// <summary>
    /// How a menu button answers: it grows and its label brightens when pointed at or selected, and sinks when pressed. Pointing at
    /// a button also selects it, so mouse and keys share one highlight and two buttons never light up at once. Menus run while
    /// the game is paused, so this uses real time.
    /// </summary>
    public sealed class MenuButtonFeel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private Button _button;
        [SerializeField] private Text _label;
        [SerializeField] private Color _labelNormal = new Color(0.84f, 0.81f, 0.74f);
        [SerializeField] private Color _labelLit = new Color(1f, 0.93f, 0.72f);
        [SerializeField, Min(1f)] private float _litScale = 1.03f;
        [SerializeField, Range(0.8f, 1f)] private float _pressedScale = 0.97f;
        [SerializeField, Min(1f)] private float _speed = 16f;

        private bool _pointed;
        private bool _selected;
        private bool _pressed;

        public void OnPointerEnter(PointerEventData eventData)
        {
            _pointed = true;
            if (_button.interactable && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _pointed = false;
            _pressed = false;
        }

        public void OnSelect(BaseEventData eventData) => _selected = true;

        public void OnDeselect(BaseEventData eventData) => _selected = false;

        public void OnPointerDown(PointerEventData eventData) => _pressed = true;

        public void OnPointerUp(PointerEventData eventData) => _pressed = false;

        private void OnDisable()
        {
            _pointed = _selected = _pressed = false;
            transform.localScale = Vector3.one;
            if (_label != null) _label.color = _labelNormal;
        }

        private void Update()
        {
            bool lit = _button.interactable && (_pointed || _selected);
            float wanted = _pressed ? _pressedScale : lit ? _litScale : 1f;
            float step = 1f - Mathf.Exp(-_speed * Time.unscaledDeltaTime);
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * wanted, step);
            if (_label != null) _label.color = Color.Lerp(_label.color, lit ? _labelLit : _labelNormal, step);
        }
    }
}
