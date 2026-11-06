using BorrowedSouls.Bootstrap;
using UnityEngine;

namespace BorrowedSouls.InputHandling
{
    /// <summary>
    /// On-screen controls for phones and tablets in the browser: a floating move stick on the left and the action
    /// buttons on the right. Shown only on touch devices (or with <c>?touch=1</c> in the page address, for testing on
    /// a computer); hidden while the game is paused so menus take the taps. <see cref="PlayerInputDriver"/> reads it.
    /// </summary>
    public sealed class TouchControls : MonoBehaviour
    {
        [SerializeField] private GameRoot _root;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private TouchStick _stick;
        [SerializeField] private TouchButton _weak;
        [SerializeField] private TouchButton _strong;
        [SerializeField] private TouchButton _special;
        [SerializeField] private TouchButton _dodge;
        [SerializeField] private TouchButton _soulShift;
        [SerializeField] private TouchButton _target;
        [Tooltip("Show the controls in the Editor too, to try the layout with the mouse.")]
        [SerializeField] private bool _showInEditor;

        private static bool? _active;

        /// <summary>True on a touch device: the controls are up and hints name the on-screen buttons instead of keys.</summary>
        public static bool Active
        {
            get
            {
                if (_active == null) _active = Detect();
                return _active.Value;
            }
        }

        public bool Visible => isActiveAndEnabled && _group != null && _group.alpha > 0f;

        public Vector2 Move => Visible ? _stick.Value : Vector2.zero;
        public bool WeakPressed => Visible && _weak.TakePress();
        public bool WeakHeld => Visible && _weak.Held;
        public bool StrongPressed => Visible && _strong.TakePress();
        public bool SpecialPressed => Visible && _special.TakePress();
        public bool DodgePressed => Visible && _dodge.TakePress();
        public bool SoulShiftHeld => Visible && _soulShift.Held;
        public bool SoulShiftPressed => Visible && _soulShift.TakePress();
        public bool TargetPressed => Visible && _target.TakePress();

        private void Awake()
        {
            bool show = Active || (Application.isEditor && _showInEditor);
            if (show) _active = true; // the Editor preview behaves like a phone throughout, hints included
            gameObject.SetActive(show);
        }

        private void Update()
        {
            bool paused = _root != null && _root.Time.Stack.IsPaused;
            float alpha = paused ? 0f : 1f;
            if (!Mathf.Approximately(_group.alpha, alpha))
            {
                _group.alpha = alpha;
                _group.blocksRaycasts = !paused;
                if (paused) _stick.Release();
            }
        }

        private static bool Detect()
        {
            if (Application.isEditor) return false;
            if (Application.isMobilePlatform) return true;
            string url = Application.absoluteURL;
            return !string.IsNullOrEmpty(url) && url.Contains("touch=1");
        }
    }
}
