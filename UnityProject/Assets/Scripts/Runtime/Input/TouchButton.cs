using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BorrowedSouls.InputHandling
{
    /// <summary>
    /// One on-screen action button. Held while a finger is on it; a press waits for its reader for one frame, so it
    /// is never lost to the order the event system and the game update in, and never fires twice.
    /// </summary>
    public sealed class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private Image _face;
        [SerializeField] private Color _idle = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] private Color _pressed = new Color(1f, 1f, 1f, 0.7f);
        [Tooltip("Optional radial fill drawn over the button while its move recharges.")]
        [SerializeField] private Image _cooldown;

        private int _pointer = int.MinValue;
        private bool _pending;
        private int _pendingFrame;

        public bool Held => _pointer != int.MinValue;

        /// <summary>True once per press, for the reader that acts on it.</summary>
        public bool TakePress()
        {
            if (!_pending) return false;
            _pending = false;
            return true;
        }

        /// <summary>Share of the cooldown still to go, 0 = ready.</summary>
        public void SetCooldown(float remaining)
        {
            if (_cooldown != null) _cooldown.fillAmount = Mathf.Clamp01(remaining);
        }

        public void OnPointerDown(PointerEventData e)
        {
            _pointer = e.pointerId;
            _pending = true;
            _pendingFrame = Time.frameCount;
            Tint();
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId == _pointer) Release();
        }

        // A finger sliding off lets go, like lifting it: a held soul button shouldn't stay held after a slip.
        public void OnPointerExit(PointerEventData e)
        {
            if (e.pointerId == _pointer && e.pointerPress == gameObject) Release();
        }

        private void LateUpdate()
        {
            if (_pending && Time.frameCount - _pendingFrame > 1) _pending = false; // nobody wanted it (e.g. paused)
        }

        private void OnDisable() => Release();

        private void Release()
        {
            _pointer = int.MinValue;
            Tint();
        }

        private void Tint()
        {
            if (_face != null) _face.color = Held ? _pressed : _idle;
        }
    }
}
