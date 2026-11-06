using BorrowedSouls.Bootstrap;
using BorrowedSouls.Soul;
using UnityEngine;

namespace BorrowedSouls.Feedback
{
    /// <summary>
    /// The mouse as a sword whose tip is the aim point, kept inside the window while playing so a click can't land on the desktop
    /// mid-fight. Menus and pause let it go. A hardware cursor, so it never lags behind the mouse.
    /// </summary>
    public sealed class GameCursor : MonoBehaviour
    {
        [SerializeField] private GameRoot _root;
        [SerializeField] private PlayerSoulController _soul;
        [SerializeField] private Texture2D _sword;
        [SerializeField] private Texture2D _soulSword;
        [Tooltip("The tip of the sword, in pixels from the texture's top left.")]
        [SerializeField] private Vector2 _hotspot = new Vector2(3f, 3f);

        private Texture2D _shown;

        public Texture2D Shown => _shown;

        private void LateUpdate()
        {
            Texture2D wanted = _soul != null && _soul.IsFree ? _soulSword : _sword;
            if (wanted != _shown && wanted != null)
            {
                _shown = wanted;
                Cursor.SetCursor(wanted, _hotspot, CursorMode.Auto);
            }
            bool playing = _root != null && !_root.Time.IsPaused; // the rest room has no run to keep the mouse in
            CursorLockMode mode = Application.isFocused && playing ? CursorLockMode.Confined : CursorLockMode.None;
            if (Cursor.lockState != mode) Cursor.lockState = mode;
            if (!Cursor.visible) Cursor.visible = true;
        }

        private void OnDisable()
        {
            _shown = null;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
